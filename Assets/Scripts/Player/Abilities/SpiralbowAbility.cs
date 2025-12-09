using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Category5.Core;
using Category5.Enemies;
using Category5.Player;
using Category5.PowerUps;
using Category5.Boss;

namespace Category5
{
    // ranger e ability - tracker arrow with dot zone and slow
    public class SpiralbowAbility : AbilityBase
    {
        [Header("Spiralbow Settings")]
        [SerializeField] private float zoneRadius = 5f;
        [SerializeField] private float zoneDuration = 6f;
        [SerializeField] private float damageTickInterval = 0.5f;
        [SerializeField] private float slowMultiplier = 0.6f; // 60% speed (40% slow)
        [SerializeField] private float arrowSpeed = 20f;
        [SerializeField] private float arrowLifetime = 5f;
        
        private PlayerCombat playerCombat;
        
        public override void Initialize(PlayerController player, PlayerStats stats, PlayerAbilityManager manager)
        {
            base.Initialize(player, stats, manager);
            playerCombat = player.GetComponent<PlayerCombat>();
        }
        
        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            if (playerCombat == null) return false;
            if (playerCombat.CurrentCombatClass != CombatClass.Ranged) return false;
            
            return true;
        }
        
        public override void Execute()
        {
            Debug.Log("SpiralbowAbility.Execute() called");
            
            // get aim direction from camera
            Vector3 spawnPos = playerController.transform.position + Vector3.up * 1.5f + playerController.transform.forward * 0.5f;
            Vector3 direction = GetAimDirection();
            
            // spawn tracker arrow that will create zone on impact
            SpawnTrackerArrow(spawnPos, direction);
            
            // play vfx and audio directly (no need for RPC since we're owner)
            SpawnVfx(spawnPos);
            PlayAudio(spawnPos);
        }
        
        private Vector3 GetAimDirection()
        {
            if (Camera.main == null)
            {
                return playerController.transform.forward;
            }
            
            // raycast from screen center
            Ray aimRay = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 spawnPos = playerController.transform.position + Vector3.up * 1.5f + playerController.transform.forward * 0.5f;
            
            if (Physics.Raycast(aimRay, out RaycastHit hit, 100f))
            {
                return (hit.point - spawnPos).normalized;
            }
            else
            {
                return (aimRay.GetPoint(100f) - spawnPos).normalized;
            }
        }
        
        private void SpawnTrackerArrow(Vector3 position, Vector3 direction)
        {
            // create a simple tracker arrow object
            GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            arrow.transform.position = position;
            arrow.transform.forward = direction;
            arrow.transform.localScale = Vector3.one * 0.3f;
            
            // set layer to "Projectile" so DoT zones know to ignore it
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            if (projectileLayer == -1)
            {
                // if layer doesn't exist, use Default
                projectileLayer = LayerMask.NameToLayer("Default");
            }
            arrow.layer = projectileLayer;
            
            // remove the collider created by CreatePrimitive
            Collider primCollider = arrow.GetComponent<Collider>();
            if (primCollider != null)
            {
                Destroy(primCollider);
            }
            
            // remove the rigidbody created by CreatePrimitive
            Rigidbody primRb = arrow.GetComponent<Rigidbody>();
            if (primRb != null)
            {
                Destroy(primRb);
            }
            
            // add a fresh collider as trigger
            SphereCollider sphereCollider = arrow.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = 0.5f;
            
            // add rigidbody (non-kinematic for proper trigger detection)
            Rigidbody rb = arrow.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false; // NOT kinematic so triggers work properly
            rb.constraints = RigidbodyConstraints.FreezeRotation; // don't rotate
            
            // add tracker component
            TrackerArrow tracker = arrow.AddComponent<TrackerArrow>();
            tracker.Initialize(zoneDuration, zoneRadius, damageTickInterval, slowMultiplier, abilityData.baseDamage, playerStats, OwnerClientId);
            
            // manually move the arrow
            tracker.SetVelocity(direction * arrowSpeed);
            
            // destroy after lifetime
            Destroy(arrow, arrowLifetime);
        }
    }
    
    // tracker arrow component that creates dot zone on collision
    public class TrackerArrow : MonoBehaviour
    {
        private float zoneDuration;
        private float zoneRadius;
        private float damageTickInterval;
        private float slowMultiplier;
        private float baseDamage;
        private PlayerStats ownerStats;
        private ulong ownerClientId;
        private bool hasImpacted = false;
        private Vector3 velocity;
        
        public void Initialize(float duration, float radius, float tickInterval, float slow, float damage, PlayerStats stats, ulong clientId)
        {
            zoneDuration = duration;
            zoneRadius = radius;
            damageTickInterval = tickInterval;
            slowMultiplier = slow;
            baseDamage = damage;
            ownerStats = stats;
            ownerClientId = clientId;
        }
        
        public void SetVelocity(Vector3 newVelocity)
        {
            velocity = newVelocity;
        }
        
        private void Update()
        {
            // manually move the arrow
            if (!hasImpacted)
            {
                Vector3 nextPos = transform.position + velocity * Time.deltaTime;
                Vector3 moveDirection = (nextPos - transform.position).normalized;
                float moveDistance = Vector3.Distance(nextPos, transform.position);
                
                // raycast to check for collision along the path
                if (Physics.Raycast(transform.position, moveDirection, out RaycastHit hit, moveDistance))
                {
                    // check if we hit a player (should ignore)
                    if (hit.collider.GetComponent<PlayerController>() != null || 
                        hit.collider.GetComponentInParent<PlayerController>() != null)
                    {
                        // move past the player and continue
                        transform.position = nextPos;
                        return;
                    }
                    
                    // we hit something - trigger impact
                    Debug.Log($"TrackerArrow raycast hit: {hit.collider.name}");
                    hasImpacted = true;
                    
                    // find ground level - raycast down from impact point
                    Vector3 zonePosition = hit.point;
                    if (Physics.Raycast(hit.point, Vector3.down, out RaycastHit groundHit, 100f))
                    {
                        zonePosition = groundHit.point;
                        Debug.Log($"  -> Ground detected at {zonePosition}");
                    }
                    else
                    {
                        // fallback: set Y to 0 if no ground hit
                        zonePosition.y = 0f;
                        Debug.Log($"  -> No ground hit, using Y=0");
                    }
                    
                    CreateDotZone(zonePosition);
                    Destroy(gameObject);
                    return;
                }
                
                // no collision, move normally
                transform.position = nextPos;
            }
        }
        
        
        private void CreateDotZone(Vector3 position)
        {
            // create visual telegraph cylinder (flat on ground)
            GameObject visualCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualCylinder.name = "SpiralbowZoneVisual";
            visualCylinder.transform.position = position;
            visualCylinder.transform.localScale = new Vector3(zoneRadius * 2f, 0.1f, zoneRadius * 2f);
            
            // disable collider on visual (we'll use invisible sphere for detection)
            Collider visualCol = visualCylinder.GetComponent<Collider>();
            if (visualCol != null) Destroy(visualCol);
            
            // remove rigidbody from visual
            Rigidbody visualRb = visualCylinder.GetComponent<Rigidbody>();
            if (visualRb != null) Destroy(visualRb);
            
            // make visual semi-transparent
            MeshRenderer visualRenderer = visualCylinder.GetComponent<MeshRenderer>();
            if (visualRenderer != null)
            {
                foreach (var mat in visualRenderer.materials)
                {
                    Color col = mat.color;
                    col.a = 0.3f;
                    mat.color = col;
                }
            }
            
            // create invisible hitbox sphere for damage detection
            GameObject hitbox = new GameObject("SpiralbowZoneHitbox");
            hitbox.transform.position = position;
            hitbox.transform.parent = visualCylinder.transform;
            
            // add sphere collider as trigger for damage detection
            SphereCollider sphereCol = hitbox.AddComponent<SphereCollider>();
            sphereCol.radius = zoneRadius;
            sphereCol.isTrigger = true;
            
            // configure collider to ignore projectiles
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            if (projectileLayer != -1)
            {
                // use layer-based collision matrix to ignore projectiles
                Physics.IgnoreLayerCollision(hitbox.layer, projectileLayer);
                Debug.Log($"Configured DoT zone to ignore Projectile layer");
            }
            
            // add rigidbody for proper trigger detection
            Rigidbody hitboxRb = hitbox.AddComponent<Rigidbody>();
            hitboxRb.useGravity = false;
            hitboxRb.isKinematic = true;
            
            // add dot zone component to HITBOX (where the collider is), not the visual
            SpiralbowDotZone dotZone = hitbox.AddComponent<SpiralbowDotZone>();
            dotZone.Initialize(zoneDuration, zoneRadius, damageTickInterval, slowMultiplier, baseDamage, ownerStats, ownerClientId);
            
            // destroy the visual cylinder after duration (which also destroys the hitbox child)
            Destroy(visualCylinder, zoneDuration);
        }
    }
    
    // dot zone that damages and slows enemies over time
    public class SpiralbowDotZone : MonoBehaviour
    {
        private float zoneDuration;
        private float zoneRadius;
        private float damageTickInterval;
        private float slowMultiplier;
        private float baseDamage;
        private PlayerStats ownerStats;
        private ulong ownerClientId;
        
        private float tickTimer;
        private HashSet<IDamageable> enemiesInZone = new HashSet<IDamageable>();
        
        public void Initialize(float duration, float radius, float tickInterval, float slow, float damage, PlayerStats stats, ulong clientId)
        {
            zoneDuration = duration;
            zoneRadius = radius;
            damageTickInterval = tickInterval;
            slowMultiplier = slow;
            baseDamage = damage;
            ownerStats = stats;
            ownerClientId = clientId;
            tickTimer = 0f;
        }
        
        private void Update()
        {
            tickTimer += Time.deltaTime;
            
            if (tickTimer >= damageTickInterval)
            {
                tickTimer = 0f;
                DamageEnemiesInZone();
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            // check if it's a boss
            BossBase boss = other.GetComponent<BossBase>();
            if (boss != null)
            {
                if (!enemiesInZone.Contains(boss))
                {
                    enemiesInZone.Add(boss);
                    Debug.Log($"Spiralbow zone hit boss");
                }
                return;
            }
            
            // check if it's an enemy
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy == null)
            {
                enemy = other.GetComponentInParent<EnemyBase>();
            }
            
            if (enemy != null && !enemiesInZone.Contains(enemy))
            {
                enemiesInZone.Add(enemy);
                Debug.Log($"Spiralbow zone hit enemy");
                // apply slow
                enemy.ApplyMovementModifier(slowMultiplier, zoneDuration, $"Spiralbow_{ownerClientId}");
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            // check if it's a boss
            BossBase boss = other.GetComponent<BossBase>();
            if (boss != null)
            {
                enemiesInZone.Remove(boss);
                return;
            }
            
            // check if it's an enemy
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy == null)
            {
                enemy = other.GetComponentInParent<EnemyBase>();
            }
            
            if (enemy != null)
            {
                enemiesInZone.Remove(enemy);
            }
        }
        
        private void DamageEnemiesInZone()
        {
            // remove any destroyed enemies from the set
            enemiesInZone.RemoveWhere(e => e == null);
            
            foreach (IDamageable target in enemiesInZone)
            {
                // calculate damage with power-up modifiers
                int finalDamage = ownerStats != null 
                    ? ownerStats.CalculateDamage((int)baseDamage) 
                    : (int)baseDamage;
                
                // deal damage directly - boss/enemy TakeDamage will handle server authority
                target.TakeDamage(finalDamage);
                
                Debug.Log($"Spiralbow DoT: Dealt {finalDamage} damage to {target}");
            }
        }
        
        private void OnDestroy()
        {
            enemiesInZone.Clear();
        }
    }
}
