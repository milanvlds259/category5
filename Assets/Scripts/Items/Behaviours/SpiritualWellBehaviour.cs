using UnityEngine;
using Unity.Netcode;

namespace Category5.Items
{
    // spiritual well: increases the player's max mana (ult meter capacity)
    // removing the old mana-on-attack mechanic — ult meter is now sacred
    public class SpiritualWellBehaviour : ItemBehaviour
    {
        [SerializeField] private int baseManaForZero = 50; // base mana given to classes with 0 mana

        // mana bonus we applied to PlayerStats — tracked so we can remove it cleanly
        private int _appliedManaBonus = 0;

        protected override void OnInitialize()
        {
            if (!IsServer) return;

            ApplyManaBonus();
        }

        protected override void OnTierChanged(int oldTier, int newTier)
        {
            if (!IsServer) return;
            ApplyManaBonus();
        }

        public override void OnRemoved()
        {
            // remove mana bonus
            if (PlayerStats != null)
                PlayerStats.SetDynamicMaxManaBonus(0);
        }

        private void ApplyManaBonus()
        {
            if (PlayerStats == null || PlayerController == null) return;

            int baseMana = PlayerStats.TotalMaxMana - _appliedManaBonus; // current base without our bonus
            int targetBase = baseMana > 0 ? baseMana : baseManaForZero;  // give classes with 0 mana a base pool
            int bonus = Mathf.RoundToInt(targetBase * 0.5f);              // +50%

            _appliedManaBonus = bonus;
            PlayerStats.SetDynamicMaxManaBonus(bonus);
        }

        public override object[] GetFormatValues(int tier)
        {
            return new object[] { 50 }; // flat +50% max mana at all tiers
        }
    }
}
