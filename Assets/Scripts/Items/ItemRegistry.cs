using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Category5.Items
{
    // singleton registry that holds all available items
    // place this in the scene on a persistent gameobject
    public class ItemRegistry : MonoBehaviour
    {
        public static ItemRegistry Instance { get; private set; }

        [Header("available items")]
        [SerializeField] private List<ItemData> allItems = new List<ItemData>();

        [Header("item pool settings")]
        [SerializeField] private bool useWeightedSelection = false; // future feature
        [SerializeField] private bool allowDuplicateChoices = false; // can same item appear twice in selection?

        private Dictionary<string, ItemData> _itemLookup;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                BuildLookup();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void BuildLookup()
        {
            _itemLookup = new Dictionary<string, ItemData>();
            foreach (var item in allItems)
            {
                if (item == null) continue;
                
                if (_itemLookup.ContainsKey(item.UniqueId))
                {
                    Debug.LogWarning($"ItemRegistry: Duplicate item id '{item.UniqueId}'");
                    continue;
                }
                _itemLookup[item.UniqueId] = item;
            }
            Debug.Log($"ItemRegistry: Loaded {_itemLookup.Count} items");
        }

        // get item by its unique id
        public ItemData GetItemById(string id)
        {
            if (_itemLookup == null) BuildLookup();
            
            _itemLookup.TryGetValue(id, out var item);
            return item;
        }

        // get item by index
        public ItemData GetItemByIndex(int index)
        {
            if (index < 0 || index >= allItems.Count) return null;
            return allItems[index];
        }

        // get total count
        public int ItemCount => allItems.Count;

        // get all items
        public IReadOnlyList<ItemData> AllItems => allItems;

        // get random items for selection
        // excludes items player already has if duplicates not allowed
        // excludes duplicate choices if configured
        public List<ItemData> GetRandomItems(int count, PlayerInventory playerInventory = null)
        {
            var result = new List<ItemData>();
            var available = new List<ItemData>(allItems);

            // filter out items the player already has at max tier
            if (playerInventory != null)
            {
                available = available.Where(item => 
                    !playerInventory.IsItemMaxTier(item.UniqueId)
                ).ToList();
            }

            if (available.Count == 0)
            {
                Debug.LogWarning("ItemRegistry: No available items for selection");
                return result;
            }

            // shuffle and take first N
            for (int i = 0; i < count && available.Count > 0; i++)
            {
                int randomIndex = Random.Range(0, available.Count);
                result.Add(available[randomIndex]);
                
                // remove from pool if duplicate choices not allowed
                if (!allowDuplicateChoices)
                {
                    available.RemoveAt(randomIndex);
                }
            }

            return result;
        }

        // get items by category (for future gear system)
        public List<ItemData> GetItemsByCategory(ItemCategory category)
        {
            return allItems.Where(item => item != null && item.Category == category).ToList();
        }



        // weighted random selection (future feature)
        public List<ItemData> GetWeightedRandomItems(int count, PlayerInventory playerInventory = null)
        {
            // todo: implement weighted selection based on tier/rarity
            // for now, falls back to uniform random
            return GetRandomItems(count, playerInventory);
        }

        // validation helper
        public bool ValidateRegistry()
        {
            if (allItems.Count == 0)
            {
                Debug.LogError("ItemRegistry: No items assigned!");
                return false;
            }

            var duplicateIds = allItems
                .Where(item => item != null)
                .GroupBy(item => item.UniqueId)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (var id in duplicateIds)
            {
                Debug.LogError($"ItemRegistry: Duplicate item ID found: {id}");
            }

            return !duplicateIds.Any();
        }

        private void OnValidate()
        {
            // rebuild lookup in editor when list changes
            if (_itemLookup != null)
            {
                BuildLookup();
            }
        }
    }
}
