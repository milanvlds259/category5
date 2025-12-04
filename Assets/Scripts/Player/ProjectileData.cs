using UnityEngine;

namespace Category5.Player
{
    /// <summary>
    /// scriptable object defining projectile properties for ranged attacks
    /// create via right-click > create > category5 > projectile data
    /// </summary>
    [CreateAssetMenu(fileName = "NewProjectile", menuName = "Category5/Projectile Data")]
    public class ProjectileData : ScriptableObject
    {
        [Header("Projectile Settings")]
        [Tooltip("the networked projectile prefab to spawn")]
        [SerializeField] private GameObject projectilePrefab;
        
        [Tooltip("projectile travel speed in units per second")]
        [SerializeField] private float speed = 20f;
        
        [Tooltip("base damage dealt on impact")]
        [SerializeField] private int damage = 15;
        
        [Tooltip("time in seconds before projectile despawns if it doesn't hit anything")]
        [SerializeField] private float lifetime = 5f;
        
        [Header("Visual Settings")]
        [Tooltip("optional trail or particle effect prefab spawned on projectile")]
        [SerializeField] private GameObject trailVfxPrefab;
        
        [Tooltip("optional impact vfx spawned when projectile hits something")]
        [SerializeField] private GameObject impactVfxPrefab;
        
        [Header("Audio")]
        [Tooltip("sound played when projectile is fired")]
        [SerializeField] private AudioClip fireSound;
        
        [Tooltip("sound played on impact")]
        [SerializeField] private AudioClip impactSound;
        
        // public accessors
        public GameObject ProjectilePrefab => projectilePrefab;
        public float Speed => speed;
        public int Damage => damage;
        public float Lifetime => lifetime;
        public GameObject TrailVfxPrefab => trailVfxPrefab;
        public GameObject ImpactVfxPrefab => impactVfxPrefab;
        public AudioClip FireSound => fireSound;
        public AudioClip ImpactSound => impactSound;
    }
}
