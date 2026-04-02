using UnityEngine;
using Category5.Player;

namespace Category5
{
    // elementalist ice e ability - long-range thin skillshot that pierces enemies, damages and slows
    public class ElementalistE_Ice : AbilityBase
    {
        [Header("ice lance settings")]
        [SerializeField] private float projectileSpeed = 22f;
        [SerializeField] private float projectileLifetime = 5f;
        [SerializeField] private float slowMultiplier = 0.5f;
        [SerializeField] private float slowDuration = 3f;

        [Header("spawn")]
        [SerializeField] private float spawnForwardOffset = 0f;

        // reference to ice projectile prefab (must be registered in networkmanager)
        [Header("prefab")]
        [SerializeField] private GameObject iceProjectilePrefab;

        // events for vfx/sfx hooks
        public static event System.Action<Vector3, Vector3> OnIceLanceLaunched;

        public override void Execute()
        {
            Vector3 spawnPos = playerController.transform.position
                + playerController.transform.forward * spawnForwardOffset;

            Vector3 direction = GetFlatForwardDirection();

            // Debug.Log($"[ElementalistE_Ice] launching ice lance from {spawnPos} toward {direction}");

            OnIceLanceLaunched?.Invoke(spawnPos, direction);
            SpawnVfx(spawnPos);
            PlayAudio(spawnPos);

            // request server to spawn the ice projectile
            abilityManager.SpawnIceProjectileServerRpc(
                spawnPos, direction, abilityData.damageCoefficient, projectileSpeed,
                projectileLifetime, slowMultiplier, slowDuration
            );
        }

        private Vector3 GetFlatForwardDirection()
        {
            Vector3 forward = playerController.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }
            return forward.normalized;
        }

        // gizmos
        private void OnDrawGizmosSelected()
        {
            if (playerController == null) return;

            Vector3 spawnPos = playerController.transform.position
                + playerController.transform.forward * spawnForwardOffset;

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(spawnPos, 0.1f);
            Gizmos.DrawRay(spawnPos, GetFlatForwardDirection() * 15f);
        }
    }
}
