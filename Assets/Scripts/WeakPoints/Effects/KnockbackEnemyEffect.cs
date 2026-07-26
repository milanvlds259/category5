using UnityEngine;
using Category5.Enemies;

namespace Category5.WeakPoints
{
    // knocks back an enemy when a weak point breaks
    [CreateAssetMenu(fileName = "New Knockback Enemy Effect", menuName = "Category5/Weak Point Effects/Knockback Enemy")]
    public class KnockbackEnemyEffect : WeakPointBreakEffect
    {
        [Header("knockback")]
        [SerializeField] private float knockbackForce = 10f;

        public enum KnockbackDirection
        {
            AwayFromAttacker,
            Upward,
            FixedDirection
        }

        [SerializeField] private KnockbackDirection direction = KnockbackDirection.AwayFromAttacker;
        [SerializeField] private Vector3 fixedDirection = Vector3.back;

        public override void ApplyEffect(WeakPointBreakContext context)
        {
            var enemy = context.Host as EnemyBase;
            if (enemy == null) return;
            if (enemy.IsDead) return;

            Vector3 knockback = direction switch
            {
                KnockbackDirection.AwayFromAttacker => (context.Host.transform.position - context.BreakPosition).normalized * knockbackForce,
                KnockbackDirection.Upward => Vector3.up * knockbackForce,
                KnockbackDirection.FixedDirection => fixedDirection.normalized * knockbackForce,
                _ => (context.Host.transform.position - context.BreakPosition).normalized * knockbackForce
            };

            enemy.ApplyKnockback(knockback);
        }
    }
}
