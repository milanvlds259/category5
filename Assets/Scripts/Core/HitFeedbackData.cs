using UnityEngine;

namespace Category5.Core
{
    // data structure for hit feedback parameters
    // used by HitFeedbackManager and can be serialized on individual attacks for per-attack tuning
    [System.Serializable]
    public struct HitFeedbackData
    {
        [Header("Screen Shake")]
        [Tooltip("How intense the screen shake is (0 = none, 0.5 = strong)")]
        public float shakeIntensity;
        [Tooltip("How long the shake lasts in seconds")]
        public float shakeDuration;
        [Tooltip("How fast the shake oscillates (higher = faster)")]
        public float shakeFrequency;
        
        [Header("Hit Freeze")]
        [Tooltip("How long the freeze lasts in seconds (0.05-0.15 typical)")]
        public float freezeDuration;
        [Tooltip("Time scale during freeze (0 = full stop, 0.1 = 10% speed)")]
        public float freezeTimeScale;
    }
    
    // enum for boss attack types used by vfx hooks
    public enum BossAttackType
    {
        None,
        Slam,
        Swipe,
        Projectile,
        AoE,
        Charge,
        Summon
    }
    
    // method used to simulate hit freeze effect
    public enum HitFreezeMethod
    {
        [Tooltip("Pause all animators - works with networking")]
        AnimatorPause,
        [Tooltip("Modify Time.timeScale - may conflict with NGO")]
        TimeScale,
        [Tooltip("Use both methods together")]
        Both
    }
}
