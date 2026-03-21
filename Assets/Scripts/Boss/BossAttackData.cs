using UnityEngine;
using Category5.Core;

namespace Category5.Boss
{
    // scriptable object for defining boss attacks
    // so yall can create new attacks in the editor without touching code :)
    [CreateAssetMenu(fileName = "New Boss Attack", menuName = "Category5/Boss Attack")]
    public class BossAttackData : ScriptableObject
    {
        [Header("identity")]
        [Tooltip("display name for debugging and ui")] // bro these tooltip things are so goated
        public string attackName = "New Attack";
        
        [Tooltip("attack type for vfx/sfx hooks")]
        public BossAttackType attackType = BossAttackType.Slam;
        
        [Header("selection")]
        [Tooltip("higher weight = more likely to be selected (relative to other attacks)")]
        [Range(0f, 100f)]
        public float selectionWeight = 50f;
        
        [Tooltip("only use this attack when boss hp is below this percentage (1.0 = always available)")]
        [Range(0f, 1f)]
        public float healthThreshold = 1f;
        
        [Tooltip("minimum distance to target for this attack to be considered")]
        public float minRange = 0f;
        
        [Tooltip("maximum distance to target for this attack to be considered")]
        public float maxRange = 100f;
        
        [Header("timing")]
        [Tooltip("how long the telegraph phase lasts for this attack")]
        public float telegraphDuration = 1.5f;
        
        [Tooltip("how long the attack execution phase lasts")]
        public float attackDuration = 1.0f;
        
        [Tooltip("cooldown after this specific attack before boss can act again")]
        public float cooldownDuration = 1.0f;
        
        [Header("damage")]
        [Tooltip("base damage dealt by this attack")]
        public int damage = 20;
        
        [Tooltip("radius of the damage area")]
        public float damageRadius = 3f;
        
        [Tooltip("offset from boss position where damage is checked (local space). set to (0,0,0) for attacks centered on boss")]
        public Vector3 damageOffset = new Vector3(0f, 0f, 2f);
        
        [Header("movement during attack")]
        [Tooltip("does the boss lunge forward during this attack")]
        public bool hasLunge = false;
        
        [Tooltip("speed of the lunge")]
        public float lungeSpeed = 8f;
        
        [Tooltip("distance of the lunge")]
        public float lungeDistance = 2f;
        
        [Header("sweep attack")]
        [Tooltip("is this a sweeping beam/line attack")]
        public bool isSweep = false;
        
        [Tooltip("offset from boss position where sweep originates (use negative y to lower the sweep to player height)")]
        public Vector3 sweepOffset = new Vector3(0f, -1f, 0f);
        
        [Tooltip("angle of the sweep in degrees (180 = half circle)")]
        public float sweepAngle = 180f;
        
        [Tooltip("length of the sweep beam")]
        public float sweepLength = 10f;
        
        [Tooltip("width of the sweep beam")]
        public float sweepWidth = 2f;
        
        [Header("projectile attack")]
        [Tooltip("does this attack fire a projectile instead of dealing melee damage")]
        public bool hasProjectile = false;
        
        [Tooltip("networkobject prefab with BossProjectile component — must be registered in NetworkManager prefab list")]
        public GameObject projectilePrefab;
        
        [Tooltip("where the projectile spawns, in boss local space (e.g. 0,1,1.5 = slightly forward and up)")]
        public Vector3 projectileSpawnOffset = new Vector3(0f, 1f, 1.5f);
        
        [Tooltip("how fast the projectile travels")]
        public float projectileSpeed = 15f;
        
        [Tooltip("seconds before the projectile auto-despawns if it misses")]
        public float projectileLifetime = 6f;
        
        [Tooltip("how many projectiles to fire (1 = single bolt, 3+ = fan spread)")]
        [Range(1, 7)]
        public int projectileCount = 1;
        
        [Tooltip("total angle of the fan spread in degrees (ignored when projectileCount is 1)")]
        [Range(0f, 90f)]
        public float projectileSpreadAngle = 45f;
        
        [Header("feedback")]
        [Tooltip("custom feedback settings for this attack (leave at 0 to use defaults)")]
        public HitFeedbackData customFeedback;
        
        [Tooltip("is this a heavy attack (triggers stronger feedback)")]
        public bool isHeavyAttack = false;
        
        [Header("editor")]
        [Tooltip("color used for gizmos in scene view - helps identify attacks visually")]
        public Color gizmoColor = Color.red;
        
        [Header("telegraph visuals")]
        [Tooltip("prefab to spawn during telegraph phase (ground indicator, warning vfx, etc)")]
        public GameObject telegraphPrefab;
        
        [Tooltip("color tint for the telegraph indicator")]
        public Color telegraphColor = new Color(1f, 0.3f, 0.3f, 0.5f);
        
        [Header("attack visuals")]
        [Tooltip("prefab to spawn when attack executes (explosion, beam, shockwave, etc)")]
        public GameObject attackVfxPrefab;
        
        [Tooltip("sound effect to play during telegraph")]
        public AudioClip telegraphSound;
        
        [Tooltip("sound effect to play when attack executes")]
        public AudioClip attackSound;
    }
}
