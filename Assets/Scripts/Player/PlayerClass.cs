using UnityEngine;

namespace Category5.Player
{
    // enum defining all player classes
    public enum PlayerClassType
    {
        Fighter,
        Ranger,
        Elementalist,
        Assassin,
        Enchanter
    }
    
    // scriptable object defining a player class and its abilities
    [CreateAssetMenu(fileName = "New Player Class", menuName = "Category5/Player Class")]
    public class PlayerClass : ScriptableObject
    {
        public PlayerClassType classType;
        public string className;
        public string characterName;
        [TextArea(3, 5)]
        public string classDescription;
        
        public Sprite classIcon; // full body art - used in character view panel
        public Sprite classPortrait; // cropped portrait - used in character select carousel
        public Sprite classPartyPortrait; // smaller portrait for party panel 

        [Header("Ability Prefabs")]
        public GameObject ability1Prefab; // Q
        public GameObject ability2Prefab; // E
        public GameObject ability3Prefab; // R
        
        [Header("Combat Settings")]
        public CombatClass combatClass = CombatClass.Ranged;
        
        [Tooltip("projectile data for ranged basic attack (only used if combatClass is Ranged)")]
        public ProjectileData basicAttackProjectile;
        
        [Header("Base Stats")]
        [Tooltip("base attack damage that abilities and basic attacks scale from")]
        public float baseAttackDamage = 12f;
        
        [Tooltip("starting and max health for this class")]
        public int baseMaxHealth = 100;
        
        [Tooltip("starting and max mana for this class")]
        public int baseMaxMana = 80;
        
        [Tooltip("base movement speed in units per second")]
        public float baseMoveSpeed = 7f;
        
        [Tooltip("base attack speed multiplier (1.0 = normal)")]
        public float baseAttackSpeed = 1f;
        
        [Tooltip("base dodge cooldown in seconds")]
        public float baseDodgeCooldown = 2f;
        
        [Tooltip("base mana regeneration per second")]
        public float baseManaRegenRate = 2f;
        
        [Tooltip("base armor value for damage reduction (lol-style formula)")]
        public float baseArmor = 10f;
        
        [Tooltip("base critical hit chance (0-1, e.g. 0.05 = 5%)")]
        [Range(0f, 1f)]
        public float baseCritChance = 0.05f;
        
        [Tooltip("base critical hit damage multiplier (e.g. 1.5 = 150% damage on crit)")]
        public float baseCritDamage = 1.5f;
        
        [Header("Melee Coefficients")]
        [Tooltip("damage coefficient for light melee attacks (fraction of attack damage)")]
        public float lightAttackCoefficient = 0.8f;
        
        [Tooltip("damage coefficient for heavy melee combo finisher (fraction of attack damage)")]
        public float heavyAttackCoefficient = 1.5f;
        
        [Header("Character Model")]
        [Tooltip("model prefab to instantiate as the player's visual representation for this class")]
        public GameObject modelPrefab;
    }
}
