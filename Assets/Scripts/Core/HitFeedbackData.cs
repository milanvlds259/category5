using UnityEngine;

namespace Category5.Core
{
    // data structure for hit feedback parameters
    // for combat designer (probably rylan) to configure different hit types
    [System.Serializable]
    public struct HitFeedbackData
    {
        [Header("Screen Shake")]
        public float shakeIntensity;
        public float shakeDuration;
        public float shakeFrequency;
        
        [Header("Hit Freeze")]
        public float freezeDuration;
        public float freezeTimeScale;
        
        // static presets for common hit types
        public static HitFeedbackData LightHit => new HitFeedbackData
        {
            shakeIntensity = 0.1f,
            shakeDuration = 0.1f,
            shakeFrequency = 25f,
            freezeDuration = 0.03f,
            freezeTimeScale = 0.1f
        };
        
        public static HitFeedbackData HeavyHit => new HitFeedbackData
        {
            shakeIntensity = 0.25f,
            shakeDuration = 0.15f,
            shakeFrequency = 30f,
            freezeDuration = 0.05f,
            freezeTimeScale = 0.05f
        };
        
        public static HitFeedbackData BossSlam => new HitFeedbackData
        {
            shakeIntensity = 0.5f,
            shakeDuration = 0.3f,
            shakeFrequency = 20f,
            freezeDuration = 0.08f,
            freezeTimeScale = 0.02f
        };
        
        public static HitFeedbackData PlayerDamaged => new HitFeedbackData
        {
            shakeIntensity = 0.15f,
            shakeDuration = 0.12f,
            shakeFrequency = 35f,
            freezeDuration = 0.04f,
            freezeTimeScale = 0.1f
        };
        
        public static HitFeedbackData None => new HitFeedbackData
        {
            shakeIntensity = 0f,
            shakeDuration = 0f,
            shakeFrequency = 0f,
            freezeDuration = 0f,
            freezeTimeScale = 1f
        };
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
}
