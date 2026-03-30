using UnityEngine;

namespace Category5.Items
{
    // scriptable object defining item properties
    [CreateAssetMenu(fileName = "NewItem", menuName = "Category5/Item Data", order = 1)]
    public class ItemData : ScriptableObject
    {
        // max tier items can reach before they stop appearing
        public const int MaxTier = 5;

        // default tier scaling: effect value * (1 + 0.25 * (tier - 1))
        // T1=100%, T2=125%, T3=150%, T4=175%, T5=200%
        public const float DefaultTierScalePerLevel = 0.25f;

        [Header("basic info")]
        [SerializeField] private string itemName;
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private Sprite icon;

        [Header("classification")]
        [SerializeField] private ItemCategory category = ItemCategory.General;
        
        [Header("effects")]
        [SerializeField] private ItemEffect[] effects; // supports multiple effects per item

        [Header("behaviour")]
        [Tooltip("optional prefab with an ItemBehaviour component for items with unique triggered effects")]
        [SerializeField] private GameObject behaviourPrefab;

        [Header("visuals")]
        [SerializeField] private Color glowColor = Color.white;
        [SerializeField] private GameObject visualEffectPrefab; // optional vfx to spawn on player

        [Header("future systems")]
        [SerializeField] private int goldCost = 0; // for future shop system (MAYBE)

        // public accessors
        public string ItemName => itemName;
        public string Description => description;
        public Sprite Icon => icon;
        public ItemCategory Category => category;
        public ItemEffect[] Effects => effects;
        public GameObject BehaviourPrefab => behaviourPrefab;
        public bool HasBehaviour => behaviourPrefab != null;
        public Color GlowColor => glowColor;
        public GameObject VisualEffectPrefab => visualEffectPrefab;
        public int GoldCost => goldCost;

        // unique identifier for networking (uses asset name)
        public string UniqueId => name;

        // returns the tier-scaled value for a stat effect
        public static float GetTierScaledValue(float baseValue, int tier)
        {
            return baseValue * (1f + DefaultTierScalePerLevel * (tier - 1));
        }

        // formats the description template at a given tier
        // overrideArgs: values from ItemBehaviour.GetFormatValues() — pass null to auto-generate from effects
        // write {0}, {1}, etc. in Description; TMP <color> tags work natively
        // example: "Deals <color=#FFD700>{0:F0}</color> bonus damage" with effects[0].value = 25
        public string FormatDescription(int tier, object[] overrideArgs = null)
        {
            if (string.IsNullOrEmpty(description)) return "";

            // use provided args (from behaviour), otherwise auto-generate from effects array
            object[] args = overrideArgs;
            if (args == null || args.Length == 0)
            {
                if (effects != null && effects.Length > 0)
                {
                    args = new object[effects.Length];
                    for (int i = 0; i < effects.Length; i++)
                    {
                        args[i] = GetTierScaledValue(effects[i].value, tier);
                    }
                }
                else
                {
                    return description; // no placeholders expected, return as-is
                }
            }

            try { return string.Format(description, args); }
            catch { return description; } // fallback: description has no {N} placeholders, safe to ignore
        }
    }

    // serializable struct for item effects (allows multiple effects per item)
    [System.Serializable]
    public struct ItemEffect
    {
        public ItemEffectType effectType;
        public float value;
    }
}
