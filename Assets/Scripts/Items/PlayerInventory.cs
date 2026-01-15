using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;

namespace Category5.Items
{
    // component attached to player to manage inventory and calculate modified stats
    [RequireComponent(typeof(Unity.Netcode.NetworkObject))]
    public class PlayerInventory : NetworkBehaviour
    {
        [Header("inventory settings")]
        [SerializeField] private int maxSlots = 5;

        [Header("base stats (reference only dont touch)")]
        [SerializeField] private int baseMaxHealth = 100;
        [SerializeField] private float baseDodgeCooldown = 2f;
        [SerializeField] private float baseMoveSpeed = 5f;

        // networked inventory slots
        private NetworkList<InventorySlot> inventorySlots;

        // cached calculated stats
        private float _damageMultiplier = 1f;
        private int _flatDamageBonus = 0;
        private int _maxHealthBonus = 0;
        private float _dodgeCooldownReduction = 0f;
        private int _lifestealAmount = 0;
        private float _moveSpeedMultiplier = 1f;
        private float _attackSpeedMultiplier = 1f;
        
        // temporary stat multipliers (for abilities like Fighter R)
        private Dictionary<string, (float multiplier, float remaining)> _temporaryMultipliers = new Dictionary<string, (float, float)>();

        // public stat accessors
        public float DamageMultiplier => _damageMultiplier;
        public int FlatDamageBonus => _flatDamageBonus;
        public int MaxHealthBonus => _maxHealthBonus;
        public int TotalMaxHealth => baseMaxHealth + _maxHealthBonus;
        public float DodgeCooldownReduction => _dodgeCooldownReduction;
        public float EffectiveDodgeCooldown => Mathf.Max(0.5f, baseDodgeCooldown - _dodgeCooldownReduction);
        public int LifestealAmount => _lifestealAmount;
        public float MoveSpeedMultiplier => _moveSpeedMultiplier;
        public float AttackSpeedMultiplier => _attackSpeedMultiplier;
        public float EffectiveMoveSpeed => baseMoveSpeed * _moveSpeedMultiplier;

        // inventory accessors
        public int MaxSlots => maxSlots;
        public int UsedSlots { get; private set; }
        public bool IsFull => UsedSlots >= maxSlots;
        public NetworkList<InventorySlot> InventorySlots => inventorySlots;

        // events
        public event Action OnInventoryChanged;
        public event Action OnStatsChanged;

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
            
            // recalculate stats on spawn
            RecalculateStats();
        }

        public override void OnNetworkDespawn()
        {
            inventorySlots.OnListChanged -= OnInventoryListChanged;
        }

        private void OnInventoryListChanged(NetworkListEvent<InventorySlot> changeEvent)
        {
            UpdateUsedSlots();
            RecalculateStats();
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

        // adds item to first available slot
        // returns true if successful, false if inventory full
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

            // check for duplicates if not allowed
            if (!itemData.AllowDuplicates && HasItem(itemId))
            {
                Debug.LogWarning($"PlayerInventory: Item '{itemId}' does not allow duplicates");
                return false;
            }

            // find first empty slot
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                if (inventorySlots[i].IsEmpty)
                {
                    inventorySlots[i] = new InventorySlot(itemId, i);
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

            // check for duplicates if not allowed (excluding the slot we're replacing)
            if (!itemData.AllowDuplicates)
            {
                for (int i = 0; i < inventorySlots.Count; i++)
                {
                    if (i != slotIndex && !inventorySlots[i].IsEmpty && 
                        inventorySlots[i].itemId.ToString() == newItemId)
                    {
                        Debug.LogWarning($"PlayerInventory: Item '{newItemId}' does not allow duplicates");
                        return false;
                    }
                }
            }

            var oldItemId = inventorySlots[slotIndex].itemId.ToString();
            inventorySlots[slotIndex] = new InventorySlot(newItemId, slotIndex);
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

        // recalculates all stats from inventory
        private void RecalculateStats()
        {
            // reset to base values
            _damageMultiplier = 1f;
            _flatDamageBonus = 0;
            _maxHealthBonus = 0;
            _dodgeCooldownReduction = 0f;
            _lifestealAmount = 0;
            _moveSpeedMultiplier = 1f;
            _attackSpeedMultiplier = 1f;

            // get item registry
            var registry = ItemRegistry.Instance;
            if (registry == null)
            {
                Debug.LogWarning("PlayerInventory: ItemRegistry not found, cannot recalculate stats");
                return;
            }

            // apply each item's effects
            foreach (var slot in inventorySlots)
            {
                if (slot.IsEmpty)
                    continue;

                var item = registry.GetItemById(slot.itemId.ToString());
                if (item == null)
                {
                    Debug.LogWarning($"PlayerInventory: Item '{slot.itemId}' not found in registry");
                    continue;
                }

                ApplyItemEffects(item);
            }

            Debug.Log($"PlayerInventory recalculated for player {OwnerClientId}: DamageMult={_damageMultiplier:F2}, FlatDmg={_flatDamageBonus}, MaxHP+={_maxHealthBonus}, DodgeCD-={_dodgeCooldownReduction:F2}, Lifesteal={_lifestealAmount}, MoveSpeed*={_moveSpeedMultiplier:F2}");

            OnStatsChanged?.Invoke();
        }

        private void ApplyItemEffects(ItemData item)
        {
            foreach (var effect in item.Effects)
            {
                switch (effect.effectType)
                {
                    case ItemEffectType.DamageMultiplier:
                        // additive stacking (e.g., +0.15 + +0.15 = +0.30)
                        _damageMultiplier += effect.value;
                        break;

                    case ItemEffectType.MaxHealthBonus:
                        _maxHealthBonus += Mathf.RoundToInt(effect.value);
                        break;

                    case ItemEffectType.DodgeCooldownReduction:
                        _dodgeCooldownReduction += effect.value;
                        break;

                    case ItemEffectType.FlatDamageBonus:
                        _flatDamageBonus += Mathf.RoundToInt(effect.value);
                        break;

                    case ItemEffectType.Lifesteal:
                        _lifestealAmount += Mathf.RoundToInt(effect.value);
                        break;

                    case ItemEffectType.MoveSpeedMultiplier:
                        _moveSpeedMultiplier += effect.value;
                        break;

                    case ItemEffectType.AttackSpeedMultiplier:
                        _attackSpeedMultiplier += effect.value;
                        break;

                    default:
                        Debug.LogWarning($"PlayerInventory: Unhandled effect type {effect.effectType}");
                        break;
                }
            }
        }

        // helper to get total damage with all modifiers applied
        public int CalculateDamage(int baseDamage)
        {
            float effectiveDamageMult = GetEffectiveDamageMultiplier();
            float totalDamage = baseDamage * effectiveDamageMult;
            totalDamage += _flatDamageBonus;
            return Mathf.RoundToInt(totalDamage);
        }
        
        // apply a temporary stat multiplier (used by abilities like Fighter R)
        public void ApplyTemporaryMultiplier(string statName, float bonusMultiplier, float duration)
        {
            _temporaryMultipliers[statName] = (bonusMultiplier, duration);
        }
        
        private void Update()
        {
            if (!IsSpawned) return;
            
            // update temporary multipliers
            var keys = new List<string>(_temporaryMultipliers.Keys);
            foreach (var key in keys)
            {
                var (multiplier, remaining) = _temporaryMultipliers[key];
                remaining -= Time.deltaTime;
                
                if (remaining <= 0)
                {
                    _temporaryMultipliers.Remove(key);
                }
                else
                {
                    _temporaryMultipliers[key] = (multiplier, remaining);
                }
            }
        }
        
        // get effective damage multiplier including temporary boosts
        public float GetEffectiveDamageMultiplier()
        {
            float effective = _damageMultiplier;
            if (_temporaryMultipliers.TryGetValue("damage", out var boost))
            {
                effective += boost.multiplier;
            }
            return effective;
        }
        
        // get effective speed multiplier including temporary boosts
        public float GetEffectiveSpeedMultiplier()
        {
            float effective = _moveSpeedMultiplier;
            if (_temporaryMultipliers.TryGetValue("speed", out var boost))
            {
                effective += boost.multiplier;
            }
            return effective;
        }
    }
}
