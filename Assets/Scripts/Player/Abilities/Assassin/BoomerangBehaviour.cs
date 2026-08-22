using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Category5.Enemies;
using Category5.Boss;
using Category5.WeakPoints;

namespace Category5.Player
{
    // add this to a NetworkedProjectile prefab to turn it into a boomerang
    // flies outward to max range then returns to the caster, dealing tick damage while overlapping enemies
    // requires a sibling NetworkedProjectile component which provides owner info and network lifecycle
    public class BoomerangBehaviour : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("distance from spawn point before the boomerang reverses direction")]
        [SerializeField] private float maxRange = 6f;

        [Tooltip("speed multiplier applied while returning so the boomerang feels snappy on the way back")]
        [SerializeField] private float returnSpeedMultiplier = 1.2f;

        [Header("Damage")]
        [Tooltip("seconds between damage ticks while overlapping an enemy")]
        [SerializeField] private float damageTickInterval = 0.25f;

        [Tooltip("sphere radius for the overlap check around the boomerang")]
        [SerializeField] private float hitRadius = 1f;

        [Tooltip("layers to check for enemies on the overlap test")]
        [SerializeField] private LayerMask enemyLayers;

        // artist hooks
        public static event System.Action<Vector3> OnBoomerangTickDamage;
        public static event System.Action<Vector3> OnBoomerangReturned;

        private NetworkedProjectile _projectile;
        private Rigidbody _rigidbody;
        private Vector3 _spawnPosition;
        private bool _isReturning;
        private float _damageTickTimer;
        private readonly HashSet<int> _enemiesDamagedThisTick = new HashSet<int>();

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _projectile = GetComponent<NetworkedProjectile>();

            if (_projectile == null)
            {
                Debug.LogError("BoomerangBehaviour: no sibling NetworkedProjectile found! This component must be on the same GameObject as a NetworkedProjectile.");
                enabled = false;
                return;
            }

            // tell the base projectile to skip its normal single-hit damage
            _projectile.ExternalDamageHandling = true;
        }

        // called after NetworkedProjectile.OnNetworkSpawn has set up owner info
        // we use LateUpdate on the first frame to ensure the projectile has initialized
        private bool _initialized;

        private void LateUpdate()
        {
            // only the server drives movement and damage — clients just follow NetworkTransform
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            if (!_initialized)
            {
                _spawnPosition = transform.position;
                _initialized = true;
            }

            UpdateMovement();
            UpdateDamageTick();
        }

        private void UpdateMovement()
        {
            if (_rigidbody == null || _projectile == null) return;

            float baseSpeed = _projectile.Speed > 0f ? _projectile.Speed : 18f;

            if (!_isReturning)
            {
                // outbound phase — fly forward until we reach max range
                float distanceFromSpawn = Vector3.Distance(transform.position, _spawnPosition);
                if (distanceFromSpawn >= maxRange)
                {
                    _isReturning = true;
                }
                else
                {
                    // drive the velocity every frame so movement never depends on spawn-time setup
                    _rigidbody.linearVelocity = transform.forward * baseSpeed;
                }
            }
            else
            {
                // return phase — track the caster and fly back
                Transform caster = _projectile.OwnerStats != null ? _projectile.OwnerStats.transform : null;
                if (caster == null) return;

                Vector3 toCaster = caster.position - transform.position;
                float distanceToCaster = toCaster.magnitude;

                if (distanceToCaster < 0.5f)
                {
                    OnBoomerangReturned?.Invoke(transform.position);
                    _rigidbody.linearVelocity = Vector3.zero;
                    _projectile.Despawn();
                    return;
                }

                float returnSpeed = baseSpeed * returnSpeedMultiplier;
                _rigidbody.linearVelocity = (toCaster / distanceToCaster) * returnSpeed;

                // rotate to face velocity
                if (_rigidbody.linearVelocity.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(_rigidbody.linearVelocity.normalized);
                }
            }
        }

        private void UpdateDamageTick()
        {
            if (_projectile == null || _projectile.OwnerStats == null) return;

            _damageTickTimer -= Time.deltaTime;
            if (_damageTickTimer > 0f) return;

            _damageTickTimer = damageTickInterval;
            _enemiesDamagedThisTick.Clear();

            Collider[] hitColliders = enemyLayers.value == 0
                ? Physics.OverlapSphere(transform.position, hitRadius)
                : Physics.OverlapSphere(transform.position, hitRadius, enemyLayers.value);

            if (hitColliders.Length == 0) return;

            int damage = _projectile.OwnerStats.CalculateDamage(_projectile.DamageCoefficient).damage;

            foreach (Collider collider in hitColliders)
            {
                int processedId;
                bool weakPointIntercepted = TryRouteTickDamage(collider, damage, out processedId);
                if (weakPointIntercepted) continue;
                if (processedId == 0) continue;

                // dedupe by host instance id so an enemy with multiple colliders only takes damage once per tick
                if (!_enemiesDamagedThisTick.Add(processedId)) continue;

                _projectile.NotifyExternalDamage(damage, collider.transform.position);
                OnBoomerangTickDamage?.Invoke(collider.transform.position);
            }
        }

        private bool TryRouteTickDamage(Collider collider, int damage, out int hostInstanceId)
        {
            hostInstanceId = 0;

            ulong ownerClientId = _projectile.OwnerClientId;

            if (WeakPointHelper.TryRouteMeleeDamage(collider, damage, ownerClientId, transform.position))
            {
                return true;
            }

            EnemyBase enemy = collider.GetComponentInParent<EnemyBase>();
            if (enemy != null && !enemy.IsDead)
            {
                enemy.LastDamagerClientId = ownerClientId;
                enemy.TakeDamage(damage);
                hostInstanceId = enemy.GetInstanceID();
                return false;
            }

            BossBase boss = collider.GetComponentInParent<BossBase>();
            if (boss != null)
            {
                boss.LastDamagerClientId = ownerClientId;
                boss.TakeDamage(damage);
                hostInstanceId = boss.GetInstanceID();
                return false;
            }

            return false;
        }
    }
}
