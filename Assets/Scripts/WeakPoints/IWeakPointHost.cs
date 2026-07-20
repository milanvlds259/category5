namespace Category5.WeakPoints
{
    // interface for entities that can have weak points (enemies and bosses)
    // the WeakPoint system calls these to forward damage and notify of breaks
    public interface IWeakPointHost
    {
        bool IsDead { get; }

        // called by a weak point after applying its damage multiplier
        // forwards the multiplied damage to the host entity's health
        void TakeDamageFromWeakPoint(int damage, ulong attackerClientId);

        // called when one of this host's weak points is broken
        void OnWeakPointBroken(WeakPoint weakPoint, ulong attackerClientId);

        // called to reset all weak points (e.g. round transition)
        void ResetAllWeakPoints();
    }
}
