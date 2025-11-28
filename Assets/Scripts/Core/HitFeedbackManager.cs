using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections;

namespace Category5.Core
{
    // centralized system for triggering hit feedback effects
    // provides hooks for vfx, sfx, screen shake, and hit freeze
    public class HitFeedbackManager : MonoBehaviour
    {
        public static HitFeedbackManager Instance { get; private set; }
        
        [Header("Feedback Presets (edit these to tune combat feel)")]
        [Tooltip("Feedback for light attacks (combo hits 1-2)")]
        [SerializeField] private HitFeedbackData lightHitFeedback = new HitFeedbackData
        {
            shakeIntensity = 0.1f,
            shakeDuration = 0.1f,
            shakeFrequency = 25f,
            freezeDuration = 0.05f,
            freezeTimeScale = 0.1f
        };
        
        [Tooltip("Feedback for heavy attacks (combo finisher)")]
        [SerializeField] private HitFeedbackData heavyHitFeedback = new HitFeedbackData
        {
            shakeIntensity = 0.25f,
            shakeDuration = 0.15f,
            shakeFrequency = 30f,
            freezeDuration = 0.08f,
            freezeTimeScale = 0.02f
        };
        
        [Tooltip("Feedback for boss slam attacks")]
        [SerializeField] private HitFeedbackData bossSlamFeedback = new HitFeedbackData
        {
            shakeIntensity = 0.5f,
            shakeDuration = 0.3f,
            shakeFrequency = 20f,
            freezeDuration = 0.12f,
            freezeTimeScale = 0.01f
        };
        
        [Tooltip("Feedback when player takes damage")]
        [SerializeField] private HitFeedbackData playerDamagedFeedback = new HitFeedbackData
        {
            shakeIntensity = 0.2f,
            shakeDuration = 0.15f,
            shakeFrequency = 35f,
            freezeDuration = 0.06f,
            freezeTimeScale = 0.05f
        };
        
        [Header("Global Settings")]
        [Tooltip("Master toggle for screen shake effects")]
        [SerializeField] private bool enableScreenShake = true;
        [Tooltip("Master toggle for hit freeze effects")]
        [SerializeField] private bool enableHitFreeze = true;
        [Tooltip("Global intensity multiplier for all effects (affects both shake and freeze)")]
        [Range(0f, 2f)]
        [SerializeField] private float globalIntensityMultiplier = 1f;
        
        [Header("Hit Freeze Method")]
        [Tooltip("How to simulate hit freeze - Animator pauses animations, TimeScale affects everything")]
        [SerializeField] private HitFreezeMethod freezeMethod = HitFreezeMethod.AnimatorPause;
        
        [Header("Debug")]
        [Tooltip("Log hit freeze events to console")]
        [SerializeField] private bool debugHitFreeze = false;
        
        // reference to camera for shake effects
        private ThirdPersonCamera _camera;
        private Coroutine _freezeCoroutine;
        
        // for animator-based freeze
        private bool _isFreezing = false;
        public bool IsFreezing => _isFreezing;
        
        // event for other systems to react to freeze state
        public static event Action<bool> OnHitFreezeStateChanged;
        
        // =====================================
        // vfx/sfx event hooks for artists
        // subscribe to these events to trigger particles and sounds
        // =====================================
        
        // fired when player hits an enemy
        public static event Action<Vector3, int, bool> OnPlayerHitEnemy;
        
        // fired when player takes damage
        public static event Action<Vector3, int> OnPlayerTakeDamage;
        
        // fired when boss starts telegraphing an attack
        public static event Action<BossAttackType, Vector3> OnBossAttackTelegraph;
        
        // fired when boss executes an attack
        public static event Action<BossAttackType, Vector3> OnBossAttackExecute;
        
        // fired on any hit for generic vfx/sfx
        public static event Action<Vector3, HitFeedbackData> OnHitFeedback;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        private void Start()
        {
            // try to find camera reference
            _camera = FindFirstObjectByType<ThirdPersonCamera>();
        }
        
        // ensures we have a valid camera reference
        private void EnsureCameraReference()
        {
            if (_camera == null)
            {
                _camera = FindFirstObjectByType<ThirdPersonCamera>();
            }
        }
        
        // =====================================
        // public api for triggering feedback
        // =====================================
        
        // trigger hit feedback with custom data
        public void TriggerHitFeedback(HitFeedbackData data, Vector3 position)
        {
            if (globalIntensityMultiplier <= 0) return;
            
            // apply global multiplier
            data.shakeIntensity *= globalIntensityMultiplier;
            data.freezeDuration *= globalIntensityMultiplier;
            
            if (enableScreenShake && data.shakeIntensity > 0)
            {
                TriggerScreenShake(data.shakeIntensity, data.shakeDuration, data.shakeFrequency);
            }
            
            if (enableHitFreeze && data.freezeDuration > 0)
            {
                TriggerHitFreeze(data.freezeDuration, data.freezeTimeScale);
            }
            
            // fire generic event for artists
            OnHitFeedback?.Invoke(position, data);
        }
        
        // trigger hit feedback with inspector-configured presets
        public void TriggerLightHit(Vector3 position)
        {
            TriggerHitFeedback(lightHitFeedback, position);
        }
        
        public void TriggerHeavyHit(Vector3 position)
        {
            TriggerHitFeedback(heavyHitFeedback, position);
        }
        
        public void TriggerBossSlam(Vector3 position)
        {
            TriggerHitFeedback(bossSlamFeedback, position);
        }
        
        public void TriggerPlayerDamaged(Vector3 position)
        {
            TriggerHitFeedback(playerDamagedFeedback, position);
        }
        
        // =====================================
        // screen shake
        // =====================================
        
        public void TriggerScreenShake()
        {
            TriggerScreenShake(lightHitFeedback.shakeIntensity, lightHitFeedback.shakeDuration, lightHitFeedback.shakeFrequency);
        }
        
        public void TriggerScreenShake(float intensity, float duration, float frequency)
        {
            if (!enableScreenShake) return;
            
            EnsureCameraReference();
            if (_camera != null)
            {
                _camera.TriggerShake(intensity * globalIntensityMultiplier, duration, frequency);
            }
        }
        
        // =====================================
        // hit freeze (client-side only)
        // uses animator speed manipulation to work with networking
        // =====================================
        
        public void TriggerHitFreeze()
        {
            TriggerHitFreeze(lightHitFeedback.freezeDuration, lightHitFeedback.freezeTimeScale);
        }
        
        public void TriggerHitFreeze(float duration, float timeScale)
        {
            if (!enableHitFreeze) return;
            
            // dont freeze on server in networked game (only affects host/client)
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
            {
                return;
            }
            
            // stop any existing freeze
            if (_freezeCoroutine != null)
            {
                StopCoroutine(_freezeCoroutine);
                EndFreeze();
            }
            
            if (debugHitFreeze)
            {
                Debug.Log($"[HitFreeze] Triggering freeze: duration={duration}s, timeScale={timeScale}, method={freezeMethod}");
            }
            
            _freezeCoroutine = StartCoroutine(FreezeCoroutine(duration, timeScale));
        }
        
        private IEnumerator FreezeCoroutine(float duration, float timeScale)
        {
            _isFreezing = true;
            OnHitFreezeStateChanged?.Invoke(true);
            
            // apply freeze based on selected method
            if (freezeMethod == HitFreezeMethod.TimeScale || freezeMethod == HitFreezeMethod.Both)
            {
                Time.timeScale = timeScale;
            }
            
            if (freezeMethod == HitFreezeMethod.AnimatorPause || freezeMethod == HitFreezeMethod.Both)
            {
                SetAllAnimatorSpeeds(0f);
            }
            
            // use unscaled time for the freeze duration
            yield return new WaitForSecondsRealtime(duration);
            
            EndFreeze();
            _freezeCoroutine = null;
            
            if (debugHitFreeze)
            {
                Debug.Log("[HitFreeze] Freeze ended");
            }
        }
        
        private void EndFreeze()
        {
            _isFreezing = false;
            OnHitFreezeStateChanged?.Invoke(false);
            
            if (freezeMethod == HitFreezeMethod.TimeScale || freezeMethod == HitFreezeMethod.Both)
            {
                Time.timeScale = 1f;
            }
            
            if (freezeMethod == HitFreezeMethod.AnimatorPause || freezeMethod == HitFreezeMethod.Both)
            {
                SetAllAnimatorSpeeds(1f);
            }
        }
        
        // pauses/resumes all animators in the scene
        private void SetAllAnimatorSpeeds(float speed)
        {
            // find all animators and set their speed
            Animator[] animators = FindObjectsByType<Animator>(FindObjectsSortMode.None);
            foreach (var animator in animators)
            {
                if (animator != null && animator.isActiveAndEnabled)
                {
                    animator.speed = speed;
                }
            }
            
            if (debugHitFreeze)
            {
                Debug.Log($"[HitFreeze] Set {animators.Length} animator speeds to {speed}");
            }
        }
        
        // =====================================
        // vfx hook trigger methods
        // these are called from combat scripts
        // =====================================
        
        // call when player hits an enemy
        public void NotifyPlayerHitEnemy(Vector3 position, int damage, bool isCritical = false)
        {
            OnPlayerHitEnemy?.Invoke(position, damage, isCritical);
        }
        
        // call when player takes damage
        public void NotifyPlayerTakeDamage(Vector3 position, int damage)
        {
            OnPlayerTakeDamage?.Invoke(position, damage);
        }
        
        // call when boss starts telegraphing
        public void NotifyBossAttackTelegraph(BossAttackType attackType, Vector3 position)
        {
            OnBossAttackTelegraph?.Invoke(attackType, position);
        }
        
        // call when boss executes attack
        public void NotifyBossAttackExecute(BossAttackType attackType, Vector3 position)
        {
            OnBossAttackExecute?.Invoke(attackType, position);
        }
        
        // =====================================
        // settings api for designers
        // =====================================
        
        public void SetScreenShakeEnabled(bool enabled)
        {
            enableScreenShake = enabled;
        }
        
        public void SetHitFreezeEnabled(bool enabled)
        {
            enableHitFreeze = enabled;
        }
        
        public void SetGlobalIntensityMultiplier(float multiplier)
        {
            globalIntensityMultiplier = Mathf.Clamp(multiplier, 0f, 2f);
        }
        
        private void OnDestroy()
        {
            // ensure timescale is reset if destroyed mid-freeze
            if (_freezeCoroutine != null)
            {
                Time.timeScale = 1f;
            }
            
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
