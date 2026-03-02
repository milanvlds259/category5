using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Category5.Core;
using Category5.Player;
using Category5.PowerUps;
using Category5.Enemies;
using Category5.Boss;

namespace Category5
{
    // black hole zone that pulls enemies inward then explodes for massive aoe damage
    // spawned by server, syncs via networktransform
    [RequireComponent(typeof(NetworkObject))]
    public class BlackHoleZone : NetworkBehaviour
    {
        [Header("pull phase")]
        [SerializeField] private float pullRadius = 8f;
        [SerializeField] private float pullForce = 8f;
        [SerializeField] private float pullDuration = 3f;
        [SerializeField] private float pullStrengthRampUp = 2f; // multiplier at end of pull phase

        [Header("stability")]
        [SerializeField] private bool lockPosition = true;

        [Header("debug")]
        [SerializeField] private bool showDebugPullSphere = true;
        [SerializeField] private Color debugSphereColor = new Color(0.08f, 0.05f, 0.16f, 0.18f);

        [Header("explosion phase")]
        [SerializeField] private int explosionDamage = 60;
        [SerializeField] private float explosionRadius = 8f;

        private ulong _ownerClientId;
        private PlayerStats _ownerStats;
        private float _elapsedTime;
        private bool _hasExploded;
        private Vector3 _spawnPosition;
        private Rigidbody _rigidbody;
        private GameObject _debugSphere;

        // events for vfx/sfx hooks
        public static event System.Action<Vector3, float> OnBlackHoleSpawned;
        public static event System.Action<Vector3, float, float> OnBlackHolePulling; // position, pullStrength, progress
        public static event System.Action<Vector3, int> OnBlackHoleExploded;

        // initialize (called on server before spawn)
        public void Initialize(ulong ownerClientId, PlayerStats ownerStats, int baseDamage,
            float radius, float force, float duration, float ramp, float explRadius)
        {
            _ownerClientId = ownerClientId;
            _ownerStats = ownerStats;
            explosionDamage = baseDamage;
            pullRadius = radius;
            pullForce = force;
            pullDuration = duration;
            pullStrengthRampUp = ramp;
            explosionRadius = explRadius;
        }

        public override void OnNetworkSpawn()
        {
            _spawnPosition = transform.position;

            // prevent any physics or external forces from moving the zone
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null)
            {
                _rigidbody.useGravity = false;
                _rigidbody.isKinematic = true;
            }

            // notify all clients of spawn for vfx
            NotifySpawnedClientRpc(transform.position, pullRadius);

            if (showDebugPullSphere)
            {
                CreateDebugSphere();
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            if (_hasExploded) return;

            if (lockPosition && transform.position != _spawnPosition)
            {
                transform.position = _spawnPosition;
            }

            _elapsedTime += Time.fixedDeltaTime;

            float progress = Mathf.Clamp01(_elapsedTime / pullDuration);

            // pull phase: pull enemies toward center with increasing strength
            float currentPullStrength = Mathf.Lerp(1f, pullStrengthRampUp, progress);
            PullEnemies(currentPullStrength);

            // notify clients of pull progress for vfx
            if (Time.frameCount % 5 == 0) // throttle to every 5 physics frames
            {
                NotifyPullingClientRpc(transform.position, currentPullStrength, progress);
            }

            // explode when pull duration is over
            if (_elapsedTime >= pullDuration)
            {
                Explode();
            }
        }

        private void PullEnemies(float strengthMultiplier)
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, pullRadius);

            foreach (Collider col in colliders)
            {
                // pull enemies
                EnemyBase enemy = col.GetComponent<EnemyBase>();
                if (enemy == null) enemy = col.GetComponentInParent<EnemyBase>();

                if (enemy != null && !enemy.IsDead)
                {
                    Vector3 dirToCenter = (transform.position - enemy.transform.position).normalized;
                    float distance = Vector3.Distance(transform.position, enemy.transform.position);

                    // stronger pull when closer (inverse distance falloff capped)
                    float distanceFactor = Mathf.Clamp01(1f - (distance / pullRadius));
                    Vector3 pullVector = dirToCenter * pullForce * strengthMultiplier * (0.5f + distanceFactor * 0.5f);

                    // use direct movement since enemies use kinematic rigidbodies
                    CharacterController enemyCC = enemy.GetComponent<CharacterController>();
                    if (enemyCC != null)
                    {
                        enemyCC.Move(pullVector * Time.fixedDeltaTime);
                    }
                    else
                    {
                        enemy.transform.position += pullVector * Time.fixedDeltaTime;
                    }
                    continue;
                }

                // pull boss using character controller directly (bosses don't have ApplyKnockback)
                BossBase boss = col.GetComponent<BossBase>();
                if (boss == null) boss = col.GetComponentInParent<BossBase>();

                if (boss != null)
                {
                    Vector3 dirToCenter = (transform.position - boss.transform.position).normalized;
                    Vector3 pullVector = dirToCenter * pullForce * strengthMultiplier * 0.3f; // bosses resist pull

                    // move boss via rigidbody MovePosition for
                    Rigidbody bossRb = boss.GetComponent<Rigidbody>();
                    if (bossRb != null)
                    {
                        bossRb.MovePosition(bossRb.position + pullVector * Time.fixedDeltaTime);
                    }
                    else
                    {
                        boss.transform.position += pullVector * Time.fixedDeltaTime;
                    }
                }
            }
        }

        private void Explode()
        {
            if (_hasExploded) return;
            _hasExploded = true;

            Debug.Log($"[BlackHoleZone] exploding at {transform.position} with radius {explosionRadius}");

            Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
            int enemiesHit = 0;

            foreach (Collider col in hits)
            {
                EnemyBase enemy = col.GetComponent<EnemyBase>();
                if (enemy == null) enemy = col.GetComponentInParent<EnemyBase>();

                if (enemy != null && !enemy.IsDead)
                {
                    int finalDamage = _ownerStats != null ? _ownerStats.CalculateDamage(explosionDamage) : explosionDamage;
                    enemy.TakeDamage(finalDamage);

                    ShowDamageNumberClientRpc(finalDamage, enemy.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { _ownerClientId } }
                    });
                    enemiesHit++;
                    continue;
                }

                BossBase boss = col.GetComponent<BossBase>();
                if (boss == null) boss = col.GetComponentInParent<BossBase>();

                if (boss != null)
                {
                    int finalDamage = _ownerStats != null ? _ownerStats.CalculateDamage(explosionDamage) : explosionDamage;
                    boss.TakeDamage(finalDamage);

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
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(_ownerClientId, out var client))
                {
                    var pc = client.PlayerObject?.GetComponent<PlayerController>();
                    if (pc != null) pc.Heal(lifestealAmount);
                }
            }

            // notify clients for explosion vfx
            NotifyExplosionClientRpc(transform.position, enemiesHit);

            // trigger hit feedback for owner
            if (enemiesHit > 0)
            {
                TriggerHitFeedbackClientRpc(transform.position, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { _ownerClientId } }
                });
            }

            // despawn after short delay to allow clients to play explosion vfx
            Invoke(nameof(DespawnZone), 0.5f);
        }

        private void DespawnZone()
        {
            if (!IsServer) return;
            DestroyDebugSphere();
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        private void CreateDebugSphere()
        {
            if (_debugSphere != null) return;

            _debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _debugSphere.name = "black_hole_debug_sphere";
            _debugSphere.transform.SetParent(transform, false);
            _debugSphere.transform.localPosition = Vector3.zero;
            _debugSphere.transform.localScale = Vector3.one * pullRadius * 2f;

            Collider col = _debugSphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer renderer = _debugSphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = CreateDebugMaterial();
                if (mat != null)
                {
                    mat.color = debugSphereColor;
                    renderer.material = mat;
                }
            }
        }


		// THIS IS ONLY FOR DEBUG IGNORE THIS CODE I WILL DELETE LATER
        private Material CreateDebugMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("HDRP/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("[BlackHoleZone] no suitable unlit shader found for debug sphere");
                return null;
            }

            Material mat = new Material(shader);

            // try to force transparent rendering for common shaders
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f); // transparent
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

        private void DestroyDebugSphere()
        {
            if (_debugSphere == null) return;
            Destroy(_debugSphere);
            _debugSphere = null;
        }

        [ClientRpc]
        private void NotifySpawnedClientRpc(Vector3 position, float radius)
        {
            OnBlackHoleSpawned?.Invoke(position, radius);
            Debug.Log($"[BlackHoleZone] spawned at {position}");
        }

        [ClientRpc]
        private void NotifyPullingClientRpc(Vector3 position, float strength, float progress)
        {
            OnBlackHolePulling?.Invoke(position, strength, progress);
        }

        [ClientRpc]
        private void NotifyExplosionClientRpc(Vector3 position, int enemiesHit)
        {
            OnBlackHoleExploded?.Invoke(position, enemiesHit);
            Debug.Log($"[BlackHoleZone] explosion vfx at {position}, hit {enemiesHit}");
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

        // gizmos
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, pullRadius);

            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
