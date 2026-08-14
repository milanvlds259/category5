using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Player;
using Category5.Enemies;
using Category5.Boss;

namespace Category5
{
    // fireball projectile that travels forward and explodes in an aoe on impact
    // applies burn effect to all enemies hit
    // spawned by server only, syncs position via networktransform
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class FireballProjectile : NetworkBehaviour
    {
        [Header("projectile settings")]
        [SerializeField] private float speed = 18f;
        private float _damageCoefficient = 1f;
        [SerializeField] private float lifetime = 4f;

        [Header("aoe explosion")]
        [SerializeField] private float explosionRadius = 4f;

        [Header("burn effect")]
        [SerializeField] private float burnDamageCoeffPerTick = 0.5f;
        [SerializeField] private float burnTickInterval = 0.5f;
        [SerializeField] private float burnDuration = 3f;

        [Header("detonation")]
        [SerializeField] private LayerMask detonationLayers = ~0;

        [Header("vfx")]
        [Tooltip("spawned once when the fireball explodes")]
        [SerializeField] private GameObject explosionVfxPrefab;

        [Header("debug")]
        [SerializeField] private bool showDebugExplosionSphere = true;
        [SerializeField] private float debugSphereDuration = 0.75f;
        [SerializeField] private Color debugSphereColor = new Color(0.12f, 0.06f, 0.02f, 0.18f);

        private ulong _ownerClientId;
        private PlayerStats _ownerStats;
        private bool _hasExploded;
        private Rigidbody _rigidbody;
        private float _castRadius = 0.2f;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = false;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // ensure collider is trigger
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

        // initialize fireball (called on server before spawn)
        public void Initialize(ulong ownerClientId, PlayerStats ownerStats, float damageCoefficient, float projectileSpeed,
            float projectileLifetime, float aoeRadius, float burnDmg, float burnInterval, float burnDur)
        {
            _ownerClientId = ownerClientId;
            _ownerStats = ownerStats;
            _damageCoefficient = damageCoefficient;
            speed = projectileSpeed;
            lifetime = projectileLifetime;
            explosionRadius = aoeRadius;
            burnDamageCoeffPerTick = burnDmg;
            burnTickInterval = burnInterval;
            burnDuration = burnDur;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            if (_hasExploded) return;

            // ignore players (no friendly fire)
            if (IsPlayerCollider(other)) return;

            // explode on any collision (enemy, boss, or environment)
            _hasExploded = true;
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Explode(hitPoint);
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            if (_hasExploded) return;

            Vector3 velocity = _rigidbody.linearVelocity;
            if (velocity.sqrMagnitude < 0.001f) return;

            Vector3 dir = velocity.normalized;
            float distance = velocity.magnitude * Time.fixedDeltaTime;

            if (Physics.SphereCast(transform.position, _castRadius, dir, out RaycastHit hit, distance, detonationLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsPlayerCollider(hit.collider)) return;

                _hasExploded = true;
                Explode(hit.point);
            }
        }

        private void Explode(Vector3 position)
        {
            // Debug.Log($"[FireballProjectile] exploding at {position} with radius {explosionRadius}");

            if (showDebugExplosionSphere)
            {
                CreateDebugExplosionSphere(position);
            }

            // find all colliders in explosion radius
            Collider[] hits = Physics.OverlapSphere(position, explosionRadius);
            int enemiesHit = 0;
            var processed = new System.Collections.Generic.HashSet<int>();

            foreach (Collider col in hits)
            {
                // try enemy
                EnemyBase enemy = col.GetComponent<EnemyBase>();
                if (enemy == null) enemy = col.GetComponentInParent<EnemyBase>();

                if (enemy != null && !enemy.IsDead)
                {
                    int instanceId = enemy.GetInstanceID();
                    if (processed.Contains(instanceId)) continue;
                    processed.Add(instanceId);

                    int finalDamage = _ownerStats != null ? _ownerStats.CalculateDamage(_damageCoefficient).damage : Mathf.RoundToInt(_damageCoefficient * 100f);
                    enemy.TakeDamage(finalDamage);
                    ApplyBurn(enemy.gameObject);

                    // show damage number to attacking player
                    ShowDamageNumberClientRpc(finalDamage, enemy.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { _ownerClientId } }
                    });
                    enemiesHit++;
                    continue;
                }

                // try boss
                BossBase boss = col.GetComponent<BossBase>();
                if (boss == null) boss = col.GetComponentInParent<BossBase>();

                if (boss != null)
                {
                    int instanceId = boss.GetInstanceID();
                    if (processed.Contains(instanceId)) continue;
                    processed.Add(instanceId);

                    int finalDamage = _ownerStats != null ? _ownerStats.CalculateDamage(_damageCoefficient).damage : Mathf.RoundToInt(_damageCoefficient * 100f);
                    boss.TakeDamage(finalDamage);
                    ApplyBurn(boss.gameObject);

                    ShowDamageNumberClientRpc(finalDamage, boss.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { _ownerClientId } }
                    });
                    enemiesHit++;
                }
            }

            // apply lifesteal
            int lifestealAmount = _ownerStats != null ? _ownerStats.LifestealAmount : 0;
            if (lifestealAmount > 0 && enemiesHit > 0)
            {
                ApplyLifestealToOwner(lifestealAmount);
            }

            // notify all clients for vfx/sfx
            FireballExplodeClientRpc(position, enemiesHit);

            // trigger hit feedback for the attacker
            if (enemiesHit > 0)
            {
                TriggerHitFeedbackClientRpc(position, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { _ownerClientId } }
                });
            }

            DespawnProjectile();
        }

        private void CreateDebugExplosionSphere(Vector3 position)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "fireball_debug_explosion";
            sphere.transform.position = position;
            sphere.transform.localScale = Vector3.one * explosionRadius * 2f;

            Collider col = sphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = CreateDebugMaterial();
                if (mat != null)
                {
                    mat.color = debugSphereColor;
                    renderer.material = mat;
                }
            }

            Destroy(sphere, debugSphereDuration);
        }

        private Material CreateDebugMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("HDRP/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("[FireballProjectile] no suitable unlit shader found for debug sphere");
                return null;
            }

            Material mat = new Material(shader);

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.renderQueue = 3000;
            }
            else if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }

            return mat;
        }

        private void ApplyBurn(GameObject target)
        {
            // check if already burning, refresh if so
            BurnEffect existing = target.GetComponent<BurnEffect>();
            if (existing != null)
            {
                existing.Refresh(burnDamageCoeffPerTick, burnTickInterval, burnDuration);
            }
            else
            {
                BurnEffect burn = target.AddComponent<BurnEffect>();
                burn.Initialize(burnDamageCoeffPerTick, burnTickInterval, burnDuration, _ownerClientId, _ownerStats);
            }
        }

        private void ApplyLifestealToOwner(int healAmount)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(_ownerClientId, out var client))
            {
                var pc = client.PlayerObject?.GetComponent<PlayerController>();
                if (pc != null) pc.Heal(healAmount);
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

        private static bool IsPlayerCollider(Collider collider)
        {
            if (collider == null) return false;
            if (collider.GetComponent<PlayerController>() != null) return true;
            if (collider.GetComponentInParent<PlayerController>() != null) return true;
            return false;
        }

        // events for vfx/sfx hooks
        public static event System.Action<Vector3, int> OnFireballExplode;

        [ClientRpc]
        private void FireballExplodeClientRpc(Vector3 position, int enemiesHit)
        {
            OnFireballExplode?.Invoke(position, enemiesHit);

            if (explosionVfxPrefab != null)
                Instantiate(explosionVfxPrefab, position, Quaternion.identity);
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
                HitFeedbackManager.Instance.TriggerHeavyHit(position);
            }
        }

        // gizmos for debugging explosion radius
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
