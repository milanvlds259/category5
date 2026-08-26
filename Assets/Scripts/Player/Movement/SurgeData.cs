using UnityEngine;

namespace Category5.Player.Movement
{
    [CreateAssetMenu(fileName = "New Surge Data", menuName = "Category5/Surge Data")]
    public class SurgeData : ScriptableObject
    {
        [Header("Resource")]
        [Min(0f)] public float initialChargeCost = 20f;
        [Min(0f)] public float sustainedChargeCostPerSecond = 5f;
        [Min(0f)] public float sustainedDrainDelay = 0.25f;
        [Min(0.01f)] public float maximumChargeDuration = 2f;
        [Min(0f)] public float slideLaunchSurgeReward = 5f;

        [Header("Slide")]
        [Min(0f)] public float slideDuration = 1f;
        [Min(0f)] public float slideSparkDelay = 0.5f;
        [Range(0f, 1f)] public float slideTractionMultiplier = 0.2f;
        [Range(0f, 1f)] public float slideDirectionInfluence = 0.15f;
        [Range(0f, 1f)] public float slideTurnSpeedMultiplier = 0.25f;
        [Min(0f)] public float slideMinimumSpeed = 0.5f;

        [Header("Thrust")]
        [Min(0f)] public float thrustMinimumDuration = 0.2f;
        [Min(0f)] public float thrustMaximumDuration = 0.35f;
        [Min(0f)] public float thrustMinimumSpeed = 12f;
        [Min(0f)] public float thrustMaximumSpeed = 24f;
        [Min(0f)] public float thrustGravityMultiplier = 0.15f;
        [Min(0f)] public float thrustFallSpeedCap = 0f;

        [Header("Jump")]
        [Min(0f)] public float surgeJumpMinimumDuration = 0.25f;
        [Min(0f)] public float surgeJumpMaximumDuration = 0.45f;
        [Min(0f)] public float surgeJumpMinimumSpeed = 10f;
        [Min(0f)] public float surgeJumpMaximumSpeed = 22f;
        [Min(0f)] public float surgeJumpGravityMultiplier = 0.2f;

        [Header("Pull")]
        [Min(0f)] public float pullRange = 20f;
        [Min(0f)] public float pullHorizontalSpeed = 18f;
        [Min(0f)] public float pullMinimumVerticalSpeed = 2f;
        [Min(0f)] public float pullVerticalSpeedPerHeight = 0.5f;
        [Min(0f)] public float pullCooldown = 0.5f;

        [Header("Curves")]
        public AnimationCurve durationByCharge = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve speedByCharge = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public float GetThrustDuration(float chargePercent)
        {
            float normalizedCharge = Mathf.Clamp01(durationByCharge.Evaluate(Mathf.Clamp01(chargePercent)));
            return Mathf.Lerp(thrustMinimumDuration, thrustMaximumDuration, normalizedCharge);
        }

        public float GetThrustSpeed(float chargePercent)
        {
            float normalizedCharge = Mathf.Clamp01(speedByCharge.Evaluate(Mathf.Clamp01(chargePercent)));
            return Mathf.Lerp(thrustMinimumSpeed, thrustMaximumSpeed, normalizedCharge);
        }

        public float GetSurgeJumpDuration(float chargePercent)
        {
            float normalizedCharge = Mathf.Clamp01(durationByCharge.Evaluate(Mathf.Clamp01(chargePercent)));
            return Mathf.Lerp(surgeJumpMinimumDuration, surgeJumpMaximumDuration, normalizedCharge);
        }

        public float GetSurgeJumpSpeed(float chargePercent)
        {
            float normalizedCharge = Mathf.Clamp01(speedByCharge.Evaluate(Mathf.Clamp01(chargePercent)));
            return Mathf.Lerp(surgeJumpMinimumSpeed, surgeJumpMaximumSpeed, normalizedCharge);
        }
    }
}
