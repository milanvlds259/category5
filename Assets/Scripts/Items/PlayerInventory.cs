using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;

namespace Category5.Items
{
    // component attached to player to manage inventory only
    // PlayerStats reads from this to calculate stat bonuses
    [RequireComponent(typeof(Unity.Netcode.NetworkObject))]
    public class PlayerInventory : NetworkBehaviour
    {
        [Header("inventory settings")]
        [SerializeField] private int maxSlots = 5;

        // networked inventory slots
        private NetworkList<InventorySlot> inventorySlots;

        // inventory accessors
        public int MaxSlots => maxSlots;
        public int UsedSlots { get; private set; }
        public bool IsFull => UsedSlots >= maxSlots;
        public NetworkList<InventorySlot> InventorySlots => inventorySlots;

        // event fired when inventory changes (PlayerStats listens to this)
        public event Action OnInventoryChanged;

        private void Awake()
        {
            inventorySlots = new NetworkList<InventorySlot>();
        }

        public override void OnNetworkSpawn()
        {
            // initialize slots if server
            if (IsServer)
            {
                for (int i = 0; i < maxSlots; i++)
                {
                    inventorySlots.Add(InventorySlot.Empty(i));
                }
            }

            // subscribe to list changes
            inventorySlots.OnListChanged += OnInventoryListChanged;
        }

        public override void OnNetworkDespawn()
        {
            inventorySlots.OnListChanged -= OnInventoryListChanged;
        }

        private void OnInventoryListChanged(NetworkListEvent<InventorySlot> changeEvent)
        {
            UpdateUsedSlots();
            OnInventoryChanged?.Invoke();
        }

        private void UpdateUsedSlots()
        {
            UsedSlots = 0;
            foreach (var slot in inventorySlots)
            {
                if (!slot.IsEmpty)
                {
                    UsedSlots++;
                }
            }
        }

        // adds item to first available slot, or upgrades tier if already owned
        // returns true if successful, false if inventory full or item at max tier
        public bool AddItem(string itemId)
        {
            if (!IsServer)
            {
                Debug.LogWarning("PlayerInventory.AddItem should only be called on server");
                return false;
            }

            // check if item exists
            var itemData = ItemRegistry.Instance?.GetItemById(itemId);
            if (itemData == null)
            {
                Debug.LogWarning($"PlayerInventory: Item '{itemId}' not found in registry");
                return false;
            }

            // check if player already has this item — upgrade tier instead
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                if (!inventorySlots[i].IsEmpty && inventorySlots[i].itemId.ToString() == itemId)
                {
                    return UpgradeItemTier(itemId);
                }
            }

            // find first empty slot
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                if (inventorySlots[i].IsEmpty)
                {
                    inventorySlots[i] = new InventorySlot(itemId, i, 1);
                    Debug.Log($"PlayerInventory: Added item '{itemId}' to slot {i} for player {OwnerClientId}");
                    return true;
                }
            }

            Debug.LogWarning($"PlayerInventory: No empty slots available for player {OwnerClientId}");
            return false;
        }

        // replaces item at specific slot
        public bool ReplaceItem(int slotIndex, string newItemId)
        {
            if (!IsServer)
            {
                Debug.LogWarning("PlayerInventory.ReplaceItem should only be called on server");
                return false;
            }

            if (slotIndex < 0 || slotIndex >= inventorySlots.Count)
            {
                Debug.LogWarning($"PlayerInventory: Invalid slot index {slotIndex}");
                return false;
            }

            // check if new item exists
            var itemData = ItemRegistry.Instance?.GetItemById(newItemId);
            if (itemData == null)
            {
                Debug.LogWarning($"PlayerInventory: Item '{newItemId}' not found in registry");
                return false;
            }

            var oldItemId = inventorySlots[slotIndex].itemId.ToString();
            inventorySlots[slotIndex] = new InventorySlot(newItemId, slotIndex, 1);
            Debug.Log($"PlayerInventory: Replaced item '{oldItemId}' with '{newItemId}' at slot {slotIndex} for player {OwnerClientId}");
            return true;
        }

        // removes item from specific slot
        public bool RemoveItem(int slotIndex)
        {
            if (!IsServer)
            {
                Debug.LogWarning("PlayerInventory.RemoveItem should only be called on server");
                return false;
            }

            if (slotIndex < 0 || slotIndex >= inventorySlots.Count)
            {
                Debug.LogWarning($"PlayerInventory: Invalid slot index {slotIndex}");
                return false;
            }

            if (inventorySlots[slotIndex].IsEmpty)
            {
                Debug.LogWarning($"PlayerInventory: Slot {slotIndex} is already empty");
                return false;
            }

            var itemId = inventorySlots[slotIndex].itemId.ToString();
            inventorySlots[slotIndex] = InventorySlot.Empty(slotIndex);
            Debug.Log($"PlayerInventory: Removed item '{itemId}' from slot {slotIndex} for player {OwnerClientId}");
            return true;
        }

        // checks if player has specific item
        public bool HasItem(string itemId)
        {
            foreach (var slot in inventorySlots)
            {
                if (!slot.IsEmpty && slot.itemId.ToString() == itemId)
                {
                    return true;
                }
            }
            return false;
        }

        // gets count of specific item (for stacking)
        public int GetItemCount(string itemId)
        {
            int count = 0;
            foreach (var slot in inventorySlots)
            {
                if (!slot.IsEmpty && slot.itemId.ToString() == itemId)
                {
                    count++;
                }
            }
            return count;
        }

        // gets item at specific slot
        public ItemData GetItemInSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= inventorySlots.Count)
            {
                return null;
            }

            var slot = inventorySlots[slotIndex];
            if (slot.IsEmpty)
            {
                return null;
            }

            return ItemRegistry.Instance?.GetItemById(slot.itemId.ToString());
        }

        // gets tier for a specific slot (0 if empty or invalid)
        public int GetSlotTier(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= inventorySlots.Count)
            {
                return 0;
            }

            var slot = inventorySlots[slotIndex];
            return slot.IsEmpty ? 0 : slot.tier;
        }

        // clears entire inventory (for testing or death penalties)
        public void ClearInventory()
        {
            if (!IsServer)
            {
                Debug.LogWarning("PlayerInventory.ClearInventory should only be called on server");
                return;
            }

            for (int i = 0; i < inventorySlots.Count; i++)
            {
                inventorySlots[i] = InventorySlot.Empty(i);
            }

            Debug.Log($"PlayerInventory: Cleared inventory for player {OwnerClientId}");
        }

        // helper method to get all items as list (for PlayerStats to read)
        public List<ItemData> GetAllItems()
        {
            var items = new List<ItemData>();
            var registry = ItemRegistry.Instance;
            if (registry == null) return items;

            foreach (var slot in inventorySlots)
            {
                if (slot.IsEmpty) continue;
                
                var item = registry.GetItemById(slot.itemId.ToString());
                if (item != null)
                {
                    items.Add(item);
                }
            }
            
            return items;
        }

        // returns all items with their current tier (for tier-aware stat calculation)
        public List<(ItemData item, int tier)> GetAllItemsWithTier()
        {
            var items = new List<(ItemData, int)>();
            var registry = ItemRegistry.Instance;
            if (registry == null) return items;

            foreach (var slot in inventorySlots)
            {
                if (slot.IsEmpty) continue;
                
                var item = registry.GetItemById(slot.itemId.ToString());
                if (item != null)
                {
                    items.Add((item, slot.tier));
                }
            }
            
            return items;
        }

        // returns the current tier of an item (0 if not owned)
        public int GetItemTier(string itemId)
        {
            foreach (var slot in inventorySlots)
            {
                if (!slot.IsEmpty && slot.itemId.ToString() == itemId)
                {
                    return slot.tier;
                }
            }
            return 0;
        }

        // upgrades the tier of an item already in inventory (server only)
        public bool UpgradeItemTier(string itemId)
        {
            if (!IsServer)
            {
                Debug.LogWarning("PlayerInventory.UpgradeItemTier should only be called on server");
                return false;
            }

            for (int i = 0; i < inventorySlots.Count; i++)
            {
                if (!inventorySlots[i].IsEmpty && inventorySlots[i].itemId.ToString() == itemId)
                {
                    int currentTier = inventorySlots[i].tier;
                    if (currentTier >= ItemData.MaxTier)
                    {
                        Debug.LogWarning($"PlayerInventory: Item '{itemId}' already at max tier {ItemData.MaxTier}");
                        return false;
                    }

                    int newTier = currentTier + 1;
                    inventorySlots[i] = new InventorySlot(itemId, i, newTier);
                    Debug.Log($"PlayerInventory: Upgraded '{itemId}' to tier {newTier} for player {OwnerClientId}");
                    return true;
                }
            }

            Debug.LogWarning($"PlayerInventory: Item '{itemId}' not found in inventory for upgrade");
            return false;
        }

        // checks if an item is at max tier
        public bool IsItemMaxTier(string itemId)
        {
            foreach (var slot in inventorySlots)
            {
                if (!slot.IsEmpty && slot.itemId.ToString() == itemId)
                {
                    return slot.tier >= ItemData.MaxTier;
                }
            }
            return false;
        }
    }
}
