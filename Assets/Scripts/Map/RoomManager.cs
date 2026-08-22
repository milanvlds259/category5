using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Enemies;
using Category5.Items;

namespace Category5.Map
{
    // manages the active room lifecycle: instantiation, despawn, map selection, timers
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

        // van locked state — prevents players from exiting van during transition
        private bool _vanLocked = false;

        // events
        public static event Action<StormRoom> OnRoomEntered;
        public static event Action<StormRoom> OnRoomCleared;
        public static event Action OnRecallingStarted;
        public static event Action OnPrepStarted;
        public static event Action OnRoomTransitioning;

        private bool IsServerAuthority => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        // item selection tracking (room clear drops)
        private HashSet<ulong> _pendingItemSelections = new HashSet<ulong>();
        private float _dropCollectTimeout = 30f;
        private Coroutine _dropCollectionCoroutine;

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

            // clean up the room instance when the network shuts down
            // (disconnect, return to menu, scene change)
            if (_currentRoomInstance != null)
            {
                if (_currentRoomInstance.RoomSpawner != null)
                    _currentRoomInstance.RoomSpawner.StopSpawning();

                Destroy(_currentRoomInstance.gameObject);
                _currentRoomInstance = null;
            }
        }

        // =====================================
        // public API
        // =====================================

        // layout accessor for UI (map selection, etc.)
        public MapLayout Layout => _layout;

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

            // single-room flow: always spawn at scene center (below the van)
            InstantiateRoom(roomIndex, Vector3.zero);
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
                roomData.taskType
            );

            // apply difficulty scaling
            if (room.RoomSpawner != null && roomData.eyewallIndex >= 0 && _currentStorm != null)
            {
                float difficulty = _currentStorm.GetDifficultyMultiplier(roomData.eyewallIndex);
                room.RoomSpawner.SetDifficultyMultiplier(difficulty);
            }

            // disable spawner item drop — room manager handles drops now
            if (room.RoomSpawner != null)
            {
                room.RoomSpawner.SpawnItemDropOnClear = false;
            }

            // subscribe to room cleared event
            StormRoom.OnRoomCleared += HandleRoomCleared;

            // spawn the room's network object so IsServer returns true
            // and OnNetworkSpawn fires on StormRoom + EnemySpawner
            NetworkObject roomNetObj = instance.GetComponent<NetworkObject>();
            if (roomNetObj != null && !roomNetObj.IsSpawned)
            {
                roomNetObj.Spawn(true);
            }
            else if (roomNetObj == null)
            {
                Debug.LogWarning($"[RoomManager] room prefab '{prefab.name}' has no NetworkObject — spawner may not start");
            }

            _currentRoomInstance = room;
            room.SetActive();

            Debug.Log($"[RoomManager] instantiated room {roomIndex} at {worldPosition}");
        }

        // despawns the current room and cleans up enemies
        private void DespawnCurrentRoom()
        {
            if (_currentRoomInstance != null)
            {
                StormRoom.OnRoomCleared -= HandleRoomCleared;

                // despawn any alive enemies before destroying the room
                if (_currentRoomInstance.RoomSpawner != null)
                {
                    _currentRoomInstance.RoomSpawner.StopSpawning();
                }

                NetworkObject roomNetObj = _currentRoomInstance.GetComponent<NetworkObject>();
                if (roomNetObj != null && roomNetObj.IsSpawned)
                {
                    roomNetObj.Despawn();
                }
                else
                {
                    Destroy(_currentRoomInstance.gameObject);
                }

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
        // room cleared → recall → prep → transition
        // =====================================

        // called when the current room's spawner completes
        private void HandleRoomCleared(StormRoom room)
        {
            if (!IsServerAuthority) return;
            if (room != _currentRoomInstance) return;

            OnRoomCleared?.Invoke(room);

            // eye room is the boss room means no item drops
            if (room.EyewallIndex == -1)
            {
                Debug.Log($"[RoomManager] eye room cleared — skipping item drops, boss incoming");
                return;
            }

            Debug.Log($"[RoomManager] room {room.RoomIndex} cleared — spawning item drops");

            // spawn item drops for each alive player instead of auto-recalling
            SpawnRoomItemDrops();
        }

        // =====================================
        // item drop collection
        // =====================================

        private void SpawnRoomItemDrops()
        {
            CurrentState.Value = RoomState.ItemDrop;

            // get drop prefab from the rooms spawner
            var spawner = _currentRoomInstance?.RoomSpawner;
            if (spawner == null || spawner.ItemDropPrefab == null)
            {
                Debug.LogWarning("[RoomManager] no spawner or drop prefab — skipping item drops");
                StartCoroutine(RecallTimerCoroutine());
                return;
            }

            // count alive players
            int aliveCount = 0;
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                {
                    var player = client.PlayerObject?.GetComponent<Category5.Player.PlayerController>();
                    if (player != null && !player.IsDead.Value)
                        aliveCount++;
                }
            }

            if (aliveCount == 0)
            {
                Debug.Log("[RoomManager] no alive players — skipping item drops");
                StartCoroutine(RecallTimerCoroutine());
                return;
            }

            _pendingItemSelections.Clear();

            // track which players need to select an item
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                {
                    var player = client.PlayerObject?.GetComponent<Category5.Player.PlayerController>();
                    if (player != null && !player.IsDead.Value)
                        _pendingItemSelections.Add(clientId);
                }
            }

            for (int i = 0; i < aliveCount; i++)
            {
                // cycle through the rooms designated drop positions
                Vector3 pos = _currentRoomInstance.GetItemDropPosition(i);

                GameObject dropObj = Instantiate(spawner.ItemDropPrefab, pos, Quaternion.identity);
                NetworkObject netObj = dropObj.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                }
                else
                {
                    Debug.LogError("[RoomManager] item drop prefab has no NetworkObject");
                    Destroy(dropObj);
                }
            }

            // subscribe to item selection completion (fires when player picks an item from the UI)
            if (ItemManager.Instance != null)
                ItemManager.Instance.OnItemApplied += OnItemSelectionApplied;

            Debug.Log($"[RoomManager] spawned {aliveCount} item drops — waiting for item selection");

            // start timeout in case players cant collect (death, disconnect)
            _dropCollectionCoroutine = StartCoroutine(DropCollectionTimeout(_dropCollectTimeout));
        }

        private void OnItemSelectionApplied(ulong clientId, string itemId)
        {
            if (CurrentState.Value != RoomState.ItemDrop) return;
            if (!_pendingItemSelections.Remove(clientId)) return;

            Debug.Log($"[RoomManager] player {clientId} selected item — {_pendingItemSelections.Count} remaining");

            if (_pendingItemSelections.Count <= 0)
            {
                if (_dropCollectionCoroutine != null)
                    StopCoroutine(_dropCollectionCoroutine);
                StartCoroutine(RecallTimerCoroutine());
            }
        }

        private IEnumerator DropCollectionTimeout(float timeout)
        {
            yield return new WaitForSeconds(timeout);

            if (CurrentState.Value == RoomState.ItemDrop)
            {
                Debug.Log("[RoomManager] item drop collection timeout — forcing recall");
                StartCoroutine(RecallTimerCoroutine());
            }
        }

        private IEnumerator RecallTimerCoroutine()
        {
            CurrentState.Value = RoomState.Recalling;
            OnRecallingStarted?.Invoke();

            // unsubscribe from item selection
            if (ItemManager.Instance != null)
                ItemManager.Instance.OnItemApplied -= OnItemSelectionApplied;

            float recallTime = _currentStorm != null ? _currentStorm.recallTimer : 5f;
            yield return new WaitForSeconds(recallTime);

            // teleport all players to the van and lock exit
            TeleportPlayersToVan();
            LockVan();

            // wait for the host to select the next room via the map table
            // MapSelectionUI.OnNodeSelected() calls StartPrepTimerForRoom()
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
        // host auto-selection (replaces voting)
        // =====================================

        // the host (or solo player) picks the next room from connected options
        // this is a temporary solution — replace with a proper voting/selection system later
        private int HostSelectNextRoom()
        {
            if (_layout == null || _currentRoomInstance == null) return -1;

            int currentRoomIdx = _currentRoomInstance.RoomIndex;
            List<int> connectedRooms = _layout.GetConnectedRooms(currentRoomIdx);

            if (connectedRooms.Count == 0)
            {
                Debug.LogWarning("[RoomManager] no connected rooms from room " + currentRoomIdx);
                return -1;
            }

            // single connected room — go there automatically
            if (connectedRooms.Count == 1)
            {
                Debug.Log($"[RoomManager] auto-selected room {connectedRooms[0]} (only option)");
                return connectedRooms[0];
            }

            // multiple options — pick randomly for now
            // replace this with a host selection UI when ready
            int picked = connectedRooms[UnityEngine.Random.Range(0, connectedRooms.Count)];
            Debug.Log($"[RoomManager] host auto-selected room {picked} from {connectedRooms.Count} options");
            return picked;
        }

        // =====================================
        // van locking
        // =====================================

        public bool IsVanLocked => _vanLocked;

        private void LockVan()
        {
            _vanLocked = true;
            Debug.Log("[RoomManager] van exit locked");
        }

        private void UnlockVan()
        {
            _vanLocked = false;
            Debug.Log("[RoomManager] van exit unlocked");
        }

        // =====================================
        // prep timer
        // =====================================

        // called by MapSelectionUI when the host picks a room
        public void StartPrepTimerForRoom(int nextRoomIndex)
        {
            if (!IsServerAuthority) return;
            StartPrepTimer(nextRoomIndex);
        }

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
            UnlockVan();

            // clear island selection state so players can collect drops in the next room
            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.ClearIslandSelectionState();
            }

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
        ItemDrop,       // item drops spawned, waiting for collection
        Recalling,      // all drops collected, waiting to recall to van
        Preparing,      // waiting for prep timer before next room
    }
}
