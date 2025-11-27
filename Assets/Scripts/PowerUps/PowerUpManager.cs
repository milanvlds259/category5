using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Category5.Boss;
using Category5.Player;

namespace Category5.PowerUps
{
    // game state phases
    public enum GamePhase
    {
        Fighting,       // normal gameplay, fighting boss
        PowerUpSelection, // boss died, players selecting power-ups
        Victory,        // all rounds complete
        GameOver        // all players died
    }

    // manages the power-up selection flow and round progression
    public class PowerUpManager : NetworkBehaviour
    {
        public static PowerUpManager Instance { get; private set; }

        [Header("round settings")]
        [SerializeField] private int totalRounds = 3;
        [SerializeField] private int[] bossHealthPerRound = { 500, 800, 1200 };
        [SerializeField] private int powerUpChoicesPerPlayer = 3;

        [Header("references")]
        [SerializeField] private GameObject bossSpawnPoint;
        [SerializeField] private GameObject bossPrefab;

        // network variables for syncing game state
        public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(GamePhase.Fighting);
        public NetworkVariable<int> CurrentRound = new NetworkVariable<int>(1);

        // track which players have made their selection (server only)
        private Dictionary<ulong, bool> _playerSelections = new Dictionary<ulong, bool>();
        private Dictionary<ulong, int[]> _playerPowerUpChoices = new Dictionary<ulong, int[]>();

        // current boss reference
        private BossBase _currentBoss;

        // events for ui
        public event System.Action<int[]> OnShowPowerUpSelection; // indices of power-ups to show
        public event System.Action OnHidePowerUpSelection;
        public event System.Action OnVictory;
        public event System.Action OnGameOver;
        public event System.Action<int> OnRoundChanged;

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
                // find the boss in scene
                _currentBoss = FindFirstObjectByType<BossBase>();
                if (_currentBoss == null)
                {
                    Debug.LogWarning("PowerUpManager: No boss found in scene");
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            CurrentPhase.OnValueChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            Debug.Log($"PowerUpManager: Phase changed from {oldPhase} to {newPhase}");

            switch (newPhase)
            {
                case GamePhase.Fighting:
                    OnHidePowerUpSelection?.Invoke();
                    break;
                case GamePhase.PowerUpSelection:
                    // ui will be triggered by client rpc with choices
                    break;
                case GamePhase.Victory:
                    OnVictory?.Invoke();
                    break;
                case GamePhase.GameOver:
                    OnGameOver?.Invoke();
                    break;
            }
        }

        // called by BossBase when boss dies (server only)
        public void OnBossDied()
        {
            if (!IsServer) return;

            Debug.Log($"PowerUpManager: Boss died on round {CurrentRound.Value}");

            // check if this was the final round
            if (CurrentRound.Value >= totalRounds)
            {
                CurrentPhase.Value = GamePhase.Victory;
                TriggerVictoryClientRpc();
                return;
            }

            // enter power-up selection phase
            StartPowerUpSelection();
        }

        private void StartPowerUpSelection()
        {
            Debug.Log("PowerUpManager: Starting power-up selection phase");
            CurrentPhase.Value = GamePhase.PowerUpSelection;
            _playerSelections.Clear();
            _playerPowerUpChoices.Clear();

            // generate random power-up choices for each player
            var registry = PowerUpRegistry.Instance;
            if (registry == null)
            {
                Debug.LogError("PowerUpManager: PowerUpRegistry not found! Make sure PowerUpRegistry is in the scene with power-ups assigned.");
                return;
            }

            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                _playerSelections[clientId] = false;
                
                // generate random choices for this player
                int[] choices = registry.GetRandomPowerUpIndices(powerUpChoicesPerPlayer);
                _playerPowerUpChoices[clientId] = choices;

                // send choices to the specific client
                SendPowerUpChoicesClientRpc(choices, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { clientId }
                    }
                });
            }
        }

        [ClientRpc]
        private void SendPowerUpChoicesClientRpc(int[] powerUpIndices, ClientRpcParams clientRpcParams = default)
        {
            Debug.Log($"PowerUpManager: Received {powerUpIndices.Length} power-up choices");
            OnShowPowerUpSelection?.Invoke(powerUpIndices); // aaaa
        }

        [ClientRpc]
        private void TriggerVictoryClientRpc()
        {
            OnVictory?.Invoke();
        }

        // called by client ui when player makes a selection
        public void SelectPowerUp(int powerUpIndex)
        {
            if (!IsOwner && !IsClient) return;
            
            Debug.Log($"PowerUpManager: Local player selected power-up index {powerUpIndex}");
            SubmitSelectionServerRpc(powerUpIndex);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitSelectionServerRpc(int powerUpIndex, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            
            // validate the selection
            if (!_playerPowerUpChoices.TryGetValue(clientId, out int[] validChoices))
            {
                Debug.LogWarning($"PowerUpManager: Client {clientId} has no valid choices");
                return;
            }

            // check if this index was in their choices
            bool validSelection = false;
            foreach (int choice in validChoices)
            {
                if (choice == powerUpIndex)
                {
                    validSelection = true;
                    break;
                }
            }

            if (!validSelection)
            {
                Debug.LogWarning($"PowerUpManager: Client {clientId} selected invalid index {powerUpIndex}");
                return;
            }

            // mark player as having selected
            _playerSelections[clientId] = true;

            // apply the power-up to the player
            ApplyPowerUpToPlayer(clientId, powerUpIndex);

            // notify the client their selection was received
            AcknowledgeSelectionClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            });

            // check if all players have selected
            CheckAllPlayersSelected();
        }

        private void ApplyPowerUpToPlayer(ulong clientId, int powerUpIndex)
        {
            var registry = PowerUpRegistry.Instance;
            if (registry == null) return;

            var powerUp = registry.GetPowerUpByIndex(powerUpIndex);
            if (powerUp == null) return;

            // find the player's PlayerStats component
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                var playerStats = client.PlayerObject?.GetComponent<PlayerStats>();
                if (playerStats != null)
                {
                    playerStats.AddPowerUp(powerUp.UniqueId);
                    Debug.Log($"PowerUpManager: Applied {powerUp.PowerUpName} to player {clientId}");
                }
            }
        }

        [ClientRpc]
        private void AcknowledgeSelectionClientRpc(ClientRpcParams clientRpcParams = default)
        {
            Debug.Log("PowerUpManager: Selection acknowledged, waiting for other players...");
            // ui can show "waiting for other player" state or something
        }

        private void CheckAllPlayersSelected()
        {
            foreach (var kvp in _playerSelections)
            {
                if (!kvp.Value)
                {
                    Debug.Log($"PowerUpManager: Still waiting for player {kvp.Key}");
                    return;
                }
            }

            Debug.Log("PowerUpManager: All players selected, starting next round");
            StartNextRound();
        }

        private void StartNextRound()
        {
            CurrentRound.Value++;
            CurrentPhase.Value = GamePhase.Fighting;

            // respawn any dead players before starting next round
            RespawnAllPlayers();

            // respawn or reset the boss with new health
            RespawnBoss();

            // notify clients to hide selection ui
            HideSelectionUIClientRpc();
        }

        [ClientRpc]
        private void HideSelectionUIClientRpc()
        {
            OnHidePowerUpSelection?.Invoke();
        }

        private void RespawnBoss()
        {
            // get hp for current round
            int roundIndex = CurrentRound.Value - 1;
            int bossHp = roundIndex < bossHealthPerRound.Length 
                ? bossHealthPerRound[roundIndex] 
                : bossHealthPerRound[bossHealthPerRound.Length - 1];

            if (_currentBoss != null && _currentBoss.IsSpawned)
            {
                // reset existing boss
                _currentBoss.ResetBoss(bossHp);
            }
            else if (bossPrefab != null)
            {
                // spawn new boss
                Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.transform.position : Vector3.zero;
                Quaternion spawnRot = bossSpawnPoint != null ? bossSpawnPoint.transform.rotation : Quaternion.identity;

                var bossInstance = Instantiate(bossPrefab, spawnPos, spawnRot);
                var networkObj = bossInstance.GetComponent<NetworkObject>();
                if (networkObj != null)
                {
                    networkObj.Spawn();
                    _currentBoss = bossInstance.GetComponent<BossBase>();
                    _currentBoss?.ResetBoss(bossHp);
                }
            }
            else
            {
                Debug.LogError("PowerUpManager: Cannot respawn boss, no prefab or existing boss");
            }
        }

        // allow setting boss reference from boss script
        public void RegisterBoss(BossBase boss)
        {
            _currentBoss = boss;
        }
        
        // called when a player dies (server only)
        public void OnPlayerDied(ulong clientId)
        {
            if (!IsServer) return;
            
            Debug.Log($"PowerUpManager: Player {clientId} died, checking for game over");
            
            // don't trigger game over during power-up selection or if already game over
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
            Debug.Log($"PowerUpManager: Game Over on round {CurrentRound.Value}");
            CurrentPhase.Value = GamePhase.GameOver;
            TriggerGameOverClientRpc(CurrentRound.Value);
        }
        
        [ClientRpc]
        private void TriggerGameOverClientRpc(int roundReached)
        {
            Debug.Log($"Game Over! Reached round {roundReached}");
            OnGameOver?.Invoke();
        }
        
        // respawns all dead players (server only) - called at round transitions
        public void RespawnAllPlayers()
        {
            if (!IsServer) return;
            
            Debug.Log("PowerUpManager: Respawning all dead players");
            
            // reset spawn index for consistent spawning
            Category5.Core.PlayerSpawnPoint.ResetSpawnIndex();
            
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                {
                    var player = client.PlayerObject?.GetComponent<PlayerController>();
                    if (player != null && player.IsDead.Value)
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
    }
}
