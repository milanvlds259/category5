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
        [TextArea(3, 5)]
        public string classDescription;
        
        public Sprite classIcon;
        
        [Header("Ability Prefabs")]
        public GameObject ability1Prefab; // Q
        public GameObject ability2Prefab; // E
        public GameObject ability3Prefab; // R
        
        [Header("Combat Settings")]
        public CombatClass combatClass = CombatClass.Ranged;
        
        [Tooltip("projectile data for ranged basic attack (only used if combatClass is Ranged)")]
        public ProjectileData basicAttackProjectile;
        
        [Header("Character Model")]
        [Tooltip("model prefab to instantiate as the player's visual representation for this class")]
        public GameObject modelPrefab;
    }
}
