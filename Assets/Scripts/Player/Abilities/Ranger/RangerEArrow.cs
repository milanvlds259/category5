using UnityEngine;
using Unity.Netcode;
using Category5.Player;

namespace Category5
{
    // networked arrow that spawns the ranger e zone on impact
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class RangerEArrow : NetworkBehaviour
    {
        [Header("projectile settings")]
        [SerializeField] private float speed = 20f;
        [SerializeField] private float lifetime = 5f;

        [Header("vfx")]
        [Tooltip("spawned once when the arrow detonates on impact")]
        [SerializeField] private GameObject detonationVfxPrefab;

        // events for vfx/sfx hooks
        public static event System.Action<Vector3> OnArrowDetonated;

        private ulong _ownerClientId;
        private PlayerStats _ownerStats;
        private GameObject _zonePrefab;
        private float _damageCoefficient;
        private float _zoneRadius;
        private float _zoneDuration;
        private float _tickInterval;
        private float _slowMultiplier;

        private bool _hasDetonated;
        private Rigidbody _rigidbody;
        private float _castRadius = 0.2f;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = false;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var col = GetComponentInChildren<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
                _castRadius = Mathf.Max(0.1f, col.bounds.extents.magnitude * 0.3f);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Invoke(nameof(DespawnProjectile), lifetime);
                _rigidbody.linearVelocity = transform.forward * speed;
            }
            else
            {
                _rigidbody.isKinematic = true;
            }
        }

        public void Initialize(ulong ownerClientId, PlayerStats ownerStats, GameObject zonePrefab,
            float damageCoefficient, float projectileSpeed, float projectileLifetime, float zoneRadius,
            float zoneDuration, float tickInterval, float slowMultiplier)
        {
            _ownerClientId = ownerClientId;
            _ownerStats = ownerStats;
            _zonePrefab = zonePrefab;
            _damageCoefficient = damageCoefficient;
            speed = projectileSpeed;
            lifetime = projectileLifetime;
            _zoneRadius = zoneRadius;
            _zoneDuration = zoneDuration;
            _tickInterval = tickInterval;
            _slowMultiplier = slowMultiplier;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            if (_hasDetonated) return;
            if (IsPlayerCollider(other)) return;

            _hasDetonated = true;
            Detonate(other.ClosestPoint(transform.position));
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            if (_hasDetonated) return;

            Vector3 velocity = _rigidbody.linearVelocity;
            if (velocity.sqrMagnitude < 0.001f) return;

            Vector3 direction = velocity.normalized;
            float distance = velocity.magnitude * Time.fixedDeltaTime;

            if (Physics.SphereCast(transform.position, _castRadius, direction, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IsPlayerCollider(hit.collider)) return;

                _hasDetonated = true;
                Detonate(hit.point);
            }
        }

        private void Detonate(Vector3 hitPoint)
        {
            Vector3 zonePosition = hitPoint;
            Vector3 rayOrigin = hitPoint + Vector3.up;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, 20f, ~0, QueryTriggerInteraction.Ignore))
            {
                zonePosition = groundHit.point;
            }
            else
            {
                zonePosition.y = 0f;
            }

            // notify all clients for vfx
            NotifyArrowDetonatedClientRpc(hitPoint);

            SpawnZone(zonePosition);
        }

        private void SpawnZone(Vector3 position)
        {
            if (_zonePrefab == null)
            {
                Debug.LogError("[RangerEArrow] ranger e zone prefab is not assigned");
                DespawnProjectile();
                return;
            }

            GameObject obj = Instantiate(_zonePrefab, position, Quaternion.identity);
            NetworkObject netObj = obj.GetComponent<NetworkObject>();
            RangerEZone zone = obj.GetComponent<RangerEZone>();

            if (netObj == null || zone == null)
            {
                Debug.LogError("[RangerEArrow] zone prefab missing NetworkObject or RangerEZone");
                Destroy(obj);
                DespawnProjectile();
                return;
            }

            zone.Initialize(_ownerClientId, _ownerStats, _damageCoefficient, _zoneRadius, _zoneDuration, _tickInterval, _slowMultiplier);
            netObj.Spawn();
            DespawnProjectile();
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

        private static bool IsPlayerCollider(Collider collider)
        {
            if (collider.GetComponent<PlayerController>() != null) return true;
            if (collider.GetComponentInParent<PlayerController>() != null) return true;
            return false;
        }

        [ClientRpc]
        private void NotifyArrowDetonatedClientRpc(Vector3 position)
        {
            OnArrowDetonated?.Invoke(position);

            if (detonationVfxPrefab != null)
                Instantiate(detonationVfxPrefab, position, Quaternion.identity);
        }
    }
}