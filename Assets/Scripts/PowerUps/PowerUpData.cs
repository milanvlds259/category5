using UnityEngine;

namespace Category5.PowerUps
{
    // defines the type of effect a power-up provides
    public enum PowerUpEffectType
    {
        DamageMultiplier,   // berserker rage: +20% damage dealt
        MaxHealthBonus,     // stone skin: +30 max hp
        DodgeCooldownReduction, // lightning step: -1s dodge cooldown
        FlatDamageBonus,    // giant slayer: +15 flat damage to boss
        Lifesteal           // vampire touch: heal 5 hp per hit
    }

    // scriptable object defining power-up properties
    [CreateAssetMenu(fileName = "NewPowerUp", menuName = "Category5/Power-Up Data", order = 1)]
    public class PowerUpData : ScriptableObject
    {
        [Header("basic info")]
        [SerializeField] private string powerUpName;
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private Sprite icon;

        [Header("effect")]
        [SerializeField] private PowerUpEffectType effectType;
        [SerializeField] private float effectValue;
        
        [Header("visuals")]
        [SerializeField] private Color glowColor = Color.white;
        [SerializeField] private GameObject visualEffectPrefab; // optional vfx to spawn on player

        // public accessors
        public string PowerUpName => powerUpName;
        public string Description => description;
        public Sprite Icon => icon;
        public PowerUpEffectType EffectType => effectType;
        public float EffectValue => effectValue;
        public Color GlowColor => glowColor;
        public GameObject VisualEffectPrefab => visualEffectPrefab;

        // unique identifier for networking
        // uses the asset name as id since it's unique in the project
        public string UniqueId => name;
    }
}
