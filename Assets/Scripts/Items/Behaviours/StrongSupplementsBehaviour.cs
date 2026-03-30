using UnityEngine;

namespace Category5.Items
{
    // strong supplements: grants bonus max HP, then converts all bonus HP into extra attack damage
    // attack damage bonus = (totalBonusHp / baseMaxHp) * scalingFactor * baseAttackDamage
    // feeds into every damage source (melee, ranged, abilities) through EffectiveAttackDamage
    public class StrongSupplementsBehaviour : ItemBehaviour
    {
        // tier scaling        
		[SerializeField] private int[]   bonusHp       = { 25, 35, 45, 55, 65 };
        [SerializeField] private float[] scalingFactor = { 0.50f, 0.55f, 0.60f, 0.65f, 0.70f };

        protected override void OnInitialize()
        {
            if (!IsServer) return;

            ApplyBonusHp();
            // recalculate attack bonus whenever inventory or dynamic bonuses change
            PlayerStats.OnStatsChanged += RecalculateAttackBonus;
            RecalculateAttackBonus();
        }

        protected override void OnTierChanged(int oldTier, int newTier)
        {
            if (!IsServer) return;

            ApplyBonusHp();
            RecalculateAttackBonus();
        }

        public override void OnRemoved()
        {
            if (PlayerStats != null)
            {
                PlayerStats.OnStatsChanged -= RecalculateAttackBonus;
                PlayerStats.SetDynamicMaxHealthBonus(0);
                PlayerStats.SetDynamicAttackDamageBonus(0f);
            }
        }

        private void ApplyBonusHp()
        {
            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);
            PlayerStats.SetDynamicMaxHealthBonus(bonusHp[idx]);
        }

        private void RecalculateAttackBonus()
        {
            if (PlayerStats == null) return;

            int baseHp = PlayerStats.BaseMaxHealthValue;
            if (baseHp <= 0) return;

            // total bonus HP from every source — the more HP items, the bigger the bonus
            int totalBonusHp = PlayerStats.TotalMaxHealth - baseHp;
            if (totalBonusHp <= 0)
            {
                PlayerStats.SetDynamicAttackDamageBonus(0f);
                return;
            }

            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);
            float bonus = PlayerStats.BaseAttackDamage * (totalBonusHp / (float)baseHp) * scalingFactor[idx];
            PlayerStats.SetDynamicAttackDamageBonus(bonus);
        }

        public override object[] GetFormatValues(int tier)
        {
            int idx = Mathf.Clamp(tier - 1, 0, 4);
            float exampleBonus = (bonusHp[idx] / 100f) * scalingFactor[idx] * 100f;
            return new object[]
            {
                bonusHp[idx],
                scalingFactor[idx] * 100f,
                exampleBonus
            };
        }
    }
}
