using UnityEngine;
using Category5.Player;
using Category5.Core;

namespace Category5
{
    // fighter r - tempest engine (two-press ultimate)
    // first press: resets q and e cooldowns, boosts movement speed, starts a timer
    // second press (while active): executes a large box hitbox that deals damage scaled by distance traveled and current speed
    // if the timer runs out without a second press the ult just goes on cooldown
    public class FighterR : AbilityBase
    {
        [Header("tempest engine")]
        [SerializeField] private float ultDuration = 7f;
        [SerializeField] private float speedBoostMultiplier = 0.5f; // 50% extra speed
        [SerializeField] private float distanceScalingValue = 0.5f;
        [SerializeField] private float velocityScalingValue = 0.3f;

        [Header("big move hitbox")]
        [SerializeField] private float bigMoveBoxWidth = 6f;
        [SerializeField] private float bigMoveBoxHeight = 4f;
        [SerializeField] private float bigMoveBoxDepth = 6f;
        [SerializeField] private float bigMoveBoxForwardOffset = 2f;
        [SerializeField] private LayerMask enemyLayers = 1 << 6;

        // vfx/sfx events (use OnTempestActivate as event name to avoid clash with the instance callback method)
        public static event System.Action<Vector3> OnTempestActivate;
        public static event System.Action<Vector3, bool> OnTempestDeactivated; // bool = big move was used
        public static event System.Action<Vector3, Vector3> OnTempestBigMove;
        // fires every frame while ult is active: (remainingSeconds, totalDuration)
        public static event System.Action<float, float> OnTempestTimerTick;

        // public invoke helpers called from PlayerAbilityManager clientrpcs
        public static void OnTempestBigMoveInvoke(Vector3 pos, Vector3 fwd) => OnTempestBigMove?.Invoke(pos, fwd);
        public static void OnTempestDeactivatedInvoke(Vector3 pos, bool usedBigMove) => OnTempestDeactivated?.Invoke(pos, usedBigMove);

        // only second press sets cooldown via server rpc; first press (activation) never starts cooldown here
        public override bool StartCooldownOnExecute => false;

        // state (owner-local)
        private bool _isUltActive;
        private float _ultTimer;
        private float _distanceTraveled;
        private Vector3 _lastPosition;

        // read by PlayerAbilityManager to expose ult state to ui/other systems
        public bool IsUltActive => _isUltActive;

        public override bool CanUse()
        {
            if (playerController == null || playerController.IsDead.Value) return false;

            // second press: always allowed while ult is active (bypasses cooldown gate)
            if (_isUltActive) return true;

            // first press: standard cooldown gate
            if (!base.CanUse()) return false;
            if (abilityManager.ability3Cooldown.Value > 0) return false;
            return true;
        }

        public override void Execute()
        {
            if (!CanUse()) return;

            if (_isUltActive)
            {
                // second press - execute big move
                float currentSpeed = playerController.CurrentMovementSpeed;
                int damage = playerStats.CalculateDamage(
                    Mathf.RoundToInt(abilityData.baseDamage + (_distanceTraveled * distanceScalingValue) * (currentSpeed * velocityScalingValue))
                );

                Vector3 pos = playerController.transform.position;
                Vector3 forward = playerController.transform.forward;

                OnTempestBigMove?.Invoke(pos, forward);
                SpawnVfx(pos);
                PlayAudio(pos);

                _isUltActive = false;
                abilityManager.ExecuteTempestBigMoveServerRpc(pos, forward, damage,
                    bigMoveBoxWidth, bigMoveBoxHeight, bigMoveBoxDepth, bigMoveBoxForwardOffset, enemyLayers.value);
            }
            else
            {
                // first press - activate ult
                abilityManager.ActivateTempestEngineServerRpc();
            }
        }

        // called by PlayerAbilityManager via a targeted clientrpc when the server confirms activation
        public void OnTempestActivated()
        {
            _isUltActive = true;
            _ultTimer = ultDuration;
            _distanceTraveled = 0f;
            _lastPosition = playerController.transform.position;

            playerStats.ApplyTemporaryMultiplier("speed", speedBoostMultiplier, ultDuration);

            OnTempestActivate?.Invoke(playerController.transform.position);

            if (HitFeedbackManager.Instance != null)
                HitFeedbackManager.Instance.TriggerHeavyHit(playerController.transform.position);
        }

        private void Update()
        {
            if (!IsOwner || !_isUltActive) return;

            // accumulate distance traveled
            Vector3 currentPos = playerController.transform.position;
            _distanceTraveled += Vector3.Distance(currentPos, _lastPosition);
            _lastPosition = currentPos;

            // countdown timer
            _ultTimer -= Time.deltaTime;
            OnTempestTimerTick?.Invoke(Mathf.Max(_ultTimer, 0f), ultDuration);
            if (_ultTimer <= 0f)
            {
                // timer expired without second press
                _isUltActive = false;
                OnTempestDeactivated?.Invoke(playerController.transform.position, false);
                abilityManager.EndTempestEngineServerRpc();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (playerController == null) return;

            Vector3 pos = playerController.transform.position;
            Vector3 forward = playerController.transform.forward;
            Quaternion rot = Quaternion.LookRotation(forward);

            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.5f);
            Vector3 boxCenter = pos + forward * bigMoveBoxForwardOffset + Vector3.up * (bigMoveBoxHeight * 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(boxCenter, rot, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(bigMoveBoxWidth, bigMoveBoxHeight, bigMoveBoxDepth));
            Gizmos.matrix = Matrix4x4.identity;
        }

        // note: cooldowns managed by PlayerAbilityManager
    }
}
