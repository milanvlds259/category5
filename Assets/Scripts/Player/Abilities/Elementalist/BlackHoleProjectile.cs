using UnityEngine;
using Unity.Netcode;
using Category5.Player;

namespace Category5
{
    // black hole projectile that travels forward and spawns a black hole zone on impact
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class BlackHoleProjectile : NetworkBehaviour
    {
        [Header("projectile settings")]
        [SerializeField] private float speed = 14f;
        [SerializeField] private float lifetime = 5f;

        [Header("detonation")]
        [SerializeField] private LayerMask detonationLayers = ~0;

        [Header("vfx")]
        [Tooltip("spawned once when the black hole projectile detonates (sends the zone into its spawn phase)")]
        [SerializeField] private GameObject detonationVfxPrefab;

        // events for vfx/sfx hooks
        public static event System.Action<Vector3> OnBlackHoleDetonated;

        private ulong _ownerClientId;
        private PlayerStats _ownerStats;
        private GameObject _blackHoleZonePrefab;
        private float _damageCoefficient;
        private float _pullRadius;
        private float _pullForce;
        private float _pullDuration;
        private float _pullStrengthRampUp;
        private float _explosionRadius;

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
            float damageCoefficient, float projectileSpeed, float projectileLifetime, float pullRadius,
            float pullForce, float pullDuration, float pullStrengthRampUp, float explosionRadius)
        {
            _ownerClientId = ownerClientId;
            _ownerStats = ownerStats;
            _blackHoleZonePrefab = zonePrefab;
            _damageCoefficient = damageCoefficient;
            speed = projectileSpeed;
            lifetime = projectileLifetime;
            _pullRadius = pullRadius;
            _pullForce = pullForce;
            _pullDuration = pullDuration;
            _pullStrengthRampUp = pullStrengthRampUp;
            _explosionRadius = explosionRadius;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            if (_hasDetonated) return;

            if (IsPlayerCollider(other)) return;

            _hasDetonated = true;
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            SpawnZone(hitPoint);
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            if (_hasDetonated) return;

            Vector3 velocity = _rigidbody.linearVelocity;
            if (velocity.sqrMagnitude < 0.001f) return;

            Vector3 dir = velocity.normalized;
            float distance = velocity.magnitude * Time.fixedDeltaTime;

            if (Physics.SphereCast(transform.position, _castRadius, dir, out RaycastHit hit, distance, detonationLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsPlayerCollider(hit.collider)) return;

                _hasDetonated = true;
                SpawnZone(hit.point);
            }
        }

        private void SpawnZone(Vector3 position)
        {
            if (_blackHoleZonePrefab == null)
            {
                Debug.LogError("[BlackHoleProjectile] black hole zone prefab is not assigned");
                DespawnProjectile();
                return;
            }

            NotifyBlackHoleDetonatedClientRpc(position);

            GameObject obj = Instantiate(_blackHoleZonePrefab, position, Quaternion.identity);
            NetworkObject netObj = obj.GetComponent<NetworkObject>();
            BlackHoleZone zone = obj.GetComponent<BlackHoleZone>();

            if (netObj == null || zone == null)
            {
                Debug.LogError("[BlackHoleProjectile] zone prefab missing NetworkObject or BlackHoleZone");
                Destroy(obj);
                DespawnProjectile();
                return;
            }

            zone.Initialize(_ownerClientId, _ownerStats, _damageCoefficient, _pullRadius, _pullForce,
                _pullDuration, _pullStrengthRampUp, _explosionRadius);

            netObj.Spawn();
            DespawnProjectile();
        }

        [ClientRpc]
        private void NotifyBlackHoleDetonatedClientRpc(Vector3 position)
        {
            OnBlackHoleDetonated?.Invoke(position);

            if (detonationVfxPrefab != null)
                Instantiate(detonationVfxPrefab, position, Quaternion.identity);
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
            if (collider == null) return false;
            if (collider.GetComponent<PlayerController>() != null) return true;
            if (collider.GetComponentInParent<PlayerController>() != null) return true;
            return false;
        }
    }
}
