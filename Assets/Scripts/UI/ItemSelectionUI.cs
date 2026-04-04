using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Category5.Items;

namespace Category5.UI
{
    // manages the item selection ui screen with inventory display
    public class ItemSelectionUI : MonoBehaviour
    {
        [Header("main panel")]
        [SerializeField] private GameObject selectionPanel;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("item cards (3 choices)")]
        [SerializeField] private ItemCard[] itemCards = new ItemCard[3];

        [Header("current inventory display (5 slots)")]
        [SerializeField] private ItemSlotUI[] inventorySlots = new ItemSlotUI[5];
        [SerializeField] private GameObject inventoryContainer;

        [Header("replacement mode")]
        [SerializeField] private Button skipButton;

        private bool _hasSelected = false;
        private string[] _currentChoices;
        private bool _inventoryFull = false;
        private string _pendingItemId; // item waiting to be placed
        private bool _isSubscribed = false;

        private void Start()
        {
            TrySubscribeToEvents();

            // hide panel initially
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(false);
            }

            // setup skip button
            if (skipButton != null)
            {
                skipButton.onClick.AddListener(OnSkipClicked);
            }

            // setup inventory slot click handlers for replacement mode
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                int slotIndex = i;
                if (inventorySlots[i] != null)
                {
                    inventorySlots[i].OnSlotClicked += () => OnInventorySlotClicked(slotIndex);
                }
            }
        }

        private void Update()
        {
            // keep trying to subscribe if we haven't yet
            if (!_isSubscribed)
            {
                TrySubscribeToEvents();
            }
        }

        private void TrySubscribeToEvents()
        {
            if (_isSubscribed) return;

            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.OnShowItemSelection += ShowSelection;
                ItemManager.Instance.OnHideItemSelection += HideSelection;
                _isSubscribed = true;
            }
            if (Category5.Core.GameFlowManager.Instance != null)
            {
                Category5.Core.GameFlowManager.Instance.OnGameOver += OnGameOver;
            }
        }

        private void OnDestroy()
        {
            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.OnShowItemSelection -= ShowSelection;
                ItemManager.Instance.OnHideItemSelection -= HideSelection;
            }
            if (Category5.Core.GameFlowManager.Instance != null)
            {
                Category5.Core.GameFlowManager.Instance.OnGameOver -= OnGameOver;
            }

            if (skipButton != null)
            {
                skipButton.onClick.RemoveListener(OnSkipClicked);
            }
        }

        private void ShowSelection(string[] itemIds)
        {
            if (selectionPanel == null) return;

            _currentChoices = itemIds;
            _hasSelected = false;
            _pendingItemId = null;

            // get player inventory to check if full and display current items
            var playerInventory = GetLocalPlayerInventory();
            _inventoryFull = playerInventory != null && playerInventory.IsFull;

            // show panel
            selectionPanel.SetActive(true);

            // unlock cursor for selection
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // update status text
            if (statusText != null)
            {
                statusText.text = _inventoryFull ? "Choose an Item (or skip)" : "Choose an Item!";
            }

            // show/hide skip button based on inventory full state
            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(_inventoryFull);
            }

            // setup item cards
            var registry = ItemRegistry.Instance;
            if (registry == null)
            {
                Debug.LogError("ItemSelectionUI: ItemRegistry not found");
                return;
            }

            for (int i = 0; i < itemCards.Length; i++)
            {
                if (itemCards[i] == null) continue;

                if (i < itemIds.Length)
                {
                    var item = registry.GetItemById(itemIds[i]);
                    // check if player already owns this item (show as upgrade)
                    int currentTier = 0;
                    if (playerInventory != null && playerInventory.HasItem(itemIds[i]))
                    {
                        currentTier = playerInventory.GetItemTier(itemIds[i]);
                    }
                    itemCards[i].Initialize(item, this, currentTier);
                    itemCards[i].SetInteractable(true);
                }
                else
                {
                    itemCards[i].gameObject.SetActive(false);
                }
            }

            // update inventory display
            UpdateInventoryDisplay(playerInventory);
        }

        private void UpdateInventoryDisplay(PlayerInventory playerInventory)
        {
            if (inventoryContainer == null) return;

            var registry = ItemRegistry.Instance;
            if (registry == null || playerInventory == null) return;

            // show current inventory with tier info
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                if (inventorySlots[i] == null) continue;

                var item = playerInventory.GetItemInSlot(i);
                if (item != null)
                {
                    inventorySlots[i].SetItem(item, playerInventory.GetSlotTier(i));
                }
                else
                {
                    inventorySlots[i].SetEmpty();
                }
            }
        }

        private void HideSelection()
        {
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(false);
            }

            // re-lock cursor for gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _hasSelected = false;
            _pendingItemId = null;
        }

        // called when player clicks an item card
        public void OnItemCardClicked(ItemData item)
        {
            if (_hasSelected) return;

            // Debug.Log($"ItemSelectionUI: Selected item {item.ItemName}");

            // check if player already owns this item (tier upgrade, no replacement needed)
            var playerInventory = GetLocalPlayerInventory();
            bool isUpgrade = playerInventory != null && playerInventory.HasItem(item.UniqueId);

            if (isUpgrade)
            {
                // upgrade existing item directly, no slot selection needed
                _hasSelected = true;
                DisableAllCards();
                ShowWaitingState();
                ItemManager.Instance?.SelectItem(item.UniqueId);
            }
            else if (_inventoryFull)
            {
                // enter replacement mode - highlight inventory slots as clickable
                _pendingItemId = item.UniqueId;
                
                // update status to prompt replacement
                if (statusText != null)
                {
                    statusText.text = $"Click a slot to replace with {item.ItemName} (or skip)";
                }
                
                // make inventory slots clickable
                for (int i = 0; i < inventorySlots.Length; i++)
                {
                    if (inventorySlots[i] != null)
                    {
                        inventorySlots[i].SetHighlighted(true);
                    }
                }
            }
            else
            {
                // add directly to inventory
                _hasSelected = true;
                DisableAllCards();
                ShowWaitingState();
                ItemManager.Instance?.SelectItem(item.UniqueId);
            }
        }

        // called when player clicks an inventory slot during replacement mode
        public void OnInventorySlotClicked(int slotIndex)
        {
            if (_hasSelected || string.IsNullOrEmpty(_pendingItemId)) return;

            // Debug.Log($"ItemSelectionUI: Replacing slot {slotIndex} with {_pendingItemId}");

            _hasSelected = true;
            DisableAllCards();
            
            // unhighlight all slots
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                if (inventorySlots[i] != null)
                {
                    inventorySlots[i].SetHighlighted(false);
                }
            }
            
            ShowWaitingState();
            ItemManager.Instance?.SelectItemWithReplacement(_pendingItemId, slotIndex);
        }

        private void OnSkipClicked()
        {
            if (_hasSelected) return;

            // Debug.Log("ItemSelectionUI: Skipped item selection");

            _hasSelected = true;
            DisableAllCards();
            ShowWaitingState();
            ItemManager.Instance?.SkipSelection();
        }

        private void DisableAllCards()
        {
            foreach (var card in itemCards)
            {
                if (card != null)
                {
                    card.SetInteractable(false);
                }
            }
        }

        private void ShowWaitingState()
        {
            // update status text
            if (statusText != null)
            {
                statusText.text = "Waiting for other players...";
            }
        }

        private void OnGameOver()
        {
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(false);
            }
            _hasSelected = false;
        }

        // helper to get local player's inventory
        private PlayerInventory GetLocalPlayerInventory()
        {
            // iterate all players to find the one owned by this client
            var allPlayers = FindObjectsByType<Category5.Player.PlayerController>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (player.IsOwner)
                {
                    return player.GetComponent<PlayerInventory>();
                }
            }
            return null;
        }
    }
}
