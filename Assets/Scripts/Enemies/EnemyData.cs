using UnityEngine;
using Category5.Core;

namespace Category5.Enemies
{
    // scriptable object for defining enemy properties
    // designers can create new enemy types in the editor without touching code
    [CreateAssetMenu(fileName = "New Enemy Data", menuName = "Category5/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("identity")]
        [Tooltip("display name for ui and debugging")]
        public string enemyName = "New Enemy";
        
        [Tooltip("elemental type of this enemy")]
        public ElementType elementType = ElementType.None;
        
        [Header("stats")]
        [Tooltip("maximum health points")]
        public int maxHealth = 50;
        
        [Tooltip("movement speed in units per second")]
        public float moveSpeed = 4f;
        
        [Tooltip("rotation speed in degrees per second")]
        public float rotationSpeed = 360f;
        
        [Header("combat")]
        [Tooltip("damage dealt per attack")]
        public int damage = 10;
        
        [Tooltip("range at which enemy can attack")]
        public float attackRange = 2f;
        
        [Tooltip("time between attacks in seconds")]
        public float attackCooldown = 1.5f;
        
        [Tooltip("time enemy is staggered after being hit")]
        public float staggerDuration = 0.3f;
        
        [Header("detection")]
        [Tooltip("range at which enemy detects players")]
        public float detectionRange = 15f;
        
        [Tooltip("range at which enemy stops chasing")]
        public float leashRange = 25f;
        
        [Header("visuals")]
        [Tooltip("prefab to spawn for this enemy type")]
        public GameObject enemyPrefab;
        
        [Tooltip("color tint for this enemy type (applied to materials)")]
        public Color enemyColor = Color.white;
        
        [Tooltip("scale multiplier for this enemy")]
        public float scaleMultiplier = 1f;
        
        [Header("vfx")]
        [Tooltip("vfx prefab spawned on death")]
        public GameObject deathVfxPrefab;
        
        [Tooltip("vfx prefab spawned on spawn")]
        public GameObject spawnVfxPrefab;
        
        [Tooltip("vfx prefab spawned on attack")]
        public GameObject attackVfxPrefab;
        
        [Header("audio")]
        [Tooltip("sound played when enemy spawns")]
        public AudioClip spawnSound;
        
        [Tooltip("sound played when enemy attacks")]
        public AudioClip attackSound;
        
        [Tooltip("sound played when enemy takes damage")]
        public AudioClip hurtSound;
        
        [Tooltip("sound played when enemy dies")]
        public AudioClip deathSound;
        
        [Header("drops")]
        [Tooltip("exp awarded on death (for future use)")]
        public int experienceReward = 10;
        
        [Header("editor")]
        [Tooltip("color used for gizmos in scene view")]
        public Color gizmoColor = Color.red;
    }
}
