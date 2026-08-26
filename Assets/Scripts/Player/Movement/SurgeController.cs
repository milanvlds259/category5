using System;
using Unity.Netcode;
using UnityEngine;

namespace Category5.Player.Movement
{
    public enum SurgeState
    {
        None,
        Sliding,
        Charging,
        Thrust,
        Jump,
        Pull
    }

    [RequireComponent(typeof(PlayerController))]
    public class SurgeController : NetworkBehaviour
    {
        [SerializeField] private SurgeData surgeData;

        private PlayerController _playerController;
        private SurgeState _state;
        private float _chargeTime;
        private float _stateTime;
        private float _drainAccumulator;
        private float _lastPullTime = float.NegativeInfinity;
        private bool _thrustUsedThisAirTime;
        private bool _chargeRequestPending;

        public event Action<SurgeState, SurgeState> OnStateChanged;
        public event Action<float> OnChargeProgress;

        public SurgeData Data => surgeData;
        public SurgeState State => _state;
        public bool IsActive => _state != SurgeState.None;
        public bool IsCharging => _state == SurgeState.Charging;
        public bool IsSliding => _state == SurgeState.Sliding;
        public bool IsMotionOverrideActive => _state == SurgeState.Thrust || _state == SurgeState.Jump || _state == SurgeState.Pull;
        public float ChargeTime => _chargeTime;
        public float SlideTurnSpeedMultiplier => surgeData != null ? surgeData.slideTurnSpeedMultiplier : 1f;
        public float SlideDirectionInfluence => surgeData != null ? surgeData.slideDirectionInfluence : 1f;
        public float ChargePercent => surgeData == null || surgeData.sustainedDrainDelay <= 0f
            ? 0f
            : Mathf.Clamp01(_chargeTime / surgeData.maximumChargeDuration);

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _state = SurgeState.None;
            }
        }

        private void Update()
        {
            if (surgeData == null)
            {
                return;
            }

            if (!IsOwner && !IsOffline())
            {
                return;
            }

            if (_state == SurgeState.Charging)
            {
                UpdateCharge(Time.deltaTime);
            }
            else if (_state == SurgeState.Sliding)
            {
                _stateTime += Time.deltaTime;
                if (_stateTime >= surgeData.slideDuration)
                {
                    EndState();
                }
            }
            else if (_state == SurgeState.Thrust || _state == SurgeState.Jump || _state == SurgeState.Pull)
            {
                _stateTime += Time.deltaTime;
                if (_stateTime >= GetActiveDuration())
                {
                    EndState();
                }
            }
        }

        public bool TryBeginCharge()
        {
            if (!CanStartCharge() || _chargeRequestPending)
            {
                return false;
            }

            if (!_playerController.IsOffline && !IsServer)
            {
                _chargeRequestPending = true;
                RequestBeginChargeServerRpc();
                return false;
            }

            if (!SpendSurge(surgeData.initialChargeCost))
            {
                return false;
            }

            BeginCharge();
            return true;
        }

        [Rpc(SendTo.Server)]
        private void RequestBeginChargeServerRpc()
        {
            _chargeRequestPending = false;
            if (!CanStartCharge() || !SpendSurge(surgeData.initialChargeCost))
            {
                return;
            }

            BeginCharge();
            ConfirmChargeClientRpc();
        }

        [ClientRpc]
        private void ConfirmChargeClientRpc()
        {
            if (IsOwner && _state == SurgeState.None)
            {
                BeginCharge();
            }
        }

        private void BeginCharge()
        {
            _chargeTime = 0f;
            _stateTime = 0f;
            _drainAccumulator = 0f;
            SetState(SurgeState.Charging);
        }

        public void CancelCharge()
        {
            if (_state == SurgeState.Charging)
            {
                EndState();
            }
        }

        public bool TryBeginSlide()
        {
            if (surgeData == null || _state != SurgeState.None || _playerController.IsPlayerDead)
            {
                return false;
            }

            _stateTime = 0f;
            SetState(SurgeState.Sliding);
            return true;
        }

        public bool TryBeginSlideJump()
        {
            if (surgeData == null || _state != SurgeState.Sliding || _stateTime < surgeData.slideSparkDelay)
            {
                return false;
            }

            _chargeTime = Mathf.Clamp01(_stateTime / Mathf.Max(surgeData.slideDuration, 0.01f)) * surgeData.maximumChargeDuration;
            _playerController.TryAddSurge(surgeData.slideLaunchSurgeReward);
            _stateTime = 0f;
            SetState(SurgeState.Jump);
            return true;
        }

        public bool TryBeginThrust()
        {
            if (_state != SurgeState.Charging || _thrustUsedThisAirTime)
            {
                return false;
            }

            _thrustUsedThisAirTime = true;
            _stateTime = 0f;
            SetState(SurgeState.Thrust);
            return true;
        }

        public bool TryBeginSurgeJump()
        {
            if (_state != SurgeState.Charging)
            {
                return false;
            }

            _stateTime = 0f;
            SetState(SurgeState.Jump);
            return true;
        }

        public Vector3 GetMovementOverrideVelocity()
        {
            if (surgeData == null || !IsMotionOverrideActive)
            {
                return Vector3.zero;
            }

            float normalizedTime = Mathf.Clamp01(_stateTime / Mathf.Max(GetActiveDuration(), 0.01f));
            float chargePercent = ChargePercent;
            float speed = _state == SurgeState.Thrust
                ? surgeData.GetThrustSpeed(chargePercent)
                : _state == SurgeState.Pull
                    ? Mathf.Max(surgeData.pullHorizontalSpeed, surgeData.pullMinimumVerticalSpeed)
                    : surgeData.GetSurgeJumpSpeed(chargePercent);
            float decay = Mathf.Lerp(1f, 0.35f, normalizedTime);

            if (_state == SurgeState.Thrust)
            {
                Vector3 direction = _playerController.transform.forward;
                return direction * speed * decay + Vector3.up * surgeData.thrustFallSpeedCap;
            }

            if (_state == SurgeState.Pull)
            {
                Vector3 direction = _playerController.transform.forward;
                return direction * speed * decay + Vector3.up * Mathf.Max(surgeData.pullMinimumVerticalSpeed, surgeData.pullVerticalSpeedPerHeight * chargePercent);
            }

            return Vector3.up * speed * decay;
        }

        public bool TryBeginPull()
        {
            if (surgeData == null || _state != SurgeState.Charging || Time.time < _lastPullTime + surgeData.pullCooldown)
            {
                return false;
            }

            _lastPullTime = Time.time;
            _stateTime = 0f;
            SetState(SurgeState.Pull);
            return true;
        }

        public void ResetAirTime()
        {
            _thrustUsedThisAirTime = false;
        }

        public void Interrupt()
        {
            _chargeTime = 0f;
            _stateTime = 0f;
            SetState(SurgeState.None);
        }

        private void UpdateCharge(float deltaTime)
        {
            _chargeTime += deltaTime;
            if (_chargeTime > surgeData.sustainedDrainDelay)
            {
                _drainAccumulator += surgeData.sustainedChargeCostPerSecond * deltaTime;
                int drain = Mathf.FloorToInt(_drainAccumulator);
                if (drain > 0)
                {
                    _drainAccumulator -= drain;
                    if (!SpendSurge(drain))
                    {
                        Interrupt();
                        return;
                    }
                }
            }

            OnChargeProgress?.Invoke(ChargePercent);
        }

        private bool CanStartCharge()
        {
            return surgeData != null &&
                   _playerController != null &&
                   !_playerController.IsPlayerDead &&
                   _playerController.IsAirborne &&
                   _state == SurgeState.None;
        }

        private bool SpendSurge(float amount)
        {
            return _playerController != null && _playerController.TrySpendSurge(amount);
        }

        private float GetActiveDuration()
        {
            float chargePercent = ChargePercent;
            if (_state == SurgeState.Thrust)
            {
                return surgeData.GetThrustDuration(chargePercent);
            }

            if (_state == SurgeState.Pull)
            {
                return Mathf.Lerp(surgeData.surgeJumpMinimumDuration, surgeData.surgeJumpMaximumDuration, Mathf.Clamp01(chargePercent));
            }

            return surgeData.GetSurgeJumpDuration(chargePercent);
        }

        private void EndState()
        {
            _chargeTime = 0f;
            _stateTime = 0f;
            _drainAccumulator = 0f;
            SetState(SurgeState.None);
        }

        private void SetState(SurgeState newState)
        {
            if (_state == newState)
            {
                return;
            }

            SurgeState previousState = _state;
            _state = newState;
            OnStateChanged?.Invoke(previousState, newState);
        }

        private bool IsOffline()
        {
            return _playerController != null && _playerController.IsOffline;
        }
    }
}
