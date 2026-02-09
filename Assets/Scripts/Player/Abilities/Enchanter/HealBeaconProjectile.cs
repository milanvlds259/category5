using Unity.Netcode;
using UnityEngine;

namespace Category5
{
    [RequireComponent(typeof(NetworkObject))]
    public class HealBeaconProjectile : NetworkBehaviour
    {
        [Header("Flight Settings")]
        [SerializeField] private float throwSpeed = 12f;
        [SerializeField] private float upwardSpeed = 4f;
        [SerializeField] private float maxLifetime = 5f;
        [SerializeField] private LayerMask groundLayers;

        private ulong _ownerClientId;
        private GameObject _zonePrefab;
        private float _maxDistance;
        private float _healPerTick;
        private float _tickInterval;
        private float _duration;
        private float _radius;
        private Vector3 _startPosition;
        private float _spawnTime;

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public void Initialize(ulong ownerClientId, GameObject zonePrefab, Vector3 direction, float maxDistance,
            float healPerTick, float tickInterval, float duration, float radius)
        {
            _ownerClientId = ownerClientId;
            _zonePrefab = zonePrefab;
            _maxDistance = maxDistance;
            _healPerTick = healPerTick;
            _tickInterval = tickInterval;
            _duration = duration;
            _radius = radius;
            _startPosition = transform.position;
            _spawnTime = Time.time;

            if (_rb != null)
            {
                Vector3 velocity = direction.normalized * throwSpeed + Vector3.up * upwardSpeed;
                _rb.linearVelocity = velocity;
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            if (Time.time - _spawnTime >= maxLifetime)
            {
                SpawnZoneAndDespawn();
                return;
            }

            if (Vector3.Distance(_startPosition, transform.position) >= _maxDistance)
            {
                SpawnZoneAndDespawn();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer) return;

            if (groundLayers.value != 0 && (groundLayers.value & (1 << collision.gameObject.layer)) == 0) return;

            SpawnZoneAndDespawn();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;

            if (groundLayers.value != 0 && (groundLayers.value & (1 << other.gameObject.layer)) == 0) return;

            SpawnZoneAndDespawn();
        }

        private void SpawnZoneAndDespawn()
        {
            if (_zonePrefab == null)
            {
                NetworkObject.Despawn(true);
                return;
            }

            GameObject zoneObj = Instantiate(_zonePrefab, transform.position, Quaternion.identity);
            NetworkObject zoneNet = zoneObj.GetComponent<NetworkObject>();
            HealBeaconZone zone = zoneObj.GetComponent<HealBeaconZone>();

            if (zoneNet == null || zone == null)
            {
                Destroy(zoneObj);
                NetworkObject.Despawn(true);
                return;
            }

            zone.Initialize(_ownerClientId, _healPerTick, _tickInterval, _duration, _radius);
            zoneNet.Spawn();
            zone.NotifySpawned();

            NetworkObject.Despawn(true);
        }
    }
}
