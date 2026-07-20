using UnityEngine;

namespace Category5.WeakPoints
{
    // abstract base for scriptable object break effects
    // each subclass defines one effect type (stun, knockback, heal, buff, stack, etc)
    // assign instances to a weak point's breakEffects array in the inspector
    [CreateAssetMenu(fileName = "New WeakPoint Break Effect", menuName = "Category5/Weak Point Break Effect")]
    public abstract class WeakPointBreakEffect : ScriptableObject
    {
        // called when the weak point breaks (hp reaches zero)
        public abstract void ApplyEffect(WeakPointBreakContext context);

        // called on every hit while the weak point is alive (optional, defaults to no-op)
        public virtual void ApplyHitEffect(WeakPointBreakContext context) { }
    }
}
