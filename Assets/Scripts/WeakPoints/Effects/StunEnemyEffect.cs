using UnityEngine;
using Category5.Enemies;

namespace Category5.WeakPoints
{
    // stuns an enemy when a weak point breaks
    [CreateAssetMenu(fileName = "New Stun Enemy Effect", menuName = "Category5/Weak Point Effects/Stun Enemy")]
    public class StunEnemyEffect : WeakPointBreakEffect
    {
        [Header("stun")]
        [Tooltip("how long the enemy is stunned in seconds")]
        [SerializeField] private float stunDuration = 2f;

        public override void ApplyEffect(WeakPointBreakContext context)
        {
            var enemy = context.Host as EnemyBase;
            if (enemy == null)
            {
                Debug.LogWarning("[StunEnemyEffect] host is not an EnemyBase — boss stun is not yet supported");
                return;
            }

            if (enemy.IsDead) return;

            enemy.ApplyStun(stunDuration);
        }
    }
}
