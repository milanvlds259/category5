using UnityEngine;
using System;
using Category5.Player;
using Category5.Core;

namespace Category5
{

    // enchanter e - throw a heal beacon that heals allies over time
    public class EnchanterE : AbilityBase
    {
        [Header("Throw Settings")]
        [SerializeField] private float maxThrowDistance = 20f;
        [SerializeField] private float forwardOffset = 0.8f;
        [SerializeField] private float upwardOffset = 1.2f;

        [Header("Heal Settings")]
        [SerializeField] private float healPerTick = 10f;
        [SerializeField] private float tickInterval = 1f;
        [SerializeField] private float baseDuration = 3f;
        [SerializeField] private float durationPerCharge = 1.5f;
        [SerializeField] private float healRadius = 6f;

        public static event Action<Vector3, Vector3> OnBeaconThrown;

        public override void Execute()
        {
            if (!CanUse()) return;

            Vector3 forward = playerController != null ? playerController.transform.forward : transform.forward;
            Vector3 spawnPos = transform.position + (forward * forwardOffset) + (Vector3.up * upwardOffset);
            Vector3 targetPoint = GetAimTargetPoint(spawnPos);

            abilityManager.SpawnEnchanterHealBeaconServerRpc(
                spawnPos,
                targetPoint,
                healPerTick,
                tickInterval,
                baseDuration,
                durationPerCharge,
                healRadius
            );
        }

        // fires a screen-center ray to find where the player is actually aiming, clamped to maxThrowDistance
        private Vector3 GetAimTargetPoint(Vector3 spawnPos)
        {
            if (Camera.main == null)
            {
                Vector3 fallbackForward = playerController != null ? playerController.transform.forward : transform.forward;
                return spawnPos + fallbackForward * maxThrowDistance;
            }

            Ray aimRay = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 rawTarget;

            // cast a bit further than max so we can clamp on our side
            if (Physics.Raycast(aimRay, out RaycastHit hit, maxThrowDistance + 30f))
                rawTarget = hit.point;
            else
                rawTarget = aimRay.GetPoint(maxThrowDistance);

            // clamp horizontal distance from spawnPos to maxThrowDistance
            Vector3 toTarget = rawTarget - spawnPos;
            toTarget.y = 0f;
            if (toTarget.magnitude > maxThrowDistance)
                rawTarget = spawnPos + toTarget.normalized * maxThrowDistance + Vector3.up * (rawTarget.y - spawnPos.y);

            return rawTarget;
        }

        public static void InvokeBeaconThrown(Vector3 position, Vector3 direction)
        {
            OnBeaconThrown?.Invoke(position, direction);
        }
    }
}
