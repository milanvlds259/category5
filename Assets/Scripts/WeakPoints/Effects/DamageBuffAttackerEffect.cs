using UnityEngine;
using Category5.Player;

namespace Category5.WeakPoints
{
    // gives the attacker a temporary damage buff when they break a weak point
    [CreateAssetMenu(fileName = "New Damage Buff Effect", menuName = "Category5/Weak Point Effects/Damage Buff Attacker")]
    public class DamageBuffAttackerEffect : WeakPointBreakEffect
    {
        [Header("buff")]
        [Tooltip("bonus damage multiplier (0.25 = 25% more damage)")]
        [SerializeField] private float buffMultiplier = 0.25f;

        [Tooltip("how long the buff lasts in seconds")]
        [SerializeField] private float buffDuration = 5f;

        public override void ApplyEffect(WeakPointBreakContext context)
        {
            if (context.AttackerStats == null) return;

            context.AttackerStats.ApplyTemporaryMultiplier("damage", buffMultiplier, buffDuration);
        }
    }
}
