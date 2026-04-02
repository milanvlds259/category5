using UnityEngine;
using Category5.Core;

namespace Category5.Enemies
{
    // controls how an enemy behaves when it has no target
    public enum IdleBehavior
    {
        Stand,   // stays still until a player enters detection range
        Wander   // picks random nearby points to walk to
    }

    // combat style for future ranged enemy support
    public enum CombatMode
    {
        Melee,   // always moves toward target to attack
        Ranged   // maintains preferred distance and attacks from afar
    }

    // physics parameters - tuned per enemy type so designers control feel without touching code
    [System.Serializable]
    public class EnemyPhysicsData
    {
        [Tooltip("downward acceleration (negative value)")]
        public float gravity = -20f;

        [Tooltip("small constant downward force applied while grounded to prevent floating")]
        public float groundedStickForce = -2f;

        [Tooltip("maximum downward speed")]
        public float terminalVelocity = -50f;

        [Tooltip("radius of the sphere used to check for ground")]
        public float groundCheckRadius = 0.22f;

        [Tooltip("offset from the enemy's root position for the ground check sphere")]
        public Vector3 groundCheckOffset = new Vector3(0f, -0.64f, 0f);

        [Tooltip("frames of confirmed ground contact before marking as grounded")]
        [Range(1, 10)]
        public int groundedConfirmFrames = 2;

        [Tooltip("frames of no ground contact before marking as airborne")]
        [Range(1, 10)]
        public int groundedLossFrames = 2;

        [Tooltip("decay rate for horizontal launch velocity (e.g. from fighter q slam)")]
        public float launchDecayRate = 12f;
    }

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
        [Min(1)]
        public int maxHealth = 50;

        [Tooltip("movement speed in units per second")]
        [Range(0.5f, 20f)]
        public float moveSpeed = 4f;

        [Tooltip("rotation speed in degrees per second")]
        [Range(10f, 720f)]
        public float rotationSpeed = 360f;

        [Header("combat")]
        [Tooltip("base damage per attack - multiplied by EnemyAttackData.damageMultiplier")]
        [Min(1)]
        public int damage = 10;

        [Tooltip("default attack range - can be overridden per attack in EnemyAttackData")]
        [Range(0.5f, 20f)]
        public float attackRange = 2f;

        [Tooltip("time between attacks in seconds")]
        [Range(0.1f, 10f)]
        public float attackCooldown = 1.5f;

        [Tooltip("time enemy is staggered after being hit")]
        [Range(0f, 2f)]
        public float staggerDuration = 0.3f;

        [Tooltip("attacks this enemy can perform - one is chosen by weight each time it attacks")]
        public EnemyAttackData[] attacks;

        [Header("detection")]
        [Tooltip("range at which enemy detects players")]
        [Range(1f, 100f)]
        public float detectionRange = 15f;

        [Tooltip("range at which enemy stops chasing and returns to idle")]
        [Range(1f, 150f)]
        public float leashRange = 25f;

        [Header("idle behavior")]
        [Tooltip("what the enemy does when it has no target")]
        public IdleBehavior idleBehavior = IdleBehavior.Stand;

        [Tooltip("radius around the spawn point that the enemy will wander within (wander mode only)")]
        [Range(1f, 30f)]
        public float wanderRadius = 5f;

        [Tooltip("seconds between picking a new wander destination (wander mode only)")]
        [Range(0.5f, 15f)]
        public float wanderInterval = 3f;

        [Header("death")]
        [Tooltip("seconds after death before the enemy despawns - match this to the death animation length")]
        [Range(0f, 10f)]
        public float deathLingerDuration = 1.5f;

        [Header("combat mode")]
        [Tooltip("whether this enemy fights at melee or ranged distance")]
        public CombatMode combatMode = CombatMode.Melee;

        [Tooltip("preferred engagement distance for ranged enemies")]
        [Range(1f, 30f)]
        public float preferredRangedDistance = 8f;

        [Header("visuals")]
        [Tooltip("prefab to spawn for this enemy type")]
        public GameObject enemyPrefab;

        [Tooltip("color tint applied to the enemy's materials on spawn")]
        public Color enemyColor = Color.white;

        [Tooltip("scale multiplier for this enemy")]
        [Range(0.1f, 5f)]
        public float scaleMultiplier = 1f;

        [Header("vfx")]
        [Tooltip("vfx prefab spawned on death")]
        public GameObject deathVfxPrefab;

        [Tooltip("vfx prefab spawned on spawn")]
        public GameObject spawnVfxPrefab;

        [Header("drops")]
        [Tooltip("exp awarded on death (for future use)")]
        [Min(0)]
        public int experienceReward = 10;

        [Header("physics")]
        [Tooltip("physics parameters for this enemy type")]
        public EnemyPhysicsData physics = new EnemyPhysicsData();

        [Header("editor")]
        [Tooltip("color used for gizmos in scene view")]
        public Color gizmoColor = Color.red;
    }
}
