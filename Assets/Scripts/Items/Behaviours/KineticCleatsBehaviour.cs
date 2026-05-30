using UnityEngine;

namespace Category5.Items
{
    /// <summary>
    /// Kinetic Cleats: When you sprint, you constantly build speed over time until you reach a maximum.
    /// Increases crit damage at higher speeds.
    /// </summary>
    public class KineticCleatsBehaviour : ItemBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float baseMaxSpeedBonus = 0.5f; // +50% speed at T1
        [SerializeField] private float baseMaxCritDamageBonus = 0.5f; // +50% crit damage at T1
        [SerializeField] private float chargeTime = 3.0f; // Seconds to reach full charge
        [SerializeField] private float decayTime = 1.5f; // Seconds to lose full charge

        private float _currentCharge; // 0 to 1

        protected override void OnInitialize()
        {
            _currentCharge = 0f;
        }

        private void Update()
        {
            if (PlayerController == null || PlayerStats == null) return;

            // Only build charge while sprinting AND moving
            bool isSprinting = PlayerController.IsSprinting;
            bool isMoving = PlayerController.CurrentMovementSpeed > 0.1f;

            if (isSprinting && isMoving)
            {
                _currentCharge += Time.deltaTime / chargeTime;
            }
            else
            {
                _currentCharge -= Time.deltaTime / decayTime;
            }

            _currentCharge = Mathf.Clamp01(_currentCharge);

            // Apply bonuses to PlayerStats
            float speedBonus = TierScale(baseMaxSpeedBonus, baseMaxSpeedBonus * ItemData.DefaultTierScalePerLevel) * _currentCharge;
            float critBonus = TierScale(baseMaxCritDamageBonus, baseMaxCritDamageBonus * ItemData.DefaultTierScalePerLevel) * _currentCharge;

            PlayerStats.SetDynamicMoveSpeedBonus(speedBonus);
            PlayerStats.SetDynamicCritDamageBonus(critBonus);
        }

        public override void OnRemoved()
        {
            if (PlayerStats != null)
            {
                PlayerStats.SetDynamicMoveSpeedBonus(0f);
                PlayerStats.SetDynamicCritDamageBonus(0f);
            }
        }

        public override object[] GetFormatValues(int tier)
        {
            float speedBonus = TierScaleAt(baseMaxSpeedBonus, baseMaxSpeedBonus * ItemData.DefaultTierScalePerLevel, tier);
            float critBonus = TierScaleAt(baseMaxCritDamageBonus, baseMaxCritDamageBonus * ItemData.DefaultTierScalePerLevel, tier);

            return new object[]
            {
                Mathf.RoundToInt(speedBonus * 100f),
                Mathf.RoundToInt(critBonus * 100f),
                chargeTime
            };
        }
    }
}
