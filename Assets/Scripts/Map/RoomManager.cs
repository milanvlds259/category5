using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Enemies;

namespace Category5.Map
{
    // manages the active room lifecycle: instantiation, despawn, voting, timers
    // only one room exists at a time — old rooms are despawned when transitioning
    // the van stays at scene center; new rooms spawn at the old room's position
    public class RoomManager : NetworkBehaviour
    {
        public static RoomManager Instance { get; private set; }

        [Header("van reference")]
        [Tooltip("the van GameObject — stays at scene center, does not move")]
        [SerializeField] private Transform vanTransform;

        [Header("van hover height")]
        [Tooltip("height above the room where the van hovers")]
        [SerializeField] private float vanHoverHeight = 30f;

        // current room index (synced)
        public NetworkVariable<int> CurrentRoomIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // current room state (synced)
        public NetworkVariable<RoomState> CurrentState = new NetworkVariable<RoomState>(
            RoomState.Idle,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // the currently instantiated room GameObject
        private StormRoom _currentRoomInstance;

        // the layout (set by MapGenerator)
        private MapLayout _layout;

        // current storm data
        private StormData _currentStorm;

        // vote data for next room selection
        private RoomVoteData _voteData;

        // events
        public static event Action<StormRoom> OnRoomEntered;
        public static event Action<StormRoom> OnRoomCleared;
        public static event Action OnVoteStarted;
        public static event Action<int> OnVoteResolved;
        public static event Action OnPrepStarted;
        public static event Action OnRoomTransitioning;

        private bool IsServerAuthority => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public override void OnNetworkSpawn()
        {
            CurrentRoomIndex.OnValueChanged += OnRoomIndexChanged;
        }

        public override void OnNetworkDespawn()
        {
            CurrentRoomIndex.OnValueChanged -= OnRoomIndexChanged;
        }

        // =====================================
        // public API
        // =====================================

        // called by MapGenerator after layout is generated
        public void SetLayout(MapLayout layout)
        {
            _layout = layout;
        }

        // called by MapGenerator to set the storm data
        public void SetStorm(StormData storm)
        {
            _currentStorm = storm;
        }

        // starts the game at the given room index
        public void StartAtRoom(int roomIndex)
        {
            if (!IsServerAuthority) return;
            if (_layout == null)
            {
                Debug.LogError("[RoomManager] cannot start — no layout set");
                return;
            }

            // instantiate the starting room at its layout position
            InstantiateRoom(roomIndex, _layout.GetRoomPosition(roomIndex));
            CurrentRoomIndex.Value = roomIndex;
            CurrentState.Value = RoomState.Fighting;

            OnRoomEntered?.Invoke(_currentRoomInstance);
        }

        // =====================================
        // room instantiation
        // =====================================

        // instantiates a room prefab at the given world position
        private void InstantiateRoom(int roomIndex, Vector3 worldPosition)
        {
            if (_layout == null) return;

            var roomData = _layout.GetRoom(roomIndex);
            RoomPrefabPool pool = GetPrefabPoolForRoom(roomData);

            if (pool == null || pool.PrefabCount == 0)
            {
                Debug.LogError($"[RoomManager] no prefab pool for room {roomIndex}");
                return;
            }

            GameObject prefab = pool.GetRandomPrefab();
            if (prefab == null)
            {
                Debug.LogError($"[RoomManager] pool returned null prefab for room {roomIndex}");
                return;
            }

            GameObject instance = Instantiate(prefab, worldPosition, Quaternion.identity);
            instance.name = $"Room_{roomIndex}";

            StormRoom room = instance.GetComponent<StormRoom>();
            if (room == null)
            {
                Debug.LogError($"[RoomManager] room prefab '{prefab.name}' is missing StormRoom component");
                Destroy(instance);
                return;
            }

            // configure the room
            room.Configure(
                roomData.roomIndex,
                roomData.eyewallIndex,
                roomData.taskType,
                roomData.leftRoomIndex,
                roomData.rightRoomIndex,
                roomData.inwardRoomIndex
            );

            // apply difficulty scaling
            if (room.RoomSpawner != null && roomData.eyewallIndex >= 0 && _currentStorm != null)
            {
                float difficulty = _currentStorm.GetDifficultyMultiplier(roomData.eyewallIndex);
                room.RoomSpawner.SetDifficultyMultiplier(difficulty);
            }

            // subscribe to room cleared event
            StormRoom.OnRoomCleared += HandleRoomCleared;

            _currentRoomInstance = room;
            room.SetActive();

            Debug.Log($"[RoomManager] instantiated room {roomIndex} at {worldPosition}");
        }

        // despawns the current room
        private void DespawnCurrentRoom()
        {
            if (_currentRoomInstance != null)
            {
                StormRoom.OnRoomCleared -= HandleRoomCleared;
                Destroy(_currentRoomInstance.gameObject);
                _currentRoomInstance = null;
            }
        }

        // returns the prefab pool for a given room based on its eyewall index
        private RoomPrefabPool GetPrefabPoolForRoom(StormRoomData roomData)
        {
            if (_currentStorm == null) return null;

            if (roomData.eyewallIndex == -1)
            {
                // eye room
                return _currentStorm.eyeRoomPool;
            }

            return _currentStorm.GetPoolForRing(roomData.eyewallIndex);
        }

        // =====================================
        // room cleared → recall → vote → prep → transition
        // =====================================

        // called when the current room's spawner completes
        private void HandleRoomCleared(StormRoom room)
        {
            if (!IsServerAuthority) return;
            if (room != _currentRoomInstance) return;

            Debug.Log($"[RoomManager] room {room.RoomIndex} cleared — starting recall timer");
            OnRoomCleared?.Invoke(room);

            // start the recall timer
            StartCoroutine(RecallTimerCoroutine());
        }

        private IEnumerator RecallTimerCoroutine()
        {
            CurrentState.Value = RoomState.Recalling;

            float recallTime = _currentStorm != null ? _currentStorm.recallTimer : 5f;
            yield return new WaitForSeconds(recallTime);

            // teleport all players to the van
            TeleportPlayersToVan();

            // start the vote
            StartVote();
        }

        // teleports all players to the van spawn points
        private void TeleportPlayersToVan()
        {
            if (!IsServerAuthority) return;

            int spawnIndex = 0;
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                {
                    var player = client.PlayerObject?.GetComponent<Category5.Player.PlayerController>();
                    if (player != null)
                    {
                        var spawnPoint = PlayerSpawnPoint.GetVanSpawnPoint(spawnIndex);
                        if (spawnPoint != null)
                        {
                            player.RepositionPlayer(spawnPoint.transform.position, spawnPoint.transform.rotation);
                        }
                        spawnIndex++;
                    }
                }
            }
        }

        // =====================================
        // voting
        // =====================================

        // starts a vote for the next room
        private void StartVote()
        {
            if (!IsServerAuthority) return;
            if (_layout == null || _currentRoomInstance == null) return;

            // get connected rooms (left, right, inward)
            int currentRoomIdx = _currentRoomInstance.RoomIndex;
            List<int> connectedRooms = _layout.GetConnectedRooms(currentRoomIdx);

            if (connectedRooms.Count == 0)
            {
                Debug.LogWarning("[RoomManager] no connected rooms — cannot vote");
                // skip vote, go straight to prep with current room? or end game?
                StartPrepTimer(currentRoomIdx);
                return;
            }

            _voteData = new RoomVoteData(connectedRooms);
            CurrentState.Value = RoomState.Voting;

            OnVoteStarted?.Invoke();
            Debug.Log($"[RoomManager] vote started with {connectedRooms.Count} options");

            // start coroutine to wait for all votes
            StartCoroutine(WaitForVotesCoroutine());
        }

        // called by RoomVoteManager when a player casts a vote
        public void CastVote(ulong clientId, int roomIndex)
        {
            if (!IsServerAuthority) return;
            if (_voteData == null) return;

            _voteData.CastVote(clientId, roomIndex);
            Debug.Log($"[RoomManager] player {clientId} voted for room {roomIndex}");
        }

        // waits until all connected players have voted
        private IEnumerator WaitForVotesCoroutine()
        {
            while (_voteData != null && !_voteData.AllConnectedPlayersVoted(GetConnectedClientIds()))
            {
                yield return null;
            }

            if (_voteData == null) yield break;

            // resolve the vote
            int winningRoom = _voteData.GetWinningRoom();
            Debug.Log($"[RoomManager] vote resolved — winning room: {winningRoom}");

            OnVoteResolved?.Invoke(winningRoom);
            _voteData = null;

            // start prep timer
            StartPrepTimer(winningRoom);
        }

        // returns the list of currently connected client IDs
        private List<ulong> GetConnectedClientIds()
        {
            var ids = new List<ulong>();
            foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
            {
                ids.Add(id);
            }
            return ids;
        }

        // =====================================
        // prep timer
        // =====================================

        // starts the prep timer before transitioning to the next room
        private void StartPrepTimer(int nextRoomIndex)
        {
            if (!IsServerAuthority) return;

            CurrentState.Value = RoomState.Preparing;
            OnPrepStarted?.Invoke();

            Debug.Log($"[RoomManager] prep timer started — next room: {nextRoomIndex}");
            StartCoroutine(PrepTimerCoroutine(nextRoomIndex));
        }

        private IEnumerator PrepTimerCoroutine(int nextRoomIndex)
        {
            float prepTime = _currentStorm != null ? _currentStorm.prepTimer : 30f;
            yield return new WaitForSeconds(prepTime);

            // transition to the next room
            TransitionToRoom(nextRoomIndex);
        }

        // =====================================
        // room transition
        // =====================================

        // transitions from the current room to the next room
        // despawns the old room and spawns the new one at the old room's position
        private void TransitionToRoom(int nextRoomIndex)
        {
            if (!IsServerAuthority) return;

            OnRoomTransitioning?.Invoke();

            // remember the old room's position
            Vector3 oldPosition = _currentRoomInstance != null
                ? _currentRoomInstance.transform.position
                : Vector3.zero;

            // despawn the old room
            DespawnCurrentRoom();

            // spawn the new room at the old room's position
            InstantiateRoom(nextRoomIndex, oldPosition);
            CurrentRoomIndex.Value = nextRoomIndex;
            CurrentState.Value = RoomState.Fighting;

            OnRoomEntered?.Invoke(_currentRoomInstance);
            Debug.Log($"[RoomManager] transitioned to room {nextRoomIndex} at {oldPosition}");
        }

        // =====================================
        // callbacks
        // =====================================

        private void OnRoomIndexChanged(int previousValue, int newValue)
        {
            // clients can react to room changes here
        }
    }

    // room manager states
    public enum RoomState
    {
        Idle,           // not started
        Fighting,       // players in room, spawner running
        Recalling,      // room cleared, waiting to recall to van
        Voting,         // players in van, voting on next room
        Preparing,      // vote resolved, waiting for prep timer
    }
}
