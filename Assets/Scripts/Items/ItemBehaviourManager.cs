using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Category5.Player;

namespace Category5.Items
{
    // manages item behaviour lifecycle on the player
    // instantiates/upgrades/removes ItemBehaviour components when inventory changes
    [RequireComponent(typeof(PlayerInventory))]
    [RequireComponent(typeof(PlayerStats))]
    public class ItemBehaviourManager : NetworkBehaviour
    {
        // cached player component references for behaviours to access
        public PlayerController PlayerController { get; private set; }
        public PlayerStats PlayerStats { get; private set; }
        public PlayerInventory PlayerInventory { get; private set; }
        public PlayerCombat PlayerCombat { get; private set; }
        public PlayerAbilityManager AbilityManager { get; private set; }

        // active behaviours keyed by item id
        private Dictionary<string, ItemBehaviour> _activeBehaviours = new Dictionary<string, ItemBehaviour>();

        private void Awake()
        {
            PlayerController = GetComponent<PlayerController>();
            PlayerStats = GetComponent<PlayerStats>();
            PlayerInventory = GetComponent<PlayerInventory>();
            PlayerCombat = GetComponent<PlayerCombat>();
            AbilityManager = GetComponent<PlayerAbilityManager>();
        }

        public override void OnNetworkSpawn()
        {
            if (PlayerInventory != null)
            {
                PlayerInventory.OnInventoryChanged += OnInventoryChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (PlayerInventory != null)
            {
                PlayerInventory.OnInventoryChanged -= OnInventoryChanged;
            }

            // cleanup all active behaviours
            RemoveAllBehaviours();
        }

        private void OnInventoryChanged()
        {
            if (!IsServer) return;
            SyncBehaviours();
        }

        // compares active behaviours against current inventory and adds/upgrades/removes as needed
        private void SyncBehaviours()
        {
            var registry = ItemRegistry.Instance;
            if (registry == null) return;

            // build set of current inventory items with their tiers
            var currentItems = new Dictionary<string, int>();
            foreach (var slot in PlayerInventory.InventorySlots)
            {
                if (slot.IsEmpty) continue;
                string id = slot.itemId.ToString();
                currentItems[id] = slot.tier;
            }

            // remove behaviours for items no longer in inventory
            var toRemove = new List<string>();
            foreach (var kvp in _activeBehaviours)
            {
                if (!currentItems.ContainsKey(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var id in toRemove)
            {
                RemoveBehaviour(id);
            }

            // add or upgrade behaviours for current items
            foreach (var kvp in currentItems)
            {
                string itemId = kvp.Key;
                int tier = kvp.Value;

                var itemData = registry.GetItemById(itemId);
                if (itemData == null || itemData.BehaviourPrefab == null) continue;

                if (_activeBehaviours.TryGetValue(itemId, out var existing))
                {
                    // item already has a behaviour, check if tier changed
                    if (existing.CurrentTier != tier)
                    {
                        existing.SetTier(tier);
                        UpgradeBehaviourClientRpc(itemId, tier);
                        // Debug.Log($"ItemBehaviourManager: upgraded {itemId} to tier {tier}");
                    }
                }
                else
                {
                    // new item with a behaviour, instantiate it
                    AddBehaviour(itemId, itemData, tier);
                }
            }
        }

        private void AddBehaviour(string itemId, ItemData itemData, int tier)
        {
            var prefab = itemData.BehaviourPrefab;
            var behaviourComponent = prefab.GetComponent<ItemBehaviour>();
            if (behaviourComponent == null)
            {
                Debug.LogError($"ItemBehaviourManager: behaviour prefab for '{itemId}' has no ItemBehaviour component");
                return;
            }

            var instance = Instantiate(prefab, transform);
            var behaviour = instance.GetComponent<ItemBehaviour>();
            behaviour.Initialize(this, tier);
            _activeBehaviours[itemId] = behaviour;

            // sync to clients so they can run client-side visuals
            AddBehaviourClientRpc(itemId, tier);

            // Debug.Log($"ItemBehaviourManager: added behaviour for {itemId} at tier {tier}");
        }

        private void RemoveBehaviour(string itemId)
        {
            if (_activeBehaviours.TryGetValue(itemId, out var behaviour))
            {
                behaviour.OnRemoved();
                Destroy(behaviour.gameObject);
                _activeBehaviours.Remove(itemId);

                // sync removal to clients
                RemoveBehaviourClientRpc(itemId);

                // Debug.Log($"ItemBehaviourManager: removed behaviour for {itemId}");
            }
        }

        private void RemoveAllBehaviours()
        {
            foreach (var kvp in _activeBehaviours)
            {
                kvp.Value.OnRemoved();
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            _activeBehaviours.Clear();
        }

        // client-side behaviour sync for visual effects
        [ClientRpc]
        private void AddBehaviourClientRpc(string itemId, int tier)
        {
            if (IsServer) return; // server already has it

            var registry = ItemRegistry.Instance;
            if (registry == null) return;

            var itemData = registry.GetItemById(itemId);
            if (itemData == null || itemData.BehaviourPrefab == null) return;

            // check if already exists on client (edge case)
            if (_activeBehaviours.ContainsKey(itemId)) return;

            var instance = Instantiate(itemData.BehaviourPrefab, transform);
            var behaviour = instance.GetComponent<ItemBehaviour>();
            if (behaviour != null)
            {
                behaviour.Initialize(this, tier);
                _activeBehaviours[itemId] = behaviour;
            }
        }

        [ClientRpc]
        private void RemoveBehaviourClientRpc(string itemId)
        {
            if (IsServer) return; // server already handled it

            if (_activeBehaviours.TryGetValue(itemId, out var behaviour))
            {
                behaviour.OnRemoved();
                Destroy(behaviour.gameObject);
                _activeBehaviours.Remove(itemId);
            }
        }

        [ClientRpc]
        private void UpgradeBehaviourClientRpc(string itemId, int newTier)
        {
            if (IsServer) return;

            if (_activeBehaviours.TryGetValue(itemId, out var behaviour))
            {
                behaviour.SetTier(newTier);
            }
        }

        // public api for item behaviours to remove themselves (e.g. Backup Plan consumed)
        public void RemoveItemById(string itemId)
        {
            if (!IsServer) return;

            // find and clear the inventory slot
            for (int i = 0; i < PlayerInventory.InventorySlots.Count; i++)
            {
                var slot = PlayerInventory.InventorySlots[i];
                if (!slot.IsEmpty && slot.itemId.ToString() == itemId)
                {
                    PlayerInventory.RemoveItem(i);
                    break;
                }
            }
            // behaviour removal happens automatically via OnInventoryChanged -> SyncBehaviours
        }

        // check if a specific behaviour is active (for cross-item interactions)
        public T GetItemBehaviour<T>() where T : ItemBehaviour
        {
            foreach (var kvp in _activeBehaviours)
            {
                if (kvp.Value is T typed) return typed;
            }
            return null;
        }

        // check if a specific item behaviour is active by item id
        public bool HasBehaviour(string itemId)
        {
            return _activeBehaviours.ContainsKey(itemId);
        }

        // called by the owner when a body contact fires during sprint/dodge
        // relays to the server-side ForcefulImpactBehaviour to apply damage
        [Rpc(SendTo.Server)]
        public void ForcefulImpactContactServerRpc(ulong hitNetworkObjectId)
        {
            var forceful = GetItemBehaviour<ForcefulImpactBehaviour>();
            if (forceful == null) return;

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(hitNetworkObjectId, out var netObj))
                forceful.OnServerBodyContact(netObj.gameObject);
        }
    }
}
