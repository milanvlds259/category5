using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using Category5.Player;
using Category5.Audio;
using Category5.Core;

namespace Category5.Items
{
    // manages item selection flow - generating choices, sending to clients, applying to inventory
    public class ItemManager : NetworkBehaviour
    {
        public static ItemManager Instance { get; private set; }

        [Header("item settings")]
        [SerializeField] private int itemChoicesPerPlayer = 3;

        // track which players have made their selection (server only)
        private Dictionary<ulong, bool> _playerSelections = new Dictionary<ulong, bool>();
        private Dictionary<ulong, string[]> _playerItemChoices = new Dictionary<ulong, string[]>(); // item ids

        private bool IsServerAuthority => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        // events for ui
        public event System.Action<string[]> OnShowItemSelection; // item ids to show
        public event System.Action OnHideItemSelection;

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

        // called by GameFlowManager on server to hide selection ui on all clients
        public void NotifyRoundStartedAndHideSelection(int round)
        {
            if (!IsServerAuthority) return;
            NotifyRoundStartedAndHideSelectionClientRpc(round);
        }

        [ClientRpc]
        private void NotifyRoundStartedAndHideSelectionClientRpc(int round)
        {
            // fire audio event for round start
            GameEvents.InvokeRoundStart(round);
            OnHideItemSelection?.Invoke();
        }

        public void StartItemSelection()
        {
            Debug.Log("ItemManager: starting item selection phase");

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

                    // check if player already owns this item (tier upgrade)
                    if (playerInventory.HasItem(itemId))
                    {
                        success = playerInventory.UpgradeItemTier(itemId);
                        int newTier = playerInventory.GetItemTier(itemId);
                        Debug.Log($"ItemManager: Upgraded {item.ItemName} to tier {newTier} for player {clientId}, success: {success}");
                    }
                    else if (slotToReplace >= 0)
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

            Debug.Log("ItemManager: All players selected, notifying GameFlowManager");

            // notify GameFlowManager that all selections are complete
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.OnAllItemSelectionsComplete();
            }
        }

        // =====================================
        // disconnect handling (item selection side)
        // =====================================

        // called by NetworkSessionManager when a player disconnects mid-game
        public void HandlePlayerDisconnected(ulong clientId)
        {
            if (!IsServerAuthority) return;

            Debug.Log($"ItemManager: handling disconnect for player {clientId}");

            // if we're in item selection, mark them as selected so we don't wait forever
            if (GameFlowManager.Instance != null &&
                GameFlowManager.Instance.CurrentPhase.Value == GamePhase.PowerUpSelection)
            {
                if (_playerSelections.ContainsKey(clientId))
                {
                    _playerSelections[clientId] = true; // mark as "selected" so we don't wait
                    _playerItemChoices.Remove(clientId);

                    Debug.Log($"ItemManager: marked disconnected player {clientId} as selected");

                    // check if all remaining players have now selected
                    CheckAllPlayersSelected();
                }
            }
        }
    }
}
