using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Category5.Boss;
using Category5.Enemies;
using Category5.Player;
using Category5.Items;
using Category5.Audio;
using Category5.Map;

namespace Category5.Core
{
    // manages game flow: storm progression, room clearing, boss lifecycle,
    // player death/respawn, victory and game over conditions
    public class GameFlowManager : NetworkBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [Header("storm configuration")]
        [Tooltip("the storm to play this session — assigned from StormCategoryData")]
        [SerializeField] private StormData currentStorm;

        [Header("boss entrance")]
        [Tooltip("delay in seconds between all enemies cleared and boss appearing")]
        [SerializeField] private float bossEntranceDelay = 2f;

        [Header("references")]
        [SerializeField] private MapGenerator mapGenerator;

        // network variables for syncing game state
        public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(GamePhase.Fighting);

        // current storm layout — set by MapGenerator after generating the room graph
        private StormMapLayout _currentLayout;
        public StormMapLayout CurrentLayout => _currentLayout;

        // current room tracked server-side
        private StormRoom _currentRoom;

        // current boss reference
        private BossBase _currentBoss;

        // spawner tracking for the current room
        private EnemySpawner[] _roomSpawners;
        private HashSet<EnemySpawner> _completedSpawners = new HashSet<EnemySpawner>();
        private bool _serverInitialized = false;
        private bool _bossEntranceTriggered = false;

        // events for ui and other systems
        public event Action OnVictory;
        public event Action OnGameOver;

        // events for boss entrance sequence (vfx/sfx hooks)
        public static event Action OnAllEnemiesCleared;
        public static event Action OnBossEntranceStart;

        // events for room progression
        public static event Action<StormRoom> OnRoomEntered;
        public static event Action<StormRoom> OnRoomCleared;

        // wwise variables
        [SerializeField] private AK.Wwise.State CombatState;
        [SerializeField] private AK.Wwise.State ExploreState;
        [SerializeField] private AK.Wwise.State WinState;
        [SerializeField] private AK.Wwise.State LoseState;
        [SerializeField] private AK.Wwise.State MenuState;

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

        private void Start()
        {
            TryInitializeServerFlow();
        }

        public override void OnNetworkSpawn()
        {
            TryInitializeServerFlow();
            RoomTransitionManager.OnRoomEntered += HandleRoomEntered;
        }

        private void Update()
        {
            if (!IsServerAuthority) return;
            if (!_serverInitialized) return;

            // fallback polling path in case spawner completion event subscription did not fire
            if (!_bossEntranceTriggered && CurrentPhase.Value == GamePhase.Fighting && AreAllRoomSpawnersComplete())
            {
                OnAllRoomSpawnersCleared();
            }
        }

        private void TryInitializeServerFlow()
        {
            if (_serverInitialized) return;
            if (!IsServerAuthority) return;

            // if we have a storm and layout already, start it
            if (currentStorm != null && _currentLayout != null)
            {
                StartStorm();
                _serverInitialized = true;
                return;
            }

            // otherwise wait for MapGenerator to call SetStormData + SetLayout
        }

        /// <summary>
        /// called by MapGenerator to provide storm data before generating the layout
        /// </summary>
        public void SetStormData(StormData storm)
        {
            currentStorm = storm;
        }

        /// <summary>
        /// called by MapGenerator after building the room graph
        /// starts the storm from the outermost ring
        /// </summary>
        public void SetLayout(StormMapLayout layout)
        {
            _currentLayout = layout;
            if (!_serverInitialized && IsServerAuthority)
            {
                StartStorm();
                _serverInitialized = true;
            }
        }

        private void StartStorm()
        {
            if (!IsServerAuthority) return;
            if (_currentLayout == null)
            {
                Debug.LogError("[GameFlowManager] cannot start storm — no layout set");
                return;
            }

            // subscribe to spawner completion events
            EnemySpawner.OnAllEnemiesDefeated += OnSpawnerCompleted;

            // find the starting room and activate it
            int startIdx = _currentLayout.StartingRoomIndex;
            var startRoom = FindRoomByIndex(startIdx);
            if (startRoom == null)
            {
                Debug.LogError($"[GameFlowManager] starting room index {startIdx} not found in scene");
                return;
            }

            _currentRoom = startRoom;
            startRoom.SetActive();
            _bossEntranceTriggered = false;

            RefreshRoomSpawners();
            ExploreState.SetValue();

            // reposition all players to the starting room's spawn points
            // players spawn before the map generates, so we need to move them after
            StartCoroutine(TeleportPlayersToRoom(startRoom));
        }

        public override void OnNetworkDespawn()
        {
            if (IsServerAuthority)
            {
                EnemySpawner.OnAllEnemiesDefeated -= OnSpawnerCompleted;
            }

            _serverInitialized = false;
            RoomTransitionManager.OnRoomEntered -= HandleRoomEntered;
        }

        // =====================================
        // room progression
        // =====================================

        /// <summary>
        /// called by RoomTransitionManager when players enter a new room
        /// </summary>
        private void HandleRoomEntered(StormRoom newRoom)
        {
            if (!IsServerAuthority) return;

            // deactivate old room
            if (_currentRoom != null && _currentRoom != newRoom)
            {
                _currentRoom.SetCleared();
                OnRoomCleared?.Invoke(_currentRoom);
            }

            _currentRoom = newRoom;

            // check if this is the eye room — boss fight!
            if (_currentLayout != null && newRoom.RoomIndex == _currentLayout.EyeRoomIndex)
            {
                CombatState.SetValue();
            }
            else
            {
                ExploreState.SetValue();
            }

            // refresh spawner tracking for the new room
            RefreshRoomSpawners();
            _bossEntranceTriggered = false;

            OnRoomEntered?.Invoke(newRoom);
        }

        /// <summary>
        /// called when an enemy spawner completes all waves and enemies are defeated
        /// </summary>
        private void OnSpawnerCompleted(EnemySpawner spawner)
        {
            if (!IsServerAuthority) return;
            if (spawner == null) return;

            _completedSpawners.Add(spawner);

            if (AreAllRoomSpawnersComplete())
            {
                OnAllRoomSpawnersCleared();
            }
        }

        private void RefreshRoomSpawners()
        {
            _completedSpawners.Clear();
            _roomSpawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
        }

        private bool AreAllRoomSpawnersComplete()
        {
            var spawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
            if (spawners == null || spawners.Length == 0) return false;

            bool hasActiveSpawner = false;
            foreach (var spawner in spawners)
            {
                if (spawner == null) continue;
                hasActiveSpawner = true;

                bool completed = spawner.CurrentWave >= spawner.TotalWaves && spawner.AliveEnemyCount == 0 && !spawner.IsSpawning;
                if (!completed)
                {
                    return false;
                }
            }

            return hasActiveSpawner;
        }

        // =====================================
        // boss entrance sequence
        // =====================================

        private void OnAllRoomSpawnersCleared()
        {
            if (_bossEntranceTriggered) return;

            // only trigger boss in the eye room
            bool isEyeRoom = _currentLayout != null && _currentRoom != null
                             && _currentRoom.RoomIndex == _currentLayout.EyeRoomIndex;

            if (!isEyeRoom)
            {
                // room cleared — reveal adjacent rooms, players move on
                OnAllEnemiesCleared?.Invoke();
                return;
            }

            // eye room cleared — boss entrance
            _bossEntranceTriggered = true;
            OnAllEnemiesCleared?.Invoke();
            StartCoroutine(BossEntranceSequence());
            CombatState.SetValue();
        }

        private IEnumerator BossEntranceSequence()
        {
            yield return new WaitForSeconds(bossEntranceDelay);
            OnBossEntranceStart?.Invoke();
            SpawnOrRevealBoss();
        }

        private void SpawnOrRevealBoss()
        {
            if (currentStorm == null || currentStorm.bossForEye == null)
            {
                Debug.LogError("[GameFlowManager] no BossData for this storm — assign bossForEye in StormData");
                return;
            }

            BossData bossData = currentStorm.bossForEye;
            int playerCount = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 1;

            // spawn position from the eye room or fallback
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;
            if (_currentRoom != null)
            {
                Transform spawnPoint = _currentRoom.GetSpawnPoint(0);
                if (spawnPoint != null)
                {
                    spawnPos = spawnPoint.position;
                    spawnRot = spawnPoint.rotation;
                }
            }

            int bossHp = bossData.GetHealthForRound(0, 1, playerCount);

            if (_currentBoss != null && _currentBoss.IsSpawned)
            {
                _currentBoss.ResetBoss(bossHp, spawnPos, spawnRot);
            }
            else
            {
                if (_currentBoss != null && _currentBoss.IsSpawned)
                {
                    _currentBoss.GetComponent<NetworkObject>()?.Despawn();
                    _currentBoss = null;
                }

                if (bossData.bossPrefab == null)
                {
                    Debug.LogError($"[GameFlowManager] BossData '{bossData.bossName}' has no bossPrefab assigned");
                    return;
                }

                var bossInstance = Instantiate(bossData.bossPrefab, spawnPos, spawnRot);
                var networkObj = bossInstance.GetComponent<NetworkObject>();
                if (networkObj != null)
                {
                    networkObj.Spawn();
                    _currentBoss = bossInstance.GetComponent<BossBase>();
                    _currentBoss?.ResetBoss(bossHp, spawnPos, spawnRot);
                }
            }
        }

        // =====================================
        // boss death handling
        // =====================================

        // called by BossBase when boss dies (server only)
        public void OnBossDied()
        {
            if (!IsServerAuthority) return;

            // boss in the eye = storm complete — victory!
            CurrentPhase.Value = GamePhase.Victory;
            TriggerVictoryLocal();
        }

        private void TriggerVictoryLocal()
        {
            GameEvents.InvokeVictory();
            NotifyVictoryClientRpc();
            WinState.SetValue();
        }

        [ClientRpc]
        private void NotifyVictoryClientRpc()
        {
            OnVictory?.Invoke();
        }

        // =====================================
        // item selection callback (kept for ItemManager compatibility)
        // =====================================

        public void OnAllItemSelectionsComplete()
        {
            if (!IsServerAuthority) return;
            // in room-based system, item selection after boss = victory
        }

        // =====================================
        // boss registration
        // =====================================

        public void RegisterBoss(BossBase boss)
        {
            _currentBoss = boss;
        }

        // =====================================
        // player death and game over
        // =====================================

        // called when a player dies (server only)
        public void OnPlayerDied(ulong clientId)
        {
            if (!IsServerAuthority) return;

            // Debug.Log($"GameFlowManager: player {clientId} died, checking for game over");

            // don't trigger game over during item selection or if already game over
            if (CurrentPhase.Value == GamePhase.PowerUpSelection ||
                CurrentPhase.Value == GamePhase.GameOver ||
                CurrentPhase.Value == GamePhase.Victory)
            {
                return;
            }

            // check if all players are dead
            if (AreAllPlayersDead())
            {
                TriggerGameOver();
            }
        }

        // checks if all connected players are dead
        private bool AreAllPlayersDead()
        {
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                {
                    var player = client.PlayerObject?.GetComponent<PlayerController>();
                    if (player != null && !player.IsDead.Value)
                    {
                        // found an alive player
                        return false;
                    }
                }
            }
            return true;
        }

        // triggers game over state
        private void TriggerGameOver()
        {
            CurrentPhase.Value = GamePhase.GameOver;
            TriggerGameOverLocal();
        }

        private void TriggerGameOverLocal()
        {
            GameEvents.InvokeGameOver();
            NotifyGameOverClientRpc();
        }

        [ClientRpc]
        private void NotifyGameOverClientRpc()
        {
            OnGameOver?.Invoke();
        }

        // player respawn

        // respawns all players to spawn positions (server only) - called at round transitions
        public void RespawnAllPlayers()
        {
            if (!IsServerAuthority) return;

            // Debug.Log("GameFlowManager: respawning all players to spawn positions");

            // reset spawn index for consistent spawning
            PlayerSpawnPoint.ResetSpawnIndex();

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                {
                    var player = client.PlayerObject?.GetComponent<PlayerController>();
                    if (player != null)
                    {
                        player.Respawn();
                    }
                }
            }
        }

        // gets count of alive players
        public int GetAlivePlayerCount()
        {
            int count = 0;
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                {
                    var player = client.PlayerObject?.GetComponent<PlayerController>();
                    if (player != null && !player.IsDead.Value)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        // =====================================
        // disconnect handling
        // =====================================

        // called by NetworkSessionManager when a player disconnects mid-game (game flow side)
        public void HandlePlayerDisconnected(ulong clientId)
        {
            if (!IsServerAuthority) return;

            // Debug.Log($"GameFlowManager: handling disconnect for player {clientId}");

            // if we're fighting, check if all remaining players are dead (game over)
            if (CurrentPhase.Value == GamePhase.Fighting)
            {
                // give a short delay to let the disconnect fully process
                StartCoroutine(CheckGameOverAfterDisconnect());
            }
        }

        private IEnumerator CheckGameOverAfterDisconnect()
        {
            yield return new WaitForSeconds(0.1f);

            // check if any players remain
            if (NetworkManager.Singleton.ConnectedClientsIds.Count == 0)
            {
                // Debug.Log("GameFlowManager: all players disconnected");
                yield break;
            }

            // check if all remaining players are dead
            if (AreAllPlayersDead())
            {
                TriggerGameOver();
            }
        }

        // =====================================
        // helpers
        // =====================================

        /// <summary>
        /// finds a StormRoom by its room index in the scene
        /// </summary>
        private StormRoom FindRoomByIndex(int roomIndex)
        {
            var allRooms = FindObjectsByType<StormRoom>(FindObjectsSortMode.None);
            foreach (var room in allRooms)
            {
                if (room.RoomIndex == roomIndex)
                    return room;
            }
            return null;
        }

        /// <summary>
        /// returns the current storm data
        /// </summary>
        public StormData GetCurrentStorm() => currentStorm;

        /// <summary>
        /// returns the current room index
        /// </summary>
        public int GetCurrentRoomIndex() => _currentRoom != null ? _currentRoom.RoomIndex : -1;

        /// <summary>
        /// teleports all connected players to a room's spawn points
        /// polls until players actually exist — OnSceneLoadCompleted may fire after us
        /// </summary>
        private IEnumerator TeleportPlayersToRoom(StormRoom room)
        {
            if (!IsServerAuthority) yield break;
            if (room == null) yield break;

            // wait until at least one player has a spawned object (up to 5 seconds)
            float timeout = 5f;
            bool anyPlayerReady = false;
            while (!anyPlayerReady && timeout > 0f)
            {
                yield return null;
                timeout -= Time.deltaTime;

                foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
                {
                    if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)
                        && client.PlayerObject != null)
                    {
                        anyPlayerReady = true;
                        break;
                    }
                }
            }

            if (!anyPlayerReady)
            {
                Debug.LogError("[GameFlowManager] TeleportPlayersToRoom timed out — no players found after 5s");
                yield break;
            }

            // small extra wait to let player components finish OnNetworkSpawn
            yield return null;

            int spawnIndex = 0;
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                {
                    var player = client.PlayerObject?.GetComponent<Category5.Player.PlayerController>();
                    if (player != null)
                    {
                        Transform spawnPoint = room.GetSpawnPoint(spawnIndex);
                        if (spawnPoint != null)
                        {
                            player.RepositionPlayer(spawnPoint.position, spawnPoint.rotation);
                            Debug.Log($"[GameFlowManager] teleported player {clientId} to room {room.RoomIndex} at {spawnPoint.position}");
                        }
                        spawnIndex++;
                    }
                }
            }
        }
    }
}
