using UnityEngine;
using Category5.Player;

namespace Category5.Items
{
    // vantage point: deal bonus damage when attacking from above the target
    // subscribes to OnBeforeDamageCalculation and checks height difference
    public class VantagePointBehaviour : ItemBehaviour
    {
        // tier scaling — tunable in the prefab inspector
        [SerializeField] private float[] heightThreshold = { 2.0f, 1.8f, 1.6f, 1.4f, 1.2f }; // min height advantage in world units
        [SerializeField] private float[] damageBonus     = { 0.10f, 0.13f, 0.16f, 0.19f, 0.22f }; // bonus multiplier added to damage

        protected override void OnInitialize()
        {
            if (!IsServer) return;
            PlayerCombat.OnBeforeDamageCalculation += OnBeforeDamage;
        }

        protected override void OnTierChanged(int oldTier, int newTier)
        {
            // no resubscription needed, values read per-hit
        }

        public override void OnRemoved()
        {
            if (PlayerCombat != null)
                PlayerCombat.OnBeforeDamageCalculation -= OnBeforeDamage;
        }

        private void OnBeforeDamage(ref float bonusMultiplier, GameObject target)
        {
            if (target == null || PlayerController == null) return;

            float playerY = PlayerController.transform.position.y;
            float targetY = target.transform.position.y;
            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);

            if (playerY - targetY >= heightThreshold[idx])
            {
                bonusMultiplier += damageBonus[idx];
            }
        }

        public override object[] GetFormatValues(int tier)
        {
            int idx = Mathf.Clamp(tier - 1, 0, 4);
            return new object[]
            {
                damageBonus[idx] * 100f,
                heightThreshold[idx]
            };
        }
    }
}
