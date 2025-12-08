using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Category5.Core;
using Category5.Enemies;
using Category5.Player;
using Category5.PowerUps;

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
            
            // configure collider as trigger
            Collider collider = arrow.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
            
            // add rigidbody for physics
            Rigidbody rb = arrow.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.isKinematic = true; // kinematic since we're moving it manually
            }
            
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
                transform.position += velocity * Time.deltaTime;
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (hasImpacted) return;
            
            Debug.Log($"TrackerArrow hit: {other.name}");
            
            // ignore players
            if (other.GetComponent<PlayerController>() != null || other.GetComponentInParent<PlayerController>() != null)
            {
                Debug.Log($"  -> Ignoring player");
                return;
            }
            
            // impact - create dot zone
            Debug.Log($"  -> Creating DoT zone at {transform.position}");
            hasImpacted = true;
            CreateDotZone(transform.position);
            Destroy(gameObject);
        }
        
        private void CreateDotZone(Vector3 position)
        {
            // create zone object
            GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            zone.transform.position = position;
            zone.transform.localScale = new Vector3(zoneRadius * 2f, 0.1f, zoneRadius * 2f);
            
            // make it a trigger
            Collider col = zone.GetComponent<Collider>();
            col.isTrigger = true;
            
            // remove rigidbody if present
            Rigidbody rb = zone.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);
            
            // add dot zone component
            SpiralbowDotZone dotZone = zone.AddComponent<SpiralbowDotZone>();
            dotZone.Initialize(zoneDuration, zoneRadius, damageTickInterval, slowMultiplier, baseDamage, ownerStats, ownerClientId);
            
            // destroy after duration
            Destroy(zone, zoneDuration);
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
        private HashSet<EnemyBase> enemiesInZone = new HashSet<EnemyBase>();
        
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
            // check if it's an enemy
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy == null)
            {
                enemy = other.GetComponentInParent<EnemyBase>();
            }
            
            if (enemy != null && !enemiesInZone.Contains(enemy))
            {
                enemiesInZone.Add(enemy);
                // apply slow
                enemy.ApplyMovementModifier(slowMultiplier, zoneDuration, $"Spiralbow_{ownerClientId}");
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
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
            
            foreach (var enemy in enemiesInZone)
            {
                // calculate damage with power-up modifiers
                int finalDamage = ownerStats != null 
                    ? ownerStats.CalculateDamage((int)baseDamage) 
                    : (int)baseDamage;
                
                // deal damage
                enemy.TakeDamage(finalDamage);
            }
        }
        
        private void OnDestroy()
        {
            enemiesInZone.Clear();
        }
    }
}
