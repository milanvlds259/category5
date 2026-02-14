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
        [SerializeField] private int[] bossHealthPerRound = { 500, 800, 1200 };

        [Header("enemy scaling")]
        [Tooltip("enemy count multiplier per round (index 0 = round 1)")]
        [SerializeField] private float[] enemyScalePerRound = { 1.0f, 1.5f, 2.0f };

        [Header("boss entrance")]
        [Tooltip("delay in seconds between all enemies cleared and boss appearing")]
        [SerializeField] private float bossEntranceDelay = 2f;

        [Header("references")]
        [SerializeField] private GameObject bossSpawnPoint;
        [SerializeField] private GameObject bossPrefab;

        // network variables for syncing game state
        public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(GamePhase.Fighting);
        public NetworkVariable<int> CurrentRound = new NetworkVariable<int>(1);

        // current boss reference
        private BossBase _currentBoss;

        // spawner tracking
        private EnemySpawner[] _allSpawners;
        private HashSet<EnemySpawner> _completedSpawners = new HashSet<EnemySpawner>();

        // events for ui and other systems
        public event Action OnVictory;
        public event Action OnGameOver;
        public event Action<int> OnRoundChanged;

        // events for boss entrance sequence (vfx/sfx hooks)
        public static event Action OnAllEnemiesCleared;
        public static event Action OnBossEntranceStart;

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
            CurrentPhase.OnValueChanged += OnPhaseChanged;
            CurrentRound.OnValueChanged += (old, newVal) => OnRoundChanged?.Invoke(newVal);

            if (IsServer)
            {
                // find the boss in scene and hide it until enemies are cleared
                _currentBoss = FindFirstObjectByType<BossBase>();
                if (_currentBoss != null)
                {
                    HideBossClientRpc();
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
                EnemySpawner.StartAllSpawners(multiplier);
            }
        }

        public override void OnNetworkDespawn()
        {
            CurrentPhase.OnValueChanged -= OnPhaseChanged;

            if (IsServer)
            {
                EnemySpawner.OnAllEnemiesDefeated -= OnSpawnerCompleted;
            }
        }

        private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            Debug.Log($"GameFlowManager: phase changed from {oldPhase} to {newPhase}");

            switch (newPhase)
            {
                case GamePhase.Victory:
                    OnVictory?.Invoke();
                    break;
                case GamePhase.GameOver:
                    OnGameOver?.Invoke();
                    break;
            }
        }

        // =====================================
        // enemy wave tracking
        // =====================================

        // called when an enemy spawner completes all waves and enemies are defeated
        private void OnSpawnerCompleted(EnemySpawner spawner)
        {
            if (!IsServer) return;

            // only track spawners we know about
            if (_allSpawners == null || Array.IndexOf(_allSpawners, spawner) < 0) return;

            _completedSpawners.Add(spawner);
            Debug.Log($"GameFlowManager: spawner completed ({_completedSpawners.Count}/{_allSpawners.Length})");

            // check if all active spawners have completed
            if (AreAllSpawnersComplete())
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
            Debug.Log("GameFlowManager: all enemy waves cleared, starting boss entrance sequence");
            OnAllEnemiesCleared?.Invoke();
            StartCoroutine(BossEntranceSequence());
        }

        private IEnumerator BossEntranceSequence()
        {
            yield return new WaitForSeconds(bossEntranceDelay);

            Debug.Log("GameFlowManager: boss entrance!");
            OnBossEntranceStart?.Invoke();

            SpawnOrRevealBoss();
        }

        private void SpawnOrRevealBoss()
        {
            // get hp for current round
            int roundIndex = CurrentRound.Value - 1;
            int bossHp = roundIndex < bossHealthPerRound.Length
                ? bossHealthPerRound[roundIndex]
                : bossHealthPerRound[bossHealthPerRound.Length - 1];

            // get spawn point position and rotation
            Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.transform.position : Vector3.zero;
            Quaternion spawnRot = bossSpawnPoint != null ? bossSpawnPoint.transform.rotation : Quaternion.identity;

            if (_currentBoss != null && _currentBoss.IsSpawned)
            {
                // reset existing boss (this shows it via ShowBossClientRpc internally)
                _currentBoss.ResetBoss(bossHp, spawnPos, spawnRot);
            }
            else if (bossPrefab != null)
            {
                // spawn new boss
                var bossInstance = Instantiate(bossPrefab, spawnPos, spawnRot);
                var networkObj = bossInstance.GetComponent<NetworkObject>();
                if (networkObj != null)
                {
                    networkObj.Spawn();
                    _currentBoss = bossInstance.GetComponent<BossBase>();
                    _currentBoss?.ResetBoss(bossHp, spawnPos, spawnRot);
                }
            }
            else
            {
                Debug.LogError("GameFlowManager: cannot spawn boss, no prefab or existing boss");
            }
        }

        // =====================================
        // boss death handling
        // =====================================

        // called by BossBase when boss dies (server only)
        public void OnBossDied()
        {
            if (!IsServer) return;

            Debug.Log($"GameFlowManager: boss died on round {CurrentRound.Value}");

            // check if this was the final round
            if (CurrentRound.Value >= totalRounds)
            {
                CurrentPhase.Value = GamePhase.Victory;
                TriggerVictoryClientRpc();
                return;
            }

            // boss is dead and enemies were already cleared -> start item selection
            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.StartItemSelection();
            }
            else
            {
                Debug.LogError("GameFlowManager: ItemManager not found, cannot start item selection");
            }
        }

        [ClientRpc]
        private void TriggerVictoryClientRpc()
        {
            // fire audio event for victory
            GameEvents.InvokeVictory();

            OnVictory?.Invoke();
        }

        // =====================================
        // round progression
        // =====================================

        // called by ItemManager when all players have made their item selection
        public void OnAllItemSelectionsComplete()
        {
            if (!IsServer) return;

            Debug.Log("GameFlowManager: all item selections complete, starting next round");
            StartNextRound();
        }

        private void StartNextRound()
        {
            CurrentRound.Value++;
            CurrentPhase.Value = GamePhase.Fighting;

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
            EnemySpawner.StartAllSpawners(multiplier);

            // notify clients to hide selection ui and fire round start event
            HideSelectionUIClientRpc();
        }

        [ClientRpc]
        private void HideSelectionUIClientRpc()
        {
            // fire audio event for round start
            GameEvents.InvokeRoundStart(CurrentRound.Value);

            // item manager will handle hiding its own ui via OnHideItemSelection
            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.NotifyHideSelectionUI();
            }
        }

        [ClientRpc]
        private void HideBossClientRpc()
        {
            if (_currentBoss != null)
            {
                _currentBoss.gameObject.SetActive(false);
            }
        }

        // =====================================
        // boss registration
        // =====================================

        // allow setting boss reference from boss script
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
            if (!IsServer) return;

            Debug.Log($"GameFlowManager: player {clientId} died, checking for game over");

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
            Debug.Log($"GameFlowManager: game over on round {CurrentRound.Value}");
            CurrentPhase.Value = GamePhase.GameOver;
            TriggerGameOverClientRpc(CurrentRound.Value);
        }

        [ClientRpc]
        private void TriggerGameOverClientRpc(int roundReached)
        {
            Debug.Log($"Game Over! Reached round {roundReached}");

            // fire audio event for game over
            GameEvents.InvokeGameOver();

            OnGameOver?.Invoke();
        }

        // =====================================
        // player respawn
        // =====================================

        // respawns all players to spawn positions (server only) - called at round transitions
        public void RespawnAllPlayers()
        {
            if (!IsServer) return;

            Debug.Log("GameFlowManager: respawning all players to spawn positions");

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
            if (!IsServer) return;

            Debug.Log($"GameFlowManager: handling disconnect for player {clientId}");

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
                Debug.Log("GameFlowManager: all players disconnected");
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
            Debug.Log($"GameFlowManager: found {_allSpawners.Length} spawners in scene");
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
