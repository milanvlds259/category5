using UnityEngine;

namespace Category5.WeakPoints
{
    // grants the attacker stacks in the PlayerStackManager when they break a weak point
    [CreateAssetMenu(fileName = "New Grant Stack Effect", menuName = "Category5/Weak Point Effects/Grant Stack")]
    public class GrantStackEffect : WeakPointBreakEffect
    {
        [Header("stacks")]
        [Tooltip("string id for this stack type (e.g. 'FighterComboStack', 'AssassinMark')")]
        [SerializeField] private string stackId = "WeakPointStack";

        [Tooltip("number of stacks granted per break")]
        [SerializeField] private int amount = 1;

        [Tooltip("maximum stacks (0 = unlimited)")]
        [SerializeField] private int maxStacks = 5;

        [Tooltip("time in seconds before stacks decay to 0 (0 = no decay)")]
        [SerializeField] private float decayTime = 0f;

        public override void ApplyEffect(WeakPointBreakContext context)
        {
            var stackManager = context.AttackerPlayer?.GetComponent<PlayerStackManager>();
            if (stackManager == null) return;

            stackManager.AddStack(stackId, amount, maxStacks, decayTime);
        }
    }
}
