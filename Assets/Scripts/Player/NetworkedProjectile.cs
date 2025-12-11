using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.PowerUps;
using Category5.Boss;

namespace Category5.Player
{
    /// <summary>
    /// networked projectile that travels forward, deals damage on impact, and despawns
    /// spawned by server only, syncs position via networktransform
    /// collider can be on this object or a child object
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkedProjectile : NetworkBehaviour
    {
        [Header("Runtime Data (Set by Spawner)")]
        [SerializeField] private float speed = 20f;
        [SerializeField] private int damage = 15;
        [SerializeField] private float lifetime = 5f;
        
        // piercing behavior for critshot
        private bool _isPiercing = false;
        private bool _ignoreEnemies = false;
        private bool _ignoreEnvironment = false;
        
        // the client who fired this projectile (for damage feedback)
        private ulong _ownerClientId;
        
        // reference to owner's player stats for damage modifiers
        private PlayerStats _ownerStats;
        
        // track if we've already hit something to prevent double damage
        private bool _hasHit = false;
        
        // cached components
        private Rigidbody _rigidbody;
        
        // vfx prefabs (set by spawner)
        private GameObject _impactVfxPrefab;
        
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            
            // configure rigidbody for projectile behavior
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = false;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            
            // find collider on this object or children and ensure it's a trigger
            var collider = GetComponentInChildren<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
            else
            {
                Debug.LogWarning("NetworkedProjectile: No collider found on this object or children!");
            }
        }
        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // start lifetime countdown on server
                Invoke(nameof(DespawnProjectile), lifetime);
                
                // set initial velocity
                _rigidbody.linearVelocity = transform.forward * speed;
            }
            else
            {
                // clients just follow the networked transform, disable physics
                _rigidbody.isKinematic = true;
            }
        }
        
        /// <summary>
        /// initialize projectile with data from the spawner (called on server before spawn)
        /// </summary>
        public void Initialize(ProjectileData data, ulong ownerClientId, PlayerStats ownerStats)
        {
            speed = data.Speed;
            damage = data.Damage;
            lifetime = data.Lifetime;
            _ownerClientId = ownerClientId;
            _ownerStats = ownerStats;
            _impactVfxPrefab = data.ImpactVfxPrefab;
            _isPiercing = false;
            _ignoreEnemies = false;
            _ignoreEnvironment = false;
        }
        
        // initialize projectile with charged multipliers (called on server before spawn)
        public void InitializeCharged(ProjectileData data, ulong ownerClientId, PlayerStats ownerStats, float damageMultiplier, float speedMultiplier)
        {
            // apply multipliers to base values
            speed = data.Speed * speedMultiplier;
            damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            lifetime = data.Lifetime;
            _ownerClientId = ownerClientId;
            _ownerStats = ownerStats;
            _impactVfxPrefab = data.ImpactVfxPrefab;
            _isPiercing = false;
            _ignoreEnemies = false;
            _ignoreEnvironment = false;
        }
        
        // initialize piercing projectile (for critshot ultimate)
        public void InitializePiercing(ProjectileData data, ulong ownerClientId, PlayerStats ownerStats, float damageMultiplier, bool ignoreEnemies = true, bool ignoreEnvironment = true)
        {
            speed = data.Speed;
            damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            lifetime = data.Lifetime;
            _ownerClientId = ownerClientId;
            _ownerStats = ownerStats;
            _impactVfxPrefab = data.ImpactVfxPrefab;
            _isPiercing = true;
            _ignoreEnemies = ignoreEnemies;
            _ignoreEnvironment = ignoreEnvironment;
        }
        
        private void OnTriggerEnter(Collider other)
        {
            // only server handles collision
            if (!IsServer) return;
            if (_hasHit && !_isPiercing) return;
            
            Debug.Log($"Projectile collision with: {other.gameObject.name} on layer {LayerMask.LayerToName(other.gameObject.layer)}");
            
            // ignore collision with all players (friendly fire disabled)
            if (other.TryGetComponent<PlayerController>(out _))
                return;
            
            // also check parent in case collider is on a child object
            if (other.GetComponentInParent<PlayerController>() != null)
                return;
            
            // check if we hit something damageable (check both this object and parent)
            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null)
            {
                damageable = other.GetComponentInParent<IDamageable>();
            }
            
            if (damageable != null)
            {
                Debug.Log($"Found IDamageable on {(damageable as MonoBehaviour)?.gameObject.name ?? "unknown"}");
                
                // check if this is a boss
                bool isBoss = other.GetComponentInParent<BossBase>() != null;
                
                // piercing logic
                if (_isPiercing)
                {
                    // if it's an enemy and we ignore enemies, pierce through
                    if (_ignoreEnemies && !isBoss)
                    {
                        // still deal damage but don't stop
                        ApplyDamageAndEffects(damageable, other.transform.position);
                        return;
                    }
                    
                    // if it's a boss, always stop (boss ends piercing)
                    if (isBoss)
                    {
                        ApplyDamageAndEffects(damageable, other.transform.position);
                        _hasHit = true;
                        DespawnProjectile();
                        return;
                    }
                }
                
                // normal hit (non-piercing or hit a valid target)
                _hasHit = true;
                ApplyDamageAndEffects(damageable, other.transform.position);
                DespawnProjectile();
            }
            else
            {
                Debug.Log($"No IDamageable found on {other.gameObject.name} or its parents");
                
                // hit environment (wall, obstacle)
                if (_isPiercing && _ignoreEnvironment)
                {
                    // pierce through environment
                    return;
                }
                
                // hit something non-damageable and not piercing through it
                _hasHit = true;
                NotifyProjectileHitClientRpc(transform.position, 0);
                DespawnProjectile();
            }
        }
        
        // helper method to apply damage and all effects
        private void ApplyDamageAndEffects(IDamageable damageable, Vector3 hitPosition)
        {
            // calculate final damage with power-up modifiers
            int finalDamage = _ownerStats != null 
                ? _ownerStats.CalculateDamage(damage) 
                : damage;
                
            // deal damage
            damageable.TakeDamage(finalDamage);
            
            // apply lifesteal if owner has it
            int lifestealAmount = _ownerStats != null ? _ownerStats.LifestealAmount : 0;
            if (lifestealAmount > 0)
            {
                ApplyLifestealToOwner(lifestealAmount);
            }
            
            // notify the attacking player to show damage number
            ShowDamageNumberClientRpc(finalDamage, hitPosition, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { _ownerClientId }
                }
            });
            
            // trigger hit feedback for the attacking player
            TriggerHitFeedbackClientRpc(hitPosition, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { _ownerClientId }
                }
            });
            
            // notify all clients for vfx hooks
            NotifyProjectileHitClientRpc(hitPosition, finalDamage);
        }
        
        private void ApplyLifestealToOwner(int healAmount)
        {
            // find the owner player and heal them
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(_ownerClientId, out var client))
            {
                var playerController = client.PlayerObject?.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.Heal(healAmount);
                    
                    // show heal feedback
                    ShowLifestealVfxClientRpc(healAmount, playerController.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { _ownerClientId }
                        }
                    });
                }
            }
        }
        
        private void DespawnProjectile()
        {
            if (!IsServer) return;
            
            CancelInvoke(nameof(DespawnProjectile));
            
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }
        
        [ClientRpc]
        private void ShowDamageNumberClientRpc(int damageAmount, Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            // RPC with TargetClientIds already ensures this only runs on the intended client
            if (Category5.UI.UIManager.Instance != null)
            {
                Category5.UI.UIManager.Instance.ShowDamageNumber(damageAmount, position);
            }
            else
            {
                Debug.LogWarning("[NetworkedProjectile] UIManager.Instance is null!");
            }
        }
        
        [ClientRpc]
        private void TriggerHitFeedbackClientRpc(Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            if (HitFeedbackManager.Instance != null)
            {
                // ranged hits use light feedback
                HitFeedbackManager.Instance.TriggerLightHit(position);
            }
        }
        
        [ClientRpc]
        private void NotifyProjectileHitClientRpc(Vector3 position, int damage)
        {
            // notify hit feedback manager for vfx hooks
            if (HitFeedbackManager.Instance != null && damage > 0)
            {
                HitFeedbackManager.Instance.NotifyPlayerHitEnemy(position, damage, false);
            }
            
            // TODO: spawn impact vfx here for all clients
            Debug.Log($"Projectile hit at {position}");
        }
        
        [ClientRpc]
        private void ShowLifestealVfxClientRpc(int healAmount, Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            Category5.Audio.PlayerEvents.InvokeHeal(position, healAmount);
            Debug.Log($"Lifesteal healed {healAmount} HP!");
        }
    }
}
