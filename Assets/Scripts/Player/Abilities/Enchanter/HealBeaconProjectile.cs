using Unity.Netcode;
using UnityEngine;

namespace Category5
{
    [RequireComponent(typeof(NetworkObject))]
    public class HealBeaconProjectile : NetworkBehaviour
    {
        [Header("Flight Settings")]
        [SerializeField][Tooltip("horizontal speed of the throw - closer targets land faster, farther ones take proportionally longer")]
        private float throwSpeed = 15f;
        [SerializeField] private float maxLifetime = 5f;
        [SerializeField] private LayerMask groundLayers;

        private ulong _ownerClientId;
        private GameObject _zonePrefab;
        private float _healPerTick;
        private float _tickInterval;
        private float _duration;
        private float _radius;
        private float _spawnTime;
        private bool _hasSpawned = false;

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            // force trigger so the beacon passes through enemies/bosses with no physics bounce
            // onTriggerEnter handles ground landing, rigidbody gravity still arcs the throw naturally
            var col = GetComponent<Collider>();
            if (col == null) col = GetComponentInChildren<Collider>();
            if (col != null) col.isTrigger = true;
        }

        public void Initialize(ulong ownerClientId, GameObject zonePrefab, Vector3 targetPosition,
            float healPerTick, float tickInterval, float duration, float radius)
        {
            _ownerClientId = ownerClientId;
            _zonePrefab = zonePrefab;
            _healPerTick = healPerTick;
            _tickInterval = tickInterval;
            _duration = duration;
            _radius = radius;
            _spawnTime = Time.time;

            if (_rb != null)
            {
                _rb.linearVelocity = CalculateArcVelocity(transform.position, targetPosition);
            }
        }

        // launches at a fixed horizontal speed so flight time scales with distance -> close = fast, far = slower
        private Vector3 CalculateArcVelocity(Vector3 from, Vector3 to)
        {
            float g = Mathf.Abs(Physics.gravity.y);
            Vector3 horizontal = to - from;
            horizontal.y = 0f;
            float d = horizontal.magnitude;

            // flight time is d / speed, so shorter throws land faster
            float totalTime = Mathf.Max(0.1f, d / throwSpeed);

            // upward velocity needed to arc over and land exactly at to.y
            float deltaY = to.y - from.y;
            float upwardVel = (deltaY + 0.5f * g * totalTime * totalTime) / totalTime;

            Vector3 horizontalVel = d > 0.01f ? horizontal.normalized * throwSpeed : Vector3.zero;
            return horizontalVel + Vector3.up * upwardVel;
        }

        private void Update()
        {
            if (!IsServer || _hasSpawned) return;

            if (Time.time - _spawnTime >= maxLifetime)
            {
                // safety fallback in case the beacon missed the ground collider
                TrySpawnZoneOnGround();
                if (!_hasSpawned) NetworkObject.Despawn(true);
                return;
            }

            // fast-moving physics objects can tunnel through colliders without firing OnTriggerEnter
            // while falling, do a short proximity raycast each frame as a reliable landing check
            if (_rb != null && _rb.linearVelocity.y < 0f)
            {
                CheckGroundProximity();
            }
        }

        // short-range downward check while descending it catches tunneling cases OnTriggerEnter misses
        private void CheckGroundProximity()
        {
            Ray downRay = new Ray(transform.position + Vector3.up * 0.1f, Vector3.down);
            bool hasMask = groundLayers.value != 0;
            bool hitGround = hasMask
                ? Physics.Raycast(downRay, out RaycastHit hit, 0.6f, groundLayers)
                : Physics.Raycast(downRay, out hit, 0.6f);

            if (!hitGround) return;

            transform.position = hit.point;
            SpawnZoneAndDespawn();
        }

        private void TrySpawnZoneOnGround()
        {
            Ray downRay = new Ray(transform.position + Vector3.up * 0.25f, Vector3.down);
            bool hasMask = groundLayers.value != 0;
            bool hitGround = hasMask
                ? Physics.Raycast(downRay, out RaycastHit hit, 10f, groundLayers)
                : Physics.Raycast(downRay, out hit, 10f);

            if (!hitGround) return;

            transform.position = hit.point;
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
            if (_hasSpawned) return;
            _hasSpawned = true;

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
