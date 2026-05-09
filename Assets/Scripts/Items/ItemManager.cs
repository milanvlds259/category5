using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using Category5.Player;
using Category5.Audio;
using Category5.Core;

namespace Category5.Items
{
    /// <summary>
    /// Manages item selection flow — generating choices, sending to clients, and applying to inventory.
    /// Supports two selection modes:
    ///   - Boss selection: synchronous, all players select, blocks round progression.
    ///   - Island selection: asynchronous, per-player, does NOT block round progression.
    /// Multiple island selections can be active simultaneously. If a player collides with an ItemDrop
    /// while already in boss selection, the island selection is queued and processed after the
    /// boss selection (or round advancement) completes.
    /// </summary>
    public class ItemManager : NetworkBehaviour
    {
        public static ItemManager Instance { get; private set; }

        [Header("Item Settings")]
        [SerializeField] private int itemChoicesPerPlayer = 3;

        // track which players have made their selection (server only) — BOSS selection only
        private Dictionary<ulong, bool> _playerSelections = new Dictionary<ulong, bool>();
        private Dictionary<ulong, string[]> _playerItemChoices = new Dictionary<ulong, string[]>(); // item ids

        // island selection tracking (per-player async selection, does not block round)
        private Dictionary<ulong, string[]> _islandPlayerItemChoices = new Dictionary<ulong, string[]>();

        // pending island selections queued when a player is already in boss selection UI
        private Queue<ulong> _pendingIslandSelections = new Queue<ulong>();

        private bool IsServerAuthority => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        // events for ui
        public event System.Action<string[]> OnShowItemSelection; // item ids to show
        public event System.Action OnHideItemSelection;
        /// <summary>Fired server-side after an item is successfully applied to a player's inventory.</summary>
        public event System.Action<ulong, string> OnItemApplied;

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

        /// <summary>
        /// Called by GameFlowManager on server to hide selection UI on all clients.
        /// Also processes any pending island selections that were deferred during the previous round.
        /// </summary>
        public void NotifyRoundStartedAndHideSelection(int round)
        {
            if (!IsServerAuthority) return;

            // clear any stale boss selection state from the previous round
            _playerSelections.Clear();
            _playerItemChoices.Clear();

            NotifyRoundStartedAndHideSelectionClientRpc(round);
            // process all island selections that were queued during the previous round
            ProcessQueuedIslandSelections();
        }

        [ClientRpc]
        private void NotifyRoundStartedAndHideSelectionClientRpc(int round)
        {
            // fire audio event for round start
            GameEvents.InvokeRoundStart(round);
            OnHideItemSelection?.Invoke();
        }

        /// <summary>
        /// Starts boss item selection for all connected players. This is a synchronous selection
        /// that blocks round progression until all players have submitted their choices.
        /// Server authority required — clients must not call this directly.
        /// </summary>
        public void StartItemSelection()
        {
            if (!IsServerAuthority) return;

            // Debug.Log("ItemManager: starting item selection phase");

            // set phase via GameFlowManager
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.CurrentPhase.Value = GamePhase.PowerUpSelection;
            }

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

        /// <summary>
        /// Called when a player collides with an ItemDrop from a cleared island spawner.
        /// Sends item choices to only the specified client without blocking round progression.
        /// If the player is already in boss selection, the island selection is queued instead.
        /// </summary>
        public void StartItemSelectionForPlayer(ulong clientId)
        {
            if (!IsServerAuthority) return;

            // prevent duplicate island selections for the same client
            if (_islandPlayerItemChoices.ContainsKey(clientId)) return;

            // if player is already in boss selection, queue the island selection
            if (_playerItemChoices.ContainsKey(clientId))
            {
                // Debug.Log($"ItemManager: client {clientId} already in boss selection, queuing island selection");
                if (!_pendingIslandSelections.Contains(clientId))
                    _pendingIslandSelections.Enqueue(clientId);
                return;
            }

            var registry = ItemRegistry.Instance;
            if (registry == null)
            {
                Debug.LogError("ItemManager: ItemRegistry not found! Cannot start island item selection.");
                return;
            }

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
            _islandPlayerItemChoices[clientId] = choiceIdStrings;

            // check if player inventory is full
            bool inventoryFull = playerInventory != null && playerInventory.IsFull;

            // send choices to the specific client only
            SendItemChoicesClientRpc(choiceIds, inventoryFull, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            });
        }

        /// <summary>
        /// Processes all queued island selections. Draining a fixed snapshot of the queue,
        /// re-enqueuing clients still in boss selection so they are retried later.
        /// </summary>
        private void ProcessQueuedIslandSelections()
        {
            if (!IsServerAuthority) return;

            // snapshot the queue size to prevent infinite loop when re-enqueueing
            // clients that are still in boss selection
            int count = _pendingIslandSelections.Count;
            for (int i = 0; i < count; i++)
            {
                ulong nextClientId = _pendingIslandSelections.Dequeue();

                // skip disconnected clients
                if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(nextClientId)) continue;

                // if client is still in boss selection, re-queue them to retry later
                if (_playerItemChoices.ContainsKey(nextClientId))
                {
                    _pendingIslandSelections.Enqueue(nextClientId);
                    continue;
                }

                // Debug.Log($"ItemManager: processing queued island selection for client {nextClientId}");
                StartItemSelectionForPlayer(nextClientId);
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

            // Debug.Log($"ItemManager: Received {itemIdStrings.Length} item choices, inventory full: {inventoryFull}");

            // fire audio event for item selection start
            GameEvents.InvokePowerUpSelectionStart(); // reusing power-up event

            OnShowItemSelection?.Invoke(itemIdStrings);
        }

        // called by client ui when player selects an item (inventory not full)
        public void SelectItem(string itemId)
        {
            if (!IsClient) return;

            // Debug.Log($"ItemManager: Local player selected item {itemId}");
            SubmitItemSelectionServerRpc(itemId, -1); // -1 means no replacement
        }

        // called by client ui when player selects an item to replace (inventory full)
        public void SelectItemWithReplacement(string itemId, int slotToReplace)
        {
            if (!IsClient) return;

            // Debug.Log($"ItemManager: Local player selected item {itemId} to replace slot {slotToReplace}");
            SubmitItemSelectionServerRpc(itemId, slotToReplace);
        }

        // called by client ui when player skips selection (inventory full and doesn't want to replace)
        public void SkipSelection()
        {
            if (!IsClient) return;

            // Debug.Log("ItemManager: Local player skipped item selection");
            SubmitItemSelectionServerRpc("", -1); // empty string means skip
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitItemSelectionServerRpc(string itemId, int slotToReplace, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // =====================================
            // island selection path (per-player, does not block round)
            // =====================================
            if (_islandPlayerItemChoices.TryGetValue(clientId, out string[] islandChoices))
            {
                // handle skip for island selection
                if (string.IsNullOrEmpty(itemId))
                {
                    // Debug.Log($"ItemManager: Client {clientId} skipped island selection");
                    _islandPlayerItemChoices.Remove(clientId);
                    AcknowledgeSelectionClientRpc(new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { clientId }
                        }
                    });
                    // process any queued island selections
                    ProcessQueuedIslandSelections();
                    // do NOT call CheckAllPlayersSelected() — island selections do not block round progression
                    return;
                }

                // validate island selection
                bool validIslandSelection = false;
                foreach (string choice in islandChoices)
                {
                    if (choice == itemId)
                    {
                        validIslandSelection = true;
                        break;
                    }
                }

                if (!validIslandSelection)
                {
                    Debug.LogWarning($"ItemManager: Client {clientId} selected invalid island item {itemId}");
                    return;
                }

                // apply item and clean up island tracking
                ApplyItemToPlayer(clientId, itemId, slotToReplace);
                _islandPlayerItemChoices.Remove(clientId);

                AcknowledgeSelectionClientRpc(new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { clientId }
                    }
                });
// process any queued island selections
                    ProcessQueuedIslandSelections();
                    // do NOT call CheckAllPlayersSelected() — island selections do not block round progression
                    return;
            }

            // =====================================
            // boss selection path (all players, blocks round)
            // =====================================

            // validate the selection
            if (!_playerItemChoices.TryGetValue(clientId, out string[] validChoices))
            {
                Debug.LogWarning($"ItemManager: Client {clientId} has no valid choices");
                return;
            }

            // handle skip
            if (string.IsNullOrEmpty(itemId))
            {
                // Debug.Log($"ItemManager: Client {clientId} skipped selection");
                _playerSelections[clientId] = true;
                _playerItemChoices.Remove(clientId);
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
            _playerItemChoices.Remove(clientId);

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

                    // check if player already owns this item (tier upgrade)
                    if (playerInventory.HasItem(itemId))
                    {
                        success = playerInventory.UpgradeItemTier(itemId);
                        int newTier = playerInventory.GetItemTier(itemId);
                        // Debug.Log($"ItemManager: Upgraded {item.ItemName} to tier {newTier} for player {clientId}, success: {success}");
                    }
                    else if (slotToReplace >= 0 && slotToReplace < playerInventory.MaxSlots)
                    {
                        // replace existing item
                        success = playerInventory.ReplaceItem(slotToReplace, itemId);
                        // Debug.Log($"ItemManager: Replaced slot {slotToReplace} with {item.ItemName} for player {clientId}, success: {success}");
                    }
                    else
                    {
                        // add to inventory
                        success = playerInventory.AddItem(itemId);
                        // Debug.Log($"ItemManager: Added {item.ItemName} to player {clientId}, success: {success}");
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
                        OnItemApplied?.Invoke(clientId, itemId);
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
            // Debug.Log("ItemManager: Selection acknowledged, waiting for other players...");
            // ui can show "waiting for other players" state
        }

        /// <summary>
        /// Checks if all players have completed their boss item selection. If so, notifies
        /// GameFlowManager to advance the round. Island selections are intentionally ignored.
        /// </summary>
        private void CheckAllPlayersSelected()
        {
            foreach (var kvp in _playerSelections)
            {
                if (!kvp.Value)
                {
                    // Debug.Log($"ItemManager: Still waiting for player {kvp.Key}");
                    return;
                }
            }

            // Debug.Log("ItemManager: All players selected, notifying GameFlowManager");

            // notify GameFlowManager that all selections are complete
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.OnAllItemSelectionsComplete();
            }
        }

        // =====================================
        // disconnect handling (item selection side)
        // =====================================

        /// <summary>
        /// Called by NetworkSessionManager when a player disconnects mid-game.
        /// Cleans up island selection tracking, pending queue entries, and boss selection state.
        /// </summary>
        public void HandlePlayerDisconnected(ulong clientId)
        {
            if (!IsServerAuthority) return;

            // Debug.Log($"ItemManager: handling disconnect for player {clientId}");

            // clean up island selection tracking for the disconnected player
            if (_islandPlayerItemChoices.ContainsKey(clientId))
            {
                _islandPlayerItemChoices.Remove(clientId);
                // Debug.Log($"ItemManager: removed disconnected player {clientId} from island selection tracking");
            }

            // remove from pending island selections queue if present
            // (rebuild queue without the disconnected client)
            if (_pendingIslandSelections.Count > 0)
            {
                var rebuiltQueue = new Queue<ulong>();
                while (_pendingIslandSelections.Count > 0)
                {
                    ulong queuedClientId = _pendingIslandSelections.Dequeue();
                    if (queuedClientId != clientId)
                    {
                        rebuiltQueue.Enqueue(queuedClientId);
                    }
                }
                _pendingIslandSelections = rebuiltQueue;
            }

            // if we're in boss item selection, mark them as selected so we don't wait forever
            if (GameFlowManager.Instance != null &&
                GameFlowManager.Instance.CurrentPhase.Value == GamePhase.PowerUpSelection)
            {
                if (_playerSelections.ContainsKey(clientId))
                {
                    _playerSelections[clientId] = true; // mark as "selected" so we don't wait
                    _playerItemChoices.Remove(clientId);

                    // Debug.Log($"ItemManager: marked disconnected player {clientId} as selected");

                    // check if all remaining players have now selected
                    CheckAllPlayersSelected();
                }
            }
        }
    }
}
