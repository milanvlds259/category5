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

            Vector3 direction = playerController != null ? playerController.GetAimDirection() : transform.forward;
            direction.y = 0f;
            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }
            direction.Normalize();

            Vector3 spawnPos = transform.position + (direction * forwardOffset) + (Vector3.up * upwardOffset);

            abilityManager.SpawnEnchanterHealBeaconServerRpc(
                spawnPos,
                direction,
                maxThrowDistance,
                healPerTick,
                tickInterval,
                baseDuration,
                durationPerCharge,
                healRadius
            );
        }

        public static void InvokeBeaconThrown(Vector3 position, Vector3 direction)
        {
            OnBeaconThrown?.Invoke(position, direction);
        }
    }
}
