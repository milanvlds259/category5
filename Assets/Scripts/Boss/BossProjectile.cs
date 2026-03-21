using UnityEngine;
using Unity.Netcode;
using Category5.Player;
using Category5.Core;

namespace Category5.Boss
{
    // server-authoritative projectile fired by the boss
    // simpler than NetworkedProjectile - no player stats, lifesteal, or damage number targeting
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class BossProjectile : NetworkBehaviour
    {
        // runtime data set by TestBoss before Spawn() is called
        private float _speed;
        private int _damage;
        private float _lifetime;
        private bool _hasHit = false;

        // optional vfx to spawn on impact (assigned via Initialize)
        private GameObject _impactVfxPrefab;

        // cached rigidbody
        private Rigidbody _rb;

        // how close the projectile center needs to be to a player's capsule center to count as a hit
        // tunable on the prefab — increase if hits feel like they're missing
        [SerializeField] private float hitRadius = 0.6f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            // configure physics for a fast projectile
            _rb.useGravity = false;
            _rb.isKinematic = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // make sure the collider (on this object or child) is a trigger
            var col = GetComponentInChildren<Collider>();
            if (col != null)
                col.isTrigger = true;
            else
                Debug.LogWarning("BossProjectile: no collider found — add a trigger collider to this prefab");
        }

        // called by TestBoss on the server right before Spawn()
        public void Initialize(float speed, int damage, float lifetime, GameObject impactVfxPrefab = null)
        {
            _speed = speed;
            _damage = damage;
            _lifetime = lifetime;
            _impactVfxPrefab = impactVfxPrefab;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // launch the projectile and auto-despawn after lifetime
                _rb.linearVelocity = transform.forward * _speed;
                Invoke(nameof(DespawnProjectile), _lifetime);
            }
            else
            {
                // clients follow via NetworkTransform — no physics needed
                _rb.isKinematic = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            if (_hasHit) return;

            // don't hit the boss itself
            if (other.GetComponentInParent<BossBase>() != null) return;

            // players are handled in Update via direct distance check — CharacterController doesn't
            // fire trigger events reliably on the server when moved by NetworkTransform sync
            if (other.GetComponent<PlayerController>() != null
             || other.GetComponentInParent<PlayerController>() != null)
                return;

            // hit a wall or other environment object — stop the projectile
            _hasHit = true;
            NotifyHitClientRpc(transform.position);
            DespawnProjectile();
        }

        // check distance to each live player every frame on the server — same approach as
        // TestBoss.CheckMeleeHits(), bypasses CharacterController physics sync issues entirely
        private void Update()
        {
            if (!IsServer) return;
            if (_hasHit) return;
            if (NetworkManager == null || NetworkManager.Singleton == null) return;

            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
                var player = client.PlayerObject?.GetComponent<PlayerController>();
                if (player == null || player.IsDead.Value) continue;

                // check against the CharacterController bounds center for accurate hit detection
                var cc = player.GetComponent<CharacterController>();
                Vector3 playerCenter = cc != null ? cc.bounds.center : player.transform.position + Vector3.up;

                if (Vector3.Distance(transform.position, playerCenter) < hitRadius)
                {
                    _hasHit = true;
                    player.TakeDamage(_damage);
                    HitFeedbackManager.Instance?.TriggerPlayerDamaged(player.transform.position);
                    NotifyHitClientRpc(player.transform.position);
                    DespawnProjectile();
                    return;
                }
            }
        }

        // broadcast to all clients so artists can spawn impact vfx
        [ClientRpc]
        private void NotifyHitClientRpc(Vector3 hitPosition)
        {
            if (_impactVfxPrefab != null)
                Instantiate(_impactVfxPrefab, hitPosition, Quaternion.identity);
        }

        private void DespawnProjectile()
        {
            if (!IsServer) return;
            CancelInvoke(nameof(DespawnProjectile));
            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
        }
    }
}
