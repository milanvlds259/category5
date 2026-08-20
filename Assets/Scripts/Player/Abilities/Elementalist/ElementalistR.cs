using UnityEngine;
using Category5.Player;

namespace Category5
{
    // elementalist r ability - throws a black hole that pulls enemies and explodes for aoe damage
    public class ElementalistR : AbilityBase
    {
        [Header("black hole settings")]
        [SerializeField] private float castRange = 15f;
        [SerializeField] private float projectileSpeed = 14f;
        [SerializeField] private float projectileLifetime = 5f;
        [SerializeField] private float pullRadius = 8f;
        [SerializeField] private float pullForce = 8f;
        [SerializeField] private float pullDuration = 3f;
        [SerializeField] private float pullStrengthRampUp = 2f;
        [SerializeField] private float explosionRadius = 8f;

        [Header("spawn")]
        [SerializeField] private float spawnHeightOffset = 1.5f;
        [SerializeField] private float spawnForwardOffset = 0.5f;

        [Header("vfx")]
        [Tooltip("spawned once when the black hole projectile is launched")]
        [SerializeField] private GameObject castVfxPrefab;

        // events for vfx/sfx hooks
        public static event System.Action<Vector3> OnBlackHoleCast;

        // plays a cast animation and fires on the CastImpact animation event
        public override bool HasCastAnimation => true;

        // can be held to aim the black hole before firing
        public override bool CanHoldToAim => true;

        public override void Execute()
        {
            Vector3 spawnPos = GetSpawnPosition();

            Vector3 direction = GetAimDirection(spawnPos);

            // Debug.Log($"[ElementalistR] launching black hole projectile from {spawnPos} toward {direction}");

            OnBlackHoleCast?.Invoke(spawnPos);
            SpawnVfx(spawnPos);
            PlayAudio(spawnPos);

            if (castVfxPrefab != null)
                Instantiate(castVfxPrefab, spawnPos, Quaternion.identity);

            // request server to spawn the black hole projectile
            abilityManager.SpawnBlackHoleProjectileServerRpc(
                spawnPos, direction, abilityData.damageCoefficient, projectileSpeed, projectileLifetime,
                pullRadius, pullForce, pullDuration, pullStrengthRampUp, explosionRadius
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

        // screen-center raycast capped at castRange (public override for hold-to-aim)
        public override Vector3 GetAimDirection(Vector3 spawnPos)
        {
            if (Camera.main == null)
            {
                return playerController.transform.forward;
            }

            Camera cam = Camera.main;
            Ray aimRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(aimRay, out RaycastHit hit, castRange))
            {
                return (hit.point - spawnPos).normalized;
            }

            // if nothing hit, aim along the ray direction
            return (aimRay.GetPoint(castRange) - spawnPos).normalized;
        }

        // gizmos
        private void OnDrawGizmosSelected()
        {
            if (playerController == null) return;

            Vector3 spawnPos = playerController.transform.position
                + Vector3.up * spawnHeightOffset
                + playerController.transform.forward * spawnForwardOffset;

            Vector3 targetPos = spawnPos + playerController.transform.forward * castRange;

            // draw cast range
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(playerController.transform.position, targetPos);

            // draw pull/explosion radius at target
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f);
            Gizmos.DrawWireSphere(targetPos, pullRadius);

            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(targetPos, explosionRadius);
        }
    }
}
