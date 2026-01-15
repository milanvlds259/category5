using UnityEngine;

namespace Category5.Items
{
    // scriptable object defining item properties
    [CreateAssetMenu(fileName = "NewItem", menuName = "Category5/Item Data", order = 1)]
    public class ItemData : ScriptableObject
    {
        [Header("basic info")]
        [SerializeField] private string itemName;
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private Sprite icon;

        [Header("classification")]
        [SerializeField] private ItemCategory category = ItemCategory.General;
        [SerializeField] private bool allowDuplicates = true; // can player have multiple copies? (should mostly be yes)
        
        [Header("effects")]
        [SerializeField] private ItemEffect[] effects; // supports multiple effects per item

        [Header("visuals")]
        [SerializeField] private Color glowColor = Color.white;
        [SerializeField] private GameObject visualEffectPrefab; // optional vfx to spawn on player

        [Header("future systems")]
        [SerializeField] private int goldCost = 0; // for future shop system (MAYBE)
        [SerializeField] private int tier = 1; // item tier/rarity (1=common, 2=rare, 3=epic, etc)

        // public accessors
        public string ItemName => itemName;
        public string Description => description;
        public Sprite Icon => icon;
        public ItemCategory Category => category;
        public bool AllowDuplicates => allowDuplicates;
        public ItemEffect[] Effects => effects;
        public Color GlowColor => glowColor;
        public GameObject VisualEffectPrefab => visualEffectPrefab;
        public int GoldCost => goldCost;
        public int Tier => tier;

        // unique identifier for networking (uses asset name)
        public string UniqueId => name;
    }

    // serializable struct for item effects (allows multiple effects per item)
    [System.Serializable]
    public struct ItemEffect
    {
        public ItemEffectType effectType;
        public float value;
    }
}
