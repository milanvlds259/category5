using UnityEngine;
using System;
using System.Collections;
using Category5.Player;

namespace Category5
{
    // dash slash - quick dash in any direction with a slashing attack. has 2 charges with individual cooldowns 
    // each successful hit grants +20% damage buff to your next ability for 4s (does not stack but refreshes) 
    // rewards chaining mobility with burst windows.
    public class AssassinQ : AbilityBase
    {
        [Header("Dash Settings")]
        [SerializeField] private float dashDistance = 7f;
        [SerializeField] private float dashDuration = 0.15f;
        [SerializeField] private float hitRadius = 1f;
        [SerializeField] private LayerMask enemyLayers;

        [Header("Buff Settings")]
        [SerializeField] private float buffDuration = 4f;

        private float _charge1Timer;
        private float _charge2Timer;
        private bool _hasDamageBuff;
        private float _buffExpireTime;
        private Coroutine _dashRoutine;
        private int _playerLayer;
        private bool _enemyCollisionIgnored;

        public static event Action<Vector3, Vector3, float> OnDashStarted;
        public static event Action<Vector3, int> OnDashHit;
        public static event Action<Vector3> OnDashEnded;
        public static event Action<AssassinQ, bool> OnBuffStateChanged;
        public static event Action<AssassinQ, int, int> OnChargesChanged;

        public override bool StartCooldownOnExecute => false;
        public override bool UsesManagerCooldownGate => false;

        public bool HasDamageBuff => _hasDamageBuff;
        public int CurrentCharges => AvailableCharges;
        public int MaxCharges => 2;

        private int AvailableCharges => (_charge1Timer <= 0f ? 1 : 0) + (_charge2Timer <= 0f ? 1 : 0);

        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            if (_dashRoutine != null) return false;
            return AvailableCharges > 0;
        }

        public override void Execute()
        {   
            if (!CanUse()) return;

            // determine dash direction based on player input or facing direction
            Vector3 direction = playerController != null ? playerController.GetMovementInputDirection() : Vector3.zero;
            direction.y = 0f;
            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }
            direction.Normalize();

            transform.rotation = Quaternion.LookRotation(direction);

            ConsumeCharge();

            // execute the dash on the server
            Vector3 startPosition = transform.position;
            abilityManager.ExecuteAssassinQDashServerRpc(
                startPosition,
                direction,
                dashDistance,
                Mathf.RoundToInt(abilityData.baseDamage),
                hitRadius,
                enemyLayers.value
            );

            if (_dashRoutine != null)
            {
                StopCoroutine(_dashRoutine);
            }

            _dashRoutine = StartCoroutine(DashRoutine(direction, dashDistance));
        }

        private void Update()
        {
            int previousAvailableCharges = AvailableCharges;

            // update charge timers
            if (_charge1Timer > 0f)
            {
                _charge1Timer = Mathf.Max(0f, _charge1Timer - Time.deltaTime);
            }

            if (_charge2Timer > 0f)
            {
                _charge2Timer = Mathf.Max(0f, _charge2Timer - Time.deltaTime);
            }

            // if we just gained a charge, reset the cooldown display
            if (previousAvailableCharges == 0 && AvailableCharges > 0 && abilityManager != null && IsOwner)
            {
                abilityManager.ResetAbilityCooldown(AbilitySlot.Ability1);
            }

            if (previousAvailableCharges != AvailableCharges)
            {
                NotifyChargesChanged();
            }

            if (_hasDamageBuff && Time.time >= _buffExpireTime)
            {
                SetDamageBuff(false);
            }
        }

        public bool ConsumeDamageBuff()
        {
            if (!_hasDamageBuff)
            {
                return false;
            }

            SetDamageBuff(false);
            return true;
        }

        public void ResetAllCharges()
        {
            _charge1Timer = 0f;
            _charge2Timer = 0f;
            NotifyChargesChanged();

            if (abilityManager != null && IsOwner)
            {
                abilityManager.ResetAbilityCooldown(AbilitySlot.Ability1);
            }
        }

        public void OnDashHitResolved(int hitCount)
        {
            if (hitCount > 0)
            {
                SetDamageBuff(true);
            }
        }

        private void ConsumeCharge()
        {
            if (_charge1Timer <= 0f)
            {
                _charge1Timer = abilityData != null ? abilityData.cooldownDuration : 0f;
            }
            else if (_charge2Timer <= 0f)
            {
                _charge2Timer = abilityData != null ? abilityData.cooldownDuration : 0f;
            }

            NotifyChargesChanged();
            SyncCooldownDisplay();
        }

        private void NotifyChargesChanged()
        {
            OnChargesChanged?.Invoke(this, CurrentCharges, MaxCharges);
        }

        private void SyncCooldownDisplay()
        {
            if (abilityManager == null || !IsOwner)
            {
                return;
            }

            if (AvailableCharges > 0)
            {
                abilityManager.ResetAbilityCooldown(AbilitySlot.Ability1);
                return;
            }

            abilityManager.SetAbilityCooldownDisplay(AbilitySlot.Ability1, GetNextChargeRecoveryTime());
        }

        private float GetNextChargeRecoveryTime()
        {
            float nextCharge = float.MaxValue;

            if (_charge1Timer > 0f)
            {
                nextCharge = Mathf.Min(nextCharge, _charge1Timer);
            }

            if (_charge2Timer > 0f)
            {
                nextCharge = Mathf.Min(nextCharge, _charge2Timer);
            }

            return nextCharge == float.MaxValue ? 0f : nextCharge;
        }

        private void SetDamageBuff(bool enabled)
        {
            if (_hasDamageBuff == enabled)
            {
                if (enabled)
                {
                    _buffExpireTime = Time.time + buffDuration;
                }
                return;
            }

            _hasDamageBuff = enabled;
            _buffExpireTime = enabled ? Time.time + buffDuration : 0f;
            OnBuffStateChanged?.Invoke(this, enabled);
        }

        private IEnumerator DashRoutine(Vector3 direction, float distance)
        {
            _playerLayer = gameObject.layer;
            SetEnemyCollisionIgnored(true);
            OnDashStarted?.Invoke(transform.position, direction, distance);

            // calculate speed based on distance and duration
            float elapsed = 0f;
            float speed = distance / Mathf.Max(0.01f, dashDuration);
            CharacterController controller = playerController != null ? playerController.GetComponent<CharacterController>() : null;

            // move the player over time
            while (elapsed < dashDuration)
            {
                float step = speed * Time.deltaTime;
                if (controller != null)
                {
                    controller.Move(direction * step);
                }
                else
                {
                    transform.position += direction * step;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            SetEnemyCollisionIgnored(false);
            _dashRoutine = null;
            OnDashEnded?.Invoke(transform.position);
        }

        private void SetEnemyCollisionIgnored(bool ignore)
        {
            if (enemyLayers.value == 0) return;

            if (!ignore && !_enemyCollisionIgnored) return;

            for (int layer = 0; layer < 32; layer++)
            {
                if ((enemyLayers.value & (1 << layer)) != 0)
                {
                    Physics.IgnoreLayerCollision(_playerLayer, layer, ignore);
                }
            }

            _enemyCollisionIgnored = ignore;
        }

        private void OnDisable()
        {
            if (_dashRoutine != null)
            {
                StopCoroutine(_dashRoutine);
                _dashRoutine = null;
            }

            SetEnemyCollisionIgnored(false);
            SetDamageBuff(false);
        }

        public static void InvokeDashStarted(Vector3 position, Vector3 direction, float distance)
        {
            OnDashStarted?.Invoke(position, direction, distance);
        }

        public static void InvokeDashHit(Vector3 position, int hitCount)
        {
            OnDashHit?.Invoke(position, hitCount);
        }
    }
}