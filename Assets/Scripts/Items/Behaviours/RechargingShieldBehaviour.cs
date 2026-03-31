using UnityEngine;
using Category5.Player.Abilities;

namespace Category5.Items
{
    // recharging shield: bonus max HP equal to the sum of all remaining ability cooldowns in seconds
    // e.g. if Q has 4.2s left and R has 8.5s left, you gain ~128 bonus HP (at 10 hp/s)
    // bonus updates continuously as cooldowns drain and naturally falls off when abilities come off cooldown
    public class RechargingShieldBehaviour : ItemBehaviour
    {
        // how much bonus HP per remaining cooldown-second, per tier
        [SerializeField] private float[] hpPerSecond = { 8f, 10f, 12f, 15f, 18f };

        private PlayerAbilityManager _abilityManager;

        protected override void OnInitialize()
        {
            if (!IsServer) return;

            _abilityManager = PlayerController.GetComponent<PlayerAbilityManager>();
            if (_abilityManager == null)
            {
                Debug.LogError("RechargingShieldBehaviour: no PlayerAbilityManager found on player");
                return;
            }

            _abilityManager.ability1Cooldown.OnValueChanged += OnAnyCooldownChanged;
            _abilityManager.ability2Cooldown.OnValueChanged += OnAnyCooldownChanged;
            _abilityManager.ability3Cooldown.OnValueChanged += OnAnyCooldownChanged;

            Recalculate();
        }

        protected override void OnTierChanged(int oldTier, int newTier)
        {
            if (!IsServer) return;
            Recalculate();
        }

        public override void OnRemoved()
        {
            if (_abilityManager != null)
            {
                _abilityManager.ability1Cooldown.OnValueChanged -= OnAnyCooldownChanged;
                _abilityManager.ability2Cooldown.OnValueChanged -= OnAnyCooldownChanged;
                _abilityManager.ability3Cooldown.OnValueChanged -= OnAnyCooldownChanged;
            }

            if (PlayerStats != null)
                PlayerStats.SetDynamicMaxHealthBonus(0);
        }

        private void OnAnyCooldownChanged(float previous, float current)
        {
            Recalculate();
        }

        private void Recalculate()
        {
            if (_abilityManager == null || PlayerStats == null) return;

            float totalSeconds = _abilityManager.ability1Cooldown.Value
                               + _abilityManager.ability2Cooldown.Value
                               + _abilityManager.ability3Cooldown.Value;

            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);
            int bonus = Mathf.RoundToInt(totalSeconds * hpPerSecond[idx]);
            PlayerStats.SetDynamicMaxHealthBonus(bonus);
        }

        public override object[] GetFormatValues(int tier)
        {
            int idx = Mathf.Clamp(tier - 1, 0, 4);
            return new object[] { hpPerSecond[idx] };
        }
    }
}
