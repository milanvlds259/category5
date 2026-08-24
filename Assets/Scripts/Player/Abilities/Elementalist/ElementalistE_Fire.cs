using UnityEngine;
using Category5.Player;

namespace Category5
{
    // elementalist fire e ability - mid-range fireball projectile that explodes in aoe and applies burn
    public class ElementalistE_Fire : AbilityBase
    {
        [Header("fireball settings")]
        [SerializeField] private float projectileSpeed = 18f;
        [SerializeField] private float projectileLifetime = 4f;
        [SerializeField] private float explosionRadius = 4f;

        [Header("burn settings")]
        [SerializeField] private float burnDamageCoeffPerTick = 0.5f;
        [SerializeField] private float burnTickInterval = 0.5f;
        [SerializeField] private float burnDuration = 3f;

        [Header("spawn")]
        [SerializeField] private float spawnHeightOffset = 1.5f;
        [SerializeField] private float spawnForwardOffset = 0.5f;

        // reference to fireball prefab (set in inspector, must be registered in networkmanager)
        [Header("prefab")]
        [SerializeField] private GameObject fireballPrefab;

        [Header("vfx")]
        [Tooltip("spawned once when the fireball is launched")]
        [SerializeField] private GameObject castVfxPrefab;

        // events for vfx/sfx hooks
        public static event System.Action<Vector3, Vector3> OnFireballLaunched;

        // plays a cast animation and fires on the CastImpact animation event
        public override bool HasCastAnimation => true;

        // can be held to aim a fireball before firing
        public override bool CanHoldToAim => true;

        public override void Execute()
        {
            Vector3 spawnPos = GetSpawnPosition();

            Vector3 direction = GetAimDirection(spawnPos);

            // Debug.Log($"[ElementalistE_Fire] launching fireball from {spawnPos} toward {direction}");

            // fire event for local vfx/sfx
            OnFireballLaunched?.Invoke(spawnPos, direction);
            SpawnVfx(spawnPos);
            PlayAudio(spawnPos);

            // request server to spawn the fireball
            abilityManager.SpawnFireballServerRpc(
                spawnPos, direction, abilityData.damageCoefficient, projectileSpeed, projectileLifetime,
                explosionRadius, burnDamageCoeffPerTick, burnTickInterval, burnDuration
            );
        }

        // spawns from the model's projectile spawn point (hand), falling back to the old offsets
        private Vector3 GetSpawnPosition()
        {
            Transform spawnPoint = GetProjectileSpawnPoint();
            if (spawnPoint != null)
            {
                return spawnPoint.position;
            }
            return playerController.transform.position
                + Vector3.up * spawnHeightOffset
                + playerController.transform.forward * spawnForwardOffset;
        }

        // screen-center raycast - same as the base default but overrideable for clarity
        public override Vector3 GetAimDirection(Vector3 spawnPos)
        {
            if (Camera.main == null)
                return playerController.transform.forward;

            Ray aimRay = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(aimRay, out RaycastHit hit, 100f))
                return (hit.point - spawnPos).normalized;

            return (aimRay.GetPoint(100f) - spawnPos).normalized;
        }

        // gizmos
        private void OnDrawGizmosSelected()
        {
            if (playerController == null) return;

            Vector3 spawnPos = playerController.transform.position
                + Vector3.up * spawnHeightOffset
                + playerController.transform.forward * spawnForwardOffset;

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(spawnPos, 0.15f);
            Gizmos.DrawRay(spawnPos, playerController.transform.forward * 5f);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(spawnPos + playerController.transform.forward * 5f, explosionRadius);
        }
    }
}
