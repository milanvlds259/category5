using UnityEngine;
using Unity.Netcode;

namespace Category5.Items
{
    // spiritual well: increases max mana (gives mana to classes that have none)
    // basic attacks while mana is available spend some mana and deal bonus damage
    // also boosts forceful impact hits (handled by ForcefulImpactBehaviour)
    public class SpiritualWellBehaviour : ItemBehaviour
    {
        [SerializeField] private float[] bonusMultiplier = { 0.25f, 0.30f, 0.35f, 0.45f, 0.60f }; // added damage fraction
        [SerializeField] private int[]   manaPerHit      = { 10, 10, 12, 14, 16 };                 // mana spent per basic attack hit
        [SerializeField] private int     baseManaForZero = 50;                                       // base mana given to classes with 0 mana

        // mana bonus we applied to PlayerStats — tracked so we can remove it cleanly
        private int _appliedManaBonus = 0;

        protected override void OnInitialize()
        {
            if (!IsServer) return;

            ApplyManaBonus();

            // subscribe to mana value changes to toggle the multiplier
            PlayerController.CurrentMana.OnValueChanged += OnManaValueChanged;

            // subscribe to basic attack hits to spend mana
            PlayerCombat.OnPlayerDealtDamage += OnBasicAttackHit;
        }

        protected override void OnTierChanged(int oldTier, int newTier)
        {
            if (!IsServer) return;
            // re-apply mana bonus with new tier (set multiplier in ApplyManaBonus)
            ApplyManaBonus();
        }

        public override void OnRemoved()
        {
            if (PlayerController != null)
                PlayerController.CurrentMana.OnValueChanged -= OnManaValueChanged;

            if (PlayerCombat != null)
                PlayerCombat.OnPlayerDealtDamage -= OnBasicAttackHit;

            // remove mana bonus and multiplier
            if (PlayerStats != null)
            {
                PlayerStats.SetDynamicMaxManaBonus(0);
                PlayerStats.SetBasicAttackManaMultiplier(0f);
            }
        }

        private void ApplyManaBonus()
        {
            if (PlayerStats == null || PlayerController == null) return;

            int baseMana = PlayerStats.TotalMaxMana - _appliedManaBonus; // current base without our bonus
            int targetBase = baseMana > 0 ? baseMana : baseManaForZero;  // give classes with 0 mana a base pool
            int bonus = Mathf.RoundToInt(targetBase * 0.5f);              // +50%

            _appliedManaBonus = bonus;
            PlayerStats.SetDynamicMaxManaBonus(bonus);

            // set the multiplier based on whether mana is currently available
            RefreshMultiplier(PlayerController.CurrentMana.Value);
        }

        private void RefreshMultiplier(int currentMana)
        {
            if (PlayerStats == null) return;
            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);
            // only activate bonus when player has enough mana to actually spend
            bool hasEnough = currentMana >= manaPerHit[idx];
            PlayerStats.SetBasicAttackManaMultiplier(hasEnough ? bonusMultiplier[idx] : 0f);
        }

        private void OnManaValueChanged(int oldMana, int newMana)
        {
            // refresh every time mana changes so the multiplier is always accurate
            RefreshMultiplier(newMana);
        }

        private void OnBasicAttackHit(int damage, GameObject target, bool wasCrit)
        {
            if (PlayerController == null) return;
            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);
            if (PlayerController.CurrentMana.Value < manaPerHit[idx]) return;
            PlayerController.SpendMana(manaPerHit[idx]);
        }

        // called by ForcefulImpactBehaviour when a body-impact hit lands
        // applies the mana bonus to the damage value and spends mana, then returns the new damage
        public int ApplyManaBonus(int baseDamage)
        {
            if (PlayerController == null) return baseDamage;
            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);
            if (PlayerController.CurrentMana.Value < manaPerHit[idx]) return baseDamage;
            int boosted = Mathf.RoundToInt(baseDamage * (1f + bonusMultiplier[idx]));
            PlayerController.SpendMana(manaPerHit[idx]);
            return boosted;
        }

        public override object[] GetFormatValues(int tier)
        {
            int idx = Mathf.Clamp(tier - 1, 0, 4);
            return new object[]
            {
                Mathf.RoundToInt(bonusMultiplier[idx] * 100f),
                manaPerHit[idx]
            };
        }
    }
}
