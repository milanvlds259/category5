using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Player;

namespace Category5.Enemies
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyProjectile : NetworkBehaviour
    {
        private int _damage;
        private float _speed;
        private float _lifetime;
        private bool _hasHit = false;

        private Rigidbody _rigidbody;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Invoke(nameof(DespawnProjectile), _lifetime);
                _rigidbody.linearVelocity = transform.forward * _speed;
            }
            else
            {
                _rigidbody.isKinematic = true;
            }
        }

        public void Initialize(int damage, float speed, float lifetime)
        {
            // Only server should initialize
            _damage = damage;
            _speed = speed;
            _lifetime = lifetime;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer || _hasHit) return;

            // Check for PlayerController first as requested
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                _hasHit = true;
                player.TakeDamage(_damage);
                DespawnProjectile();
                return;
            }

            // Check for IDamageable that is NOT an enemy
            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                var enemy = other.GetComponentInParent<EnemyBase>();
                if (enemy == null)
                {
                    _hasHit = true;
                    damageable.TakeDamage(_damage);
                    DespawnProjectile();
                    return;
                }
            }
            else
            {
                // Hit something else (environment)
                _hasHit = true;
                DespawnProjectile();
            }
        }

        private void DespawnProjectile()
        {
            if (!IsServer) return;

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }
    }
}
