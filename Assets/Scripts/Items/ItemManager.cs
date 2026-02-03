using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using Category5.Boss;
using Category5.Enemies;
using Category5.Player;
using Category5.Audio;
using Category5.Core;

namespace Category5.Items
{
    // manages the item selection flow and round progression
    public class ItemManager : NetworkBehaviour
    {
        public static ItemManager Instance { get; private set; }

        [Header("round settings")]
        [SerializeField] private int totalRounds = 3;
        [SerializeField] private int[] bossHealthPerRound = { 500, 800, 1200 };
        [SerializeField] private int itemChoicesPerPlayer = 3;

        [Header("references")]
        [SerializeField] private GameObject bossSpawnPoint;
        [SerializeField] private GameObject bossPrefab;

        // network variables for syncing game state
        public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(GamePhase.Fighting);
        public NetworkVariable<int> CurrentRound = new NetworkVariable<int>(1);

        // track which players have made their selection (server only)
        private Dictionary<ulong, bool> _playerSelections = new Dictionary<ulong, bool>();
        private Dictionary<ulong, string[]> _playerItemChoices = new Dictionary<ulong, string[]>(); // item ids

        // current boss reference
        private BossBase _currentBoss;

        // round end tracking - requires both boss dead AND all enemies defeated
        private bool _bossDead = false;
        private EnemySpawner[] _allSpawners; // all spawners in scene
        private HashSet<EnemySpawner> _completedSpawners = new HashSet<EnemySpawner>();

        // events for ui
        public event System.Action<string[]> OnShowItemSelection; // item ids to show
        public event System.Action OnHideItemSelection;
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
                    Debug.LogWarning("ItemManager: No boss found in scene");
                }

                // subscribe to spawner completion events and find all spawners
                EnemySpawner.OnAllEnemiesDefeated += OnSpawnerCompleted;
                RefreshSpawners();
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
            Debug.Log($"ItemManager: Phase changed from {oldPhase} to {newPhase}");

            switch (newPhase)
            {
                case GamePhase.Fighting:
                    OnHideItemSelection?.Invoke();
                    break;
                case GamePhase.PowerUpSelection: // reusing same phase enum for item selection
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

            Debug.Log($"ItemManager: Boss died on round {CurrentRound.Value}");

            // check if this was the final round
            if (CurrentRound.Value >= totalRounds)
            {
                CurrentPhase.Value = GamePhase.Victory;
                TriggerVictoryClientRpc();
                return;
            }

            // mark boss as dead and try to start item selection
            _bossDead = true;
            TryStartItemSelection();
        }

        // called when an enemy spawner completes all waves and enemies are defeated
        private void OnSpawnerCompleted(EnemySpawner spawner)
        {
            if (!IsServer) return;

            // only track spawners we know about
            if (_allSpawners == null || System.Array.IndexOf(_allSpawners, spawner) < 0) return;

            _completedSpawners.Add(spawner);
            Debug.Log($"ItemManager: Spawner completed ({_completedSpawners.Count} completed)");

            TryStartItemSelection();
        }

        // attempts to start item selection if both boss and all enemies are dead
        private void TryStartItemSelection()
        {
            // check if all active spawners have completed
            // we check IsActive dynamically because spawners may activate after ItemManager spawns
            bool allEnemiesDefeated = true;
            int activeCount = 0;
            int completedCount = 0;

            if (_allSpawners != null)
            {
                foreach (var spawner in _allSpawners)
                {
                    if (spawner != null && spawner.IsActive)
                    {
                        activeCount++;
                        if (_completedSpawners.Contains(spawner))
                        {
                            completedCount++;
                        }
                        else
                        {
                            // found an active spawner that hasn't completed
                            allEnemiesDefeated = false;
                        }
                    }
                }
            }

            Debug.Log($"ItemManager: Round end check - Boss dead={_bossDead}, Spawners completed={completedCount}/{activeCount}");

            if (_bossDead && allEnemiesDefeated)
            {
                Debug.Log("ItemManager: Both boss and all enemies defeated, starting item selection");
                StartItemSelection();
            }
        }

        // finds all spawners in the scene
        private void RefreshSpawners()
        {
            _allSpawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
            Debug.Log($"ItemManager: Found {_allSpawners.Length} spawners in scene");
        }

        private void StartItemSelection()
        {
            Debug.Log("ItemManager: Starting item selection phase");
            CurrentPhase.Value = GamePhase.PowerUpSelection; // reusing enum value
            _playerSelections.Clear();
            _playerItemChoices.Clear();

            // generate random item choices for each player
            var registry = ItemRegistry.Instance;
            if (registry == null)
            {
                Debug.LogError("ItemManager: ItemRegistry not found! Make sure ItemRegistry is in the scene with items assigned.");
                return;
            }

            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                _playerSelections[clientId] = false;

                // get player inventory to check for duplicates
                PlayerInventory playerInventory = null;
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                {
                    playerInventory = client.PlayerObject?.GetComponent<PlayerInventory>();
                }

                // generate random choices for this player
                var choices = registry.GetRandomItems(itemChoicesPerPlayer, playerInventory);
                FixedString64Bytes[] choiceIds = new FixedString64Bytes[choices.Count];
                string[] choiceIdStrings = new string[choices.Count];
                for (int i = 0; i < choices.Count; i++)
                {
                    choiceIds[i] = choices[i].UniqueId;
                    choiceIdStrings[i] = choices[i].UniqueId;
                }
                _playerItemChoices[clientId] = choiceIdStrings;

                // check if player inventory is full
                bool inventoryFull = playerInventory != null && playerInventory.IsFull;

                // send choices to the specific client
                SendItemChoicesClientRpc(choiceIds, inventoryFull, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { clientId }
                    }
                });
            }
        }

        [ClientRpc]
        private void SendItemChoicesClientRpc(FixedString64Bytes[] itemIds, bool inventoryFull, ClientRpcParams clientRpcParams = default)
        {
            // convert FixedString64Bytes[] to string[] for event
            string[] itemIdStrings = new string[itemIds.Length];
            for (int i = 0; i < itemIds.Length; i++)
            {
                itemIdStrings[i] = itemIds[i].ToString();
            }
            
            Debug.Log($"ItemManager: Received {itemIdStrings.Length} item choices, inventory full: {inventoryFull}");

            // fire audio event for item selection start
            GameEvents.InvokePowerUpSelectionStart(); // reusing power-up event

            OnShowItemSelection?.Invoke(itemIdStrings);
        }

        [ClientRpc]
        private void TriggerVictoryClientRpc()
        {
            // fire audio event for victory
            GameEvents.InvokeVictory();

            OnVictory?.Invoke();
        }

        // called by client ui when player selects an item (inventory not full)
        public void SelectItem(string itemId)
        {
            if (!IsOwner && !IsClient) return;

            Debug.Log($"ItemManager: Local player selected item {itemId}");
            SubmitItemSelectionServerRpc(itemId, -1); // -1 means no replacement
        }

        // called by client ui when player selects an item to replace (inventory full)
        public void SelectItemWithReplacement(string itemId, int slotToReplace)
        {
            if (!IsOwner && !IsClient) return;

            Debug.Log($"ItemManager: Local player selected item {itemId} to replace slot {slotToReplace}");
            SubmitItemSelectionServerRpc(itemId, slotToReplace);
        }

        // called by client ui when player skips selection (inventory full and doesn't want to replace)
        public void SkipSelection()
        {
            if (!IsOwner && !IsClient) return;

            Debug.Log("ItemManager: Local player skipped item selection");
            SubmitItemSelectionServerRpc("", -1); // empty string means skip
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitItemSelectionServerRpc(string itemId, int slotToReplace, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // validate the selection
            if (!_playerItemChoices.TryGetValue(clientId, out string[] validChoices))
            {
                Debug.LogWarning($"ItemManager: Client {clientId} has no valid choices");
                return;
            }

            // handle skip
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.Log($"ItemManager: Client {clientId} skipped selection");
                _playerSelections[clientId] = true;
                AcknowledgeSelectionClientRpc(new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { clientId }
                    }
                });
                CheckAllPlayersSelected();
                return;
            }

            // check if this item id was in their choices
            bool validSelection = false;
            foreach (string choice in validChoices)
            {
                if (choice == itemId)
                {
                    validSelection = true;
                    break;
                }
            }

            if (!validSelection)
            {
                Debug.LogWarning($"ItemManager: Client {clientId} selected invalid item {itemId}");
                return;
            }

            // mark player as having selected
            _playerSelections[clientId] = true;

            // apply the item to the player
            ApplyItemToPlayer(clientId, itemId, slotToReplace);

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

        private void ApplyItemToPlayer(ulong clientId, string itemId, int slotToReplace)
        {
            var registry = ItemRegistry.Instance;
            if (registry == null) return;

            var item = registry.GetItemById(itemId);
            if (item == null)
            {
                Debug.LogWarning($"ItemManager: Item {itemId} not found in registry");
                return;
            }

            // find the player's PlayerInventory component
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                var playerInventory = client.PlayerObject?.GetComponent<PlayerInventory>();
                if (playerInventory != null)
                {
                    bool success;
                    if (slotToReplace >= 0)
                    {
                        // replace existing item
                        success = playerInventory.ReplaceItem(slotToReplace, itemId);
                        Debug.Log($"ItemManager: Replaced slot {slotToReplace} with {item.ItemName} for player {clientId}, success: {success}");
                    }
                    else
                    {
                        // add to inventory
                        success = playerInventory.AddItem(itemId);
                        Debug.Log($"ItemManager: Added {item.ItemName} to player {clientId}, success: {success}");
                    }

                    if (success)
                    {
                        // fire audio event for item selected (to the specific client)
                        NotifyItemSelectedClientRpc(item.ItemName, new ClientRpcParams
                        {
                            Send = new ClientRpcSendParams
                            {
                                TargetClientIds = new ulong[] { clientId }
                            }
                        });
                    }
                }
            }
        }

        [ClientRpc]
        private void NotifyItemSelectedClientRpc(string itemName, ClientRpcParams clientRpcParams = default)
        {
            GameEvents.InvokePowerUpSelected(itemName); // reusing power-up event
        }

        [ClientRpc]
        private void AcknowledgeSelectionClientRpc(ClientRpcParams clientRpcParams = default)
        {
            Debug.Log("ItemManager: Selection acknowledged, waiting for other players...");
            // ui can show "waiting for other players" state
        }

        private void CheckAllPlayersSelected()
        {
            foreach (var kvp in _playerSelections)
            {
                if (!kvp.Value)
                {
                    Debug.Log($"ItemManager: Still waiting for player {kvp.Key}");
                    return;
                }
            }

            Debug.Log("ItemManager: All players selected, starting next round");
            StartNextRound();
        }

        private void StartNextRound()
        {
            CurrentRound.Value++;
            CurrentPhase.Value = GamePhase.Fighting;

            // reset round end tracking for new round
            _bossDead = false;
            _completedSpawners.Clear();
            RefreshSpawners();

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
            // fire audio event for round start
            GameEvents.InvokeRoundStart(CurrentRound.Value);

            OnHideItemSelection?.Invoke();
        }

        private void RespawnBoss()
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
                // reset existing boss and teleport to spawn point
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
                Debug.LogError("ItemManager: Cannot respawn boss, no prefab or existing boss");
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

            Debug.Log($"ItemManager: Player {clientId} died, checking for game over");

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
            Debug.Log($"ItemManager: Game Over on round {CurrentRound.Value}");
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

        // respawns all players to spawn positions (server only) - called at round transitions
        public void RespawnAllPlayers()
        {
            if (!IsServer) return;

            Debug.Log("ItemManager: Respawning all players to spawn positions");

            // reset spawn index for consistent spawning
            Category5.Core.PlayerSpawnPoint.ResetSpawnIndex();

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

        // called by NetworkSessionManager when a player disconnects mid-game
        public void HandlePlayerDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            Debug.Log($"ItemManager: Handling disconnect for player {clientId}");

            // if we're in item selection, mark them as selected so we don't wait forever
            if (CurrentPhase.Value == GamePhase.PowerUpSelection)
            {
                if (_playerSelections.ContainsKey(clientId))
                {
                    _playerSelections[clientId] = true; // mark as "selected" so we don't wait
                    _playerItemChoices.Remove(clientId);

                    Debug.Log($"ItemManager: Marked disconnected player {clientId} as selected");

                    // check if all remaining players have now selected
                    CheckAllPlayersSelected();
                }
            }

            // if we're fighting, check if all remaining players are dead (game over)
            if (CurrentPhase.Value == GamePhase.Fighting)
            {
                // give a short delay to let the disconnect fully process
                StartCoroutine(CheckGameOverAfterDisconnect());
            }
        }

        private System.Collections.IEnumerator CheckGameOverAfterDisconnect()
        {
            yield return new WaitForSeconds(0.1f);

            // check if any players remain
            if (NetworkManager.Singleton.ConnectedClientsIds.Count == 0)
            {
                Debug.Log("ItemManager: All players disconnected");
                yield break;
            }

            // check if all remaining players are dead
            if (AreAllPlayersDead())
            {
                TriggerGameOver();
            }
        }
    }
}
