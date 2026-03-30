using UnityEngine;

namespace Category5.Items
{
    // weather balloon: boosts jump height, reduces fall speed, grants damage resistance while airborne
    // all modifications are direct multiplier overrides on PlayerController
    public class WeatherBalloonBehaviour : ItemBehaviour
    {
        // tier scaling tables (index 0 = tier 1) — tunable in the prefab inspector
        [SerializeField] private float[] jumpBonus      = { 0.30f, 0.38f, 0.46f, 0.54f, 0.62f }; // added to 1.0 base
        [SerializeField] private float[] fallReduction  = { 0.40f, 0.45f, 0.50f, 0.55f, 0.60f }; // subtracted from 1.0 base
        [SerializeField] private float[] airborneResist = { 0.15f, 0.18f, 0.21f, 0.24f, 0.27f };

        protected override void OnInitialize()
        {
            ApplyToController();
        }

        protected override void OnTierChanged(int oldTier, int newTier)
        {
            ApplyToController();
        }

        public override void OnRemoved()
        {
            if (PlayerController == null) return;

            // restore defaults
            PlayerController.JumpHeightMultiplier = 1f;
            PlayerController.FallSpeedMultiplier = 1f;
            PlayerController.AirborneResistanceMultiplier = 0f;
        }

        private void ApplyToController()
        {
            if (PlayerController == null) return;

            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);
            PlayerController.JumpHeightMultiplier = 1f + jumpBonus[idx];
            PlayerController.FallSpeedMultiplier = 1f - fallReduction[idx];
            PlayerController.AirborneResistanceMultiplier = airborneResist[idx];
        }

        public override object[] GetFormatValues(int tier)
        {
            int idx = Mathf.Clamp(tier - 1, 0, 4);
            return new object[]
            {
                jumpBonus[idx] * 100f,
                fallReduction[idx] * 100f,
                airborneResist[idx] * 100f
            };
        }
    }
}
