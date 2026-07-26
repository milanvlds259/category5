namespace Category5.WeakPoints
{
    // how a destroyed weak point comes back
    public enum WeakPointRespawnMode
    {
        // comes back after a delay (default behavior)
        Timer,

        // only comes back when Activate() is called (animation-triggered)
        AnimationTriggered
    }
}
