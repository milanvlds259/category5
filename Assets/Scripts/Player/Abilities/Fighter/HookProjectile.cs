using UnityEngine;
using Unity.Netcode;
using Category5.Enemies;
using Category5.Boss;

namespace Category5.Player.Abilities
{
    // hook projectile for fighter e grappling hook ability
    // notifies owner when it hits an enemy/boss, despawns on terrain or lifetime expiration
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class HookProjectile : NetworkBehaviour
    {
        [Header("settings")]
        [SerializeField] private float defaultSpeed = 20f;
        [SerializeField] private float defaultLifetime = 3f;
        
        [Header("layers to ignore")]
        [SerializeField] private LayerMask ignoreLayers; // set in prefab to ignore Player and Projectile layers
        
        // runtime state
        private ulong _ownerNetworkObjectId;
        private float _speed;
        private float _lifetime;
        private float _lifetimeTimer;
        private bool _hasHit;
        private Rigidbody _rb;
        private Vector3 _direction;
        private float _pullForce;
        
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.isKinematic = false;
        }
        
        // initialize the hook with owner info and settings
        // called by PlayerAbilityManager after spawning
        public void Initialize(ulong ownerNetworkObjectId, Vector3 direction, float speed, float lifetime, float pullForce)
        {
            _ownerNetworkObjectId = ownerNetworkObjectId;
            _direction = direction.normalized;
            _speed = speed;
            _lifetime = lifetime;
            _pullForce = pullForce;
            _lifetimeTimer = 0f;
            _hasHit = false;
            
            // set velocity
            if (_rb != null)
            {
                _rb.linearVelocity = _direction * _speed;
            }
            
            // rotate to face direction
            if (_direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(_direction);
            }
        }
        
        private void Update()
        {
            if (!IsServer) return;
            if (_hasHit) return;
            
            // track lifetime
            _lifetimeTimer += Time.deltaTime;
            if (_lifetimeTimer >= _lifetime)
            {
                // despawn on lifetime expiration
                DespawnHook();
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            if (_hasHit) return;
            
            Debug.Log($"HookProjectile: OnTriggerEnter hit {other.gameObject.name} on layer {LayerMask.LayerToName(other.gameObject.layer)}");
            
            // check if we should ignore this layer
            int otherLayer = other.gameObject.layer;
            if (((1 << otherLayer) & ignoreLayers) != 0)
            {
                Debug.Log($"HookProjectile: Ignoring collision with {other.gameObject.name} (layer ignored)");
                return; // ignore this collision
            }
            
            // ignore other players
            if (other.GetComponentInParent<PlayerController>() != null)
            {
                Debug.Log($"HookProjectile: Ignoring collision with {other.gameObject.name} (player)");
                return;
            }
            
            // ignore other projectiles (check for NetworkedProjectile or HookProjectile)
            if (other.GetComponentInParent<HookProjectile>() != null)
            {
                Debug.Log($"HookProjectile: Ignoring collision with {other.gameObject.name} (hook projectile)");
                return;
            }
            if (other.GetComponentInParent<NetworkedProjectile>() != null)
            {
                Debug.Log($"HookProjectile: Ignoring collision with {other.gameObject.name} (networked projectile)");
                return;
            }
            
            // check for boss
            var boss = other.GetComponentInParent<BossBase>();
            if (boss != null)
            {
                Debug.Log($"HookProjectile: Hit boss {boss.gameObject.name}!");
                _hasHit = true;
                NotifyOwnerHit(other.ClosestPoint(transform.position), boss.NetworkObjectId, true);
                DespawnHook();
                return;
            }
            
            // check for enemy
            var enemy = other.GetComponentInParent<EnemyBase>();
            if (enemy != null)
            {
                Debug.Log($"HookProjectile: Hit enemy {enemy.gameObject.name}!");
                _hasHit = true;
                NotifyOwnerHit(other.ClosestPoint(transform.position), enemy.NetworkObjectId, false);
                DespawnHook();
                return;
            }
            
            // hit terrain or other solid object - just despawn
            Debug.Log($"HookProjectile: Hit terrain/object {other.gameObject.name}, despawning");
            _hasHit = true;
            DespawnHook();
        }
        
        private void NotifyOwnerHit(Vector3 hitPosition, ulong targetNetworkObjectId, bool isBoss)
        {
            Debug.Log($"HookProjectile: NotifyOwnerHit called. Owner ID: {_ownerNetworkObjectId}, Target ID: {targetNetworkObjectId}, IsBoss: {isBoss}");
            
            // find the owner player and notify their ability manager
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(_ownerNetworkObjectId, out var ownerNetworkObject))
            {
                Debug.LogError($"HookProjectile: Could not find owner NetworkObject with ID {_ownerNetworkObjectId}");
                return;
            }
            
            Debug.Log($"HookProjectile: Found owner NetworkObject {ownerNetworkObject.gameObject.name}");
            
            var abilityManager = ownerNetworkObject.GetComponent<PlayerAbilityManager>();
            if (abilityManager != null)
            {
                Debug.Log($"HookProjectile: Found PlayerAbilityManager, calling OnHookHitTarget");
                abilityManager.OnHookHitTarget(hitPosition, targetNetworkObjectId, isBoss, _pullForce);
            }
            else
            {
                Debug.LogError($"HookProjectile: Could not find PlayerAbilityManager component on owner {ownerNetworkObject.gameObject.name}");
            }
        }
        
        private void DespawnHook()
        {
            if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn();
            }
        }
        
        // editor gizmo to show hook direction
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
    }
}
