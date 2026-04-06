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

namespace Category5.Core
{
    // manages game flow: round progression, boss lifecycle, enemy wave tracking,
    // player death/respawn, victory and game over conditions
    public class GameFlowManager : NetworkBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [Header("round settings")]
        [SerializeField] private int totalRounds = 3;

        [Header("boss configuration")]
        [Tooltip("boss to use per round — assign one entry per round, or just one entry to reuse the same boss with scaled hp")]
        [SerializeField] private BossData[] bossPerRound;

        [Header("enemy scaling")]
        [Tooltip("enemy count multiplier per round (index 0 = round 1)")]
        [SerializeField] private float[] enemyScalePerRound = { 1.0f, 1.5f, 2.0f };

        [Header("boss entrance")]
        [Tooltip("delay in seconds between all enemies cleared and boss appearing")]
        [SerializeField] private float bossEntranceDelay = 2f;

        [Header("references")]
        [SerializeField] private GameObject bossSpawnPoint;

        // network variables for syncing game state
        public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(GamePhase.Fighting);
        public NetworkVariable<int> CurrentRound = new NetworkVariable<int>(1);

        // current boss reference
        private BossBase _currentBoss;
        // tracks which BossData is currently active so we can detect boss swaps between rounds
        private BossData _currentBossData;

        // spawner tracking
        private EnemySpawner[] _allSpawners;
        private HashSet<EnemySpawner> _completedSpawners = new HashSet<EnemySpawner>();
        private bool _serverInitialized = false;
        private bool _bossEntranceTriggeredThisRound = false;

        // events for ui and other systems
        public event Action OnVictory;
        public event Action OnGameOver;
        public event Action<int> OnRoundChanged;

        // events for boss entrance sequence (vfx/sfx hooks)
        public static event Action OnAllEnemiesCleared;
        public static event Action OnBossEntranceStart;

        // fired server-side when a new round begins (before ClientRpc) — safe to subscribe from server-only code
        public static event Action<int> OnRoundStarted;

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
        }

        private void Update()
        {
            if (!IsServerAuthority) return;
            if (!_serverInitialized) TryInitializeServerFlow();
            if (!_serverInitialized) return;

            // fallback polling path in case spawner completion event subscription did not fire
            if (!_bossEntranceTriggeredThisRound && CurrentPhase.Value == GamePhase.Fighting && AreAllActiveSpawnersFullyComplete())
            {
                // Debug.Log("GameFlowManager: fallback detected all spawners complete, triggering boss entrance");
                OnAllWavesCleared();
            }
        }

        private void TryInitializeServerFlow()
        {
            if (_serverInitialized) return;
            if (!IsServerAuthority) return;

            // find the boss in scene and hide it until enemies are cleared
            _currentBoss = FindFirstObjectByType<BossBase>();
            if (_currentBoss != null)
            {
                _currentBossData = _currentBoss.BossData;
                _currentBoss.HideBoss();
            }
            else
            {
                Debug.LogWarning("GameFlowManager: no boss found in scene");
            }

            // subscribe to spawner completion events
            EnemySpawner.OnAllEnemiesDefeated += OnSpawnerCompleted;
            RefreshSpawners();

            // start enemy waves for round 1
            float multiplier = GetEnemyMultiplier(0);
            // EnemySpawner.StartAllSpawners(multiplier);
            _bossEntranceTriggeredThisRound = false;
            _serverInitialized = true;
            // Debug.Log("GameFlowManager: server flow initialized");
        }

        public override void OnNetworkDespawn()
        {
            if (IsServerAuthority)
            {
                EnemySpawner.OnAllEnemiesDefeated -= OnSpawnerCompleted;
            }

            _serverInitialized = false;
        }

        // =====================================
        // enemy wave tracking
        // =====================================

        // called when an enemy spawner completes all waves and enemies are defeated
        private void OnSpawnerCompleted(EnemySpawner spawner)
        {
            NotifySpawnerCompleted(spawner);
        }

        // robust server entrypoint for spawner completion
        public void NotifySpawnerCompleted(EnemySpawner spawner)
        {
            if (!IsServerAuthority) return;
            if (spawner == null) return;

            if (_allSpawners == null || _allSpawners.Length == 0)
            {
                RefreshSpawners();
            }

            _completedSpawners.Add(spawner);
            int totalKnown = _allSpawners != null ? _allSpawners.Length : 0;
            // Debug.Log($"GameFlowManager: spawner completed ({_completedSpawners.Count}/{totalKnown})");

            // use dynamic completion check to avoid stale cached arrays
            if (AreAllActiveSpawnersFullyComplete())
            {
                OnAllWavesCleared();
            }
        }

        private bool AreAllSpawnersComplete()
        {
            if (_allSpawners == null) return true;

            foreach (var spawner in _allSpawners)
            {
                if (spawner != null && spawner.IsActive && !_completedSpawners.Contains(spawner))
                {
                    return false;
                }
            }
            return true;
        }

        // =====================================
        // boss entrance sequence
        // =====================================

        private void OnAllWavesCleared()
        {
            if (_bossEntranceTriggeredThisRound) return;
            _bossEntranceTriggeredThisRound = true;
            // Debug.Log("GameFlowManager: all enemy waves cleared, starting boss entrance sequence");
            OnAllEnemiesCleared?.Invoke();
            StartCoroutine(BossEntranceSequence());
        }

        private IEnumerator BossEntranceSequence()
        {
            yield return new WaitForSeconds(bossEntranceDelay);

            // Debug.Log("GameFlowManager: boss entrance!");
            OnBossEntranceStart?.Invoke();

            SpawnOrRevealBoss();
        }

        private void SpawnOrRevealBoss()
        {
            int roundIndex = CurrentRound.Value - 1;
            BossData bossData = GetBossDataForRound(roundIndex);

            if (bossData == null)
            {
                Debug.LogError("GameFlowManager: no BossData configured for this round — assign entries to bossPerRound in the inspector");
                return;
            }

            // count connected players so boss hp scales with lobby size
            int playerCount = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 1;
            int bossHp = bossData.GetHealthForRound(roundIndex, totalRounds, playerCount);

            // get spawn point position and rotation
            Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.transform.position : Vector3.zero;
            Quaternion spawnRot = bossSpawnPoint != null ? bossSpawnPoint.transform.rotation : Quaternion.identity;

            bool needsNewBoss = _currentBoss == null || !_currentBoss.IsSpawned || _currentBossData != bossData;

            if (!needsNewBoss)
            {
                // same boss type — just reset it with scaled hp
                _currentBoss.ResetBoss(bossHp, spawnPos, spawnRot);
            }
            else
            {
                // different boss or no existing boss — despawn old one and spawn the new prefab
                if (_currentBoss != null && _currentBoss.IsSpawned)
                {
                    _currentBoss.GetComponent<NetworkObject>()?.Despawn();
                    _currentBoss = null;
                }

                if (bossData.bossPrefab == null)
                {
                    Debug.LogError($"GameFlowManager: BossData '{bossData.bossName}' has no bossPrefab assigned");
                    return;
                }

                var bossInstance = Instantiate(bossData.bossPrefab, spawnPos, spawnRot);
                var networkObj = bossInstance.GetComponent<NetworkObject>();
                if (networkObj != null)
                {
                    networkObj.Spawn();
                    _currentBoss = bossInstance.GetComponent<BossBase>();
                    _currentBossData = bossData;
                    // health is set inside BossBase.OnNetworkSpawn via InitializeFromData,
                    // but we call ResetBoss to apply the round-scaled hp and show the boss
                    _currentBoss?.ResetBoss(bossHp, spawnPos, spawnRot);
                }
            }
        }

        // returns the BossData to use for a given zero-based round index
        // falls back to the last entry if the round exceeds the array length
        private BossData GetBossDataForRound(int roundIndex)
        {
            if (bossPerRound == null || bossPerRound.Length == 0) return null;
            return roundIndex < bossPerRound.Length
                ? bossPerRound[roundIndex]
                : bossPerRound[bossPerRound.Length - 1];
        }

        // =====================================
        // boss death handling
        // =====================================

        // called by BossBase when boss dies (server only)
        public void OnBossDied()
        {
            if (!IsServerAuthority) return;

            // Debug.Log($"GameFlowManager: boss died on round {CurrentRound.Value}");

            // check if this was the final round
            if (CurrentRound.Value >= totalRounds)
            {
                CurrentPhase.Value = GamePhase.Victory;
                TriggerVictoryLocal();
                return;
            }

            // boss is dead and enemies were already cleared -> start item selection
            if (ItemManager.Instance != null)
            {
                // Debug.Log("GameFlowManager: triggering ItemManager.StartItemSelection");
                ItemManager.Instance.StartItemSelection();
            }
            else
            {
                Debug.LogError("GameFlowManager: ItemManager not found, cannot start item selection");
            }
        }

        private void TriggerVictoryLocal()
        {
            // fire audio event for victory
            GameEvents.InvokeVictory();

            NotifyVictoryClientRpc(); // fire on all clients and host
        }

        [ClientRpc]
        private void NotifyRoundChangedClientRpc(int round)
        {
            OnRoundChanged?.Invoke(round);
        }

        [ClientRpc]
        private void NotifyVictoryClientRpc()
        {
            // Debug.Log("Victory! All rounds complete.");
            OnVictory?.Invoke();
        }

        // =====================================
        // round progression
        // =====================================

        // called by ItemManager when all players have made their item selection
        public void OnAllItemSelectionsComplete()
        {
            if (!IsServerAuthority) return;

            // Debug.Log("GameFlowManager: all item selections complete, starting next round");
            StartNextRound();
        }

        private void StartNextRound()
        {
            CurrentRound.Value++;
            CurrentPhase.Value = GamePhase.Fighting;
            OnRoundStarted?.Invoke(CurrentRound.Value);
            NotifyRoundChangedClientRpc(CurrentRound.Value);
            _bossEntranceTriggeredThisRound = false;

            // reset spawner tracking for new round
            _completedSpawners.Clear();
            RefreshSpawners();

            // respawn any dead players before starting next round
            RespawnAllPlayers();

            // hide boss until enemies are cleared again
            if (_currentBoss != null)
            {
                _currentBoss.HideBoss();
            }

            // start enemy waves with scaling multiplier
            float multiplier = GetEnemyMultiplier(CurrentRound.Value - 1);
            // EnemySpawner.StartAllSpawners(multiplier);

            // notify clients to hide selection ui and fire round start event via ItemManager rpc
            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.NotifyRoundStartedAndHideSelection(CurrentRound.Value);
            }
        }

        // =====================================
        // boss registration
        // =====================================

        // allow setting boss reference from boss script
        public void RegisterBoss(BossBase boss)
        {
            _currentBoss = boss;
            _currentBossData = boss?.BossData;
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
            // Debug.Log($"GameFlowManager: game over on round {CurrentRound.Value}");
            CurrentPhase.Value = GamePhase.GameOver;
            TriggerGameOverLocal(CurrentRound.Value);
        }

        private void TriggerGameOverLocal(int roundReached)
        {
            // Debug.Log($"Game Over! Reached round {roundReached}");

            // fire audio event for game over
            GameEvents.InvokeGameOver();
            NotifyGameOverClientRpc(roundReached); // fire on all clients and host
        }

        [ClientRpc]
        private void NotifyGameOverClientRpc(int roundReached)
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

        private void RefreshSpawners()
        {
            _allSpawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
            // Debug.Log($"GameFlowManager: found {_allSpawners.Length} spawners in scene");
        }

        private bool AreAllActiveSpawnersFullyComplete()
        {
            var spawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
            if (spawners == null || spawners.Length == 0) return false;

            bool hasActiveSpawner = false;
            foreach (var spawner in spawners)
            {
                if (spawner == null || !spawner.IsActive) continue;
                hasActiveSpawner = true;

                bool completed = spawner.CurrentWave >= spawner.TotalWaves && spawner.AliveEnemyCount == 0 && !spawner.IsSpawning;
                if (!completed)
                {
                    return false;
                }
            }

            return hasActiveSpawner;
        }

        private float GetEnemyMultiplier(int roundIndex)
        {
            if (enemyScalePerRound == null || enemyScalePerRound.Length == 0) return 1.0f;

            return roundIndex < enemyScalePerRound.Length
                ? enemyScalePerRound[roundIndex]
                : enemyScalePerRound[enemyScalePerRound.Length - 1];
        }
    }
}
