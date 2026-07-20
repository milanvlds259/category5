using UnityEngine;
using Category5.Player;

namespace Category5.WeakPoints
{
    // heals the attacker when they break a weak point
    [CreateAssetMenu(fileName = "New Heal Attacker Effect", menuName = "Category5/Weak Point Effects/Heal Attacker")]
    public class HealAttackerEffect : WeakPointBreakEffect
    {
        [Header("heal")]
        [Tooltip("flat heal amount (used if healPercent is 0)")]
        [SerializeField] private int healAmount = 25;

        [Tooltip("percentage of max health to heal (overrides flat heal if > 0)")]
        [SerializeField] private float healPercentOfMaxHealth = 0f;

        public override void ApplyEffect(WeakPointBreakContext context)
        {
            if (context.AttackerPlayer == null) return;
            if (context.AttackerPlayer.IsDead.Value) return;

            int heal;
            if (healPercentOfMaxHealth > 0f)
            {
                heal = Mathf.RoundToInt(context.AttackerPlayer.MaxHealth * healPercentOfMaxHealth);
            }
            else
            {
                heal = healAmount;
            }

            if (heal > 0)
            {
                context.AttackerPlayer.Heal(heal);
            }
        }
    }
}
