using UnityEngine;
using Category5.Core;
using Category5.Player;
using Category5.UI;

namespace Category5
{
    // burn status effect applied to enemies by elementalist fire abilities
    // server-only component that ticks damage over time
    // reapplication refreshes duration rather than stacking damage
    public class BurnEffect : MonoBehaviour
    {
        [Header("runtime state (set by initializer)")]
        [SerializeField] private int damagePerTick = 5;
        [SerializeField] private float tickInterval = 0.5f;
        [SerializeField] private float duration = 3f;
        [SerializeField] private ulong sourceClientId;

        private float _tickTimer;
        private float _remainingDuration;
        private IDamageable _target;
        private PlayerStats _ownerStats;
        private bool _isInitialized;

        // initialize the burn effect (called on server only)
        public void Initialize(int damagePerTick, float tickInterval, float duration, ulong sourceClientId, PlayerStats ownerStats = null)
        {
            this.damagePerTick = damagePerTick;
            this.tickInterval = tickInterval;
            this.duration = duration;
            this.sourceClientId = sourceClientId;
            _ownerStats = ownerStats;

            _remainingDuration = duration;
            _tickTimer = 0f;
            _target = GetComponent<IDamageable>();
            _isInitialized = true;

            if (_target == null)
            {
                Debug.LogWarning("[BurnEffect] no IDamageable found on target, destroying");
                Destroy(this);
            }
        }

        // refresh the burn duration (reapplication resets timer, does not stack)
        public void Refresh(float newDuration)
        {
            _remainingDuration = newDuration > 0 ? newDuration : duration;
            Debug.Log($"[BurnEffect] refreshed duration to {_remainingDuration}s");
        }

        // refresh with new parameters
        public void Refresh(int newDamagePerTick, float newTickInterval, float newDuration)
        {
            damagePerTick = newDamagePerTick;
            tickInterval = newTickInterval;
            _remainingDuration = newDuration;
        }

        private void Update()
        {
            if (!_isInitialized) return;

            _remainingDuration -= Time.deltaTime;
            if (_remainingDuration <= 0f)
            {
                Debug.Log("[BurnEffect] expired, removing");
                Destroy(this);
                return;
            }

            _tickTimer += Time.deltaTime;
            if (_tickTimer >= tickInterval)
            {
                _tickTimer -= tickInterval;
                ApplyBurnDamage();
            }
        }

        private void ApplyBurnDamage()
        {
            if (_target == null)
            {
                Destroy(this);
                return;
            }

            // calculate damage with owner stats if available
            int finalDamage = _ownerStats != null
                ? _ownerStats.CalculateDamage(damagePerTick)
                : damagePerTick;

            _target.TakeDamage(finalDamage);

            Debug.Log($"[BurnEffect] tick for {finalDamage} damage, {_remainingDuration:F1}s remaining");
        }
    }
}
