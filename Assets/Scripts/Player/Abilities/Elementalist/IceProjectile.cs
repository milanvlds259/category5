using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Player;
using Category5.Enemies;
using Category5.Boss;

namespace Category5
{
    // ice projectile that travels in a line, pierces through all enemies, deals damage and applies slow
    // spawned by server only, syncs position via networktransform
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class IceProjectile : NetworkBehaviour
    {
        [Header("projectile settings")]
        [SerializeField] private float speed = 22f;
        private float _damageCoefficient = 1f;
        [SerializeField] private float lifetime = 5f;

        [Header("slow settings")]
        [SerializeField] private float slowMultiplier = 0.5f;
        [SerializeField] private float slowDuration = 3f;

        private ulong _ownerClientId;
        private PlayerStats _ownerStats;
        private Rigidbody _rigidbody;

        // track which enemies we already hit so we don't double-hit on same pass
        private System.Collections.Generic.HashSet<int> _hitInstanceIds = new();

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = false;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var col = GetComponentInChildren<Collider>();
            if (col != null) col.isTrigger = true;
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

        // initialize ice projectile (called on server before spawn)
        public void Initialize(ulong ownerClientId, PlayerStats ownerStats, float damageCoefficient,
            float projectileSpeed, float projectileLifetime, float slowMult, float slowDur)
        {
            _ownerClientId = ownerClientId;
            _ownerStats = ownerStats;
            _damageCoefficient = damageCoefficient;
            speed = projectileSpeed;
            lifetime = projectileLifetime;
            slowMultiplier = slowMult;
            slowDuration = slowDur;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;

            // ignore players
            if (other.GetComponent<PlayerController>() != null) return;
            if (other.GetComponentInParent<PlayerController>() != null) return;

            // check for damageable targets
            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null) damageable = other.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                GameObject targetObj = (damageable as MonoBehaviour)?.gameObject;
                if (targetObj == null) return;

                // skip if we already hit this target
                int instanceId = targetObj.GetInstanceID();
                if (_hitInstanceIds.Contains(instanceId)) return;
                _hitInstanceIds.Add(instanceId);

                int finalDamage = _ownerStats != null ? _ownerStats.CalculateDamage(_damageCoefficient).damage : Mathf.RoundToInt(_damageCoefficient * 100f);
                damageable.TakeDamage(finalDamage);

                // apply slow to enemies
                EnemyBase enemy = targetObj.GetComponent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    enemy.ApplyMovementModifier(slowMultiplier, slowDuration, "elementalist_ice");
                }

                // apply slow to boss
                BossBase boss = targetObj.GetComponent<BossBase>();
                if (boss != null)
                {
                    // bosses don't have ApplyMovementModifier yet, but damage still applies
                    Debug.Log("[IceProjectile] hit boss, slow not applied (not implemented on bosses)");
                }

                // show damage number to attacking player
                ShowDamageNumberClientRpc(finalDamage, targetObj.transform.position, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { _ownerClientId } }
                });

                // notify all clients for vfx
                IceHitClientRpc(targetObj.transform.position);

                // trigger hit feedback
                TriggerHitFeedbackClientRpc(targetObj.transform.position, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { _ownerClientId } }
                });

                // apply lifesteal
                int lifestealAmount = _ownerStats != null ? _ownerStats.LifestealAmount : 0;
                if (lifestealAmount > 0)
                {
                    if (NetworkManager.Singleton.ConnectedClients.TryGetValue(_ownerClientId, out var client))
                    {
                        var pc = client.PlayerObject?.GetComponent<PlayerController>();
                        if (pc != null) pc.Heal(lifestealAmount);
                    }
                }

                Debug.Log($"[IceProjectile] hit {targetObj.name} for {finalDamage} damage, applied slow");

                // pierce through - do NOT stop
                return;
            }

            // hit environment - stop (ice lance shatters on walls)
            Debug.Log($"[IceProjectile] hit environment {other.gameObject.name}, despawning");
            NotifyIceShatterClientRpc(transform.position);
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

        // events for vfx/sfx hooks
        public static event System.Action<Vector3> OnIceHit;
        public static event System.Action<Vector3> OnIceShatter;

        [ClientRpc]
        private void IceHitClientRpc(Vector3 position)
        {
            OnIceHit?.Invoke(position);
        }

        [ClientRpc]
        private void NotifyIceShatterClientRpc(Vector3 position)
        {
            OnIceShatter?.Invoke(position);
        }

        [ClientRpc]
        private void ShowDamageNumberClientRpc(int damageAmount, Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            if (Category5.UI.UIManager.Instance != null)
            {
                Category5.UI.UIManager.Instance.ShowDamageNumber(damageAmount, position);
            }
        }

        [ClientRpc]
        private void TriggerHitFeedbackClientRpc(Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            if (HitFeedbackManager.Instance != null)
            {
                HitFeedbackManager.Instance.TriggerLightHit(position);
            }
        }

        // gizmos
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.forward * 10f);
        }
    }
}
