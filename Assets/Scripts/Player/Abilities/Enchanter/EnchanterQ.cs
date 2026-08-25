using UnityEngine;
using System;
using System.Collections;
using Category5.Player;
using Category5.Core;
using Unity.VisualScripting;

namespace Category5
{

    // enchanter q - dash - thrust spear that pierces enemies and grants a charge for each enemy hit. charge to dash longer distance
    public class EnchanterQ : AbilityBase
    {
        [Header("Charge Settings")]
        [SerializeField] private float minChargeTime = 0.3f;
        [SerializeField] private float maxChargeTime = 1.2f;

        [Header("Dash Settings")]
        [SerializeField] private float minDashDistance = 3f;
        [SerializeField] private float maxDashDistance = 10f;
        [SerializeField] private float dashDuration = 0.2f;
        [SerializeField] private float hitRadius = 1f;
        [SerializeField] private LayerMask enemyLayers;

        [Header("vfx")]
        [Tooltip("spawned once when the dash starts")]
        [SerializeField] private GameObject dashStartVfxPrefab;
        [Tooltip("spawned once when the dash ends")]
        [SerializeField] private GameObject dashEndVfxPrefab;
        [Tooltip("spawned once when the dash ends")]
        [SerializeField] private GameObject dashThruVfxPrefab;

        private bool _isCharging;
        private float _chargeStartTime;
        private Coroutine _dashRoutine;
        private int _playerLayer;

        public static event Action<Vector3, Vector3, float> OnDashStarted;
        public static event Action<Vector3, int> OnDashHit;
        public static event Action<Vector3> OnDashEnded;

        public static event Action<Vector3> OnChargeStarted;
        public static event Action<float, Vector3> OnChargeProgress;
        public static event Action<float, Vector3> OnChargeReleased;

        public override bool ConsumeCostOnExecute => false;
        public override bool StartCooldownOnExecute => false;

        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            if (_isCharging) return false;
            return true;
        }

        public override void Execute()
        {
            if (!CanUse()) return;

            _isCharging = true;
            _chargeStartTime = Time.time;
            OnChargeStarted?.Invoke(transform.position);
        }

        private void Update()
        {
            if (!_isCharging) return;

            float percent = Mathf.Clamp01((Time.time - _chargeStartTime) / Mathf.Max(0.01f, maxChargeTime));
            OnChargeProgress?.Invoke(percent, transform.position);
        }

        public override void OnReleased()
        {
            if (!_isCharging) return;

            _isCharging = false;

            // calculate charge level based on how long the button was held
            float heldTime = Mathf.Clamp(Time.time - _chargeStartTime, minChargeTime, maxChargeTime);
            float t = (maxChargeTime - minChargeTime) > 0f
                ? (heldTime - minChargeTime) / (maxChargeTime - minChargeTime)
                : 1f;

            float dashDistance = Mathf.Lerp(minDashDistance, maxDashDistance, t);
            OnChargeReleased?.Invoke(t, transform.position);

            Vector3 direction = playerController != null ? playerController.GetAimDirection() : transform.forward;
            direction.y = 0f;
            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }
            direction.Normalize();

            Vector3 startPosition = transform.position;

            abilityManager.ExecuteEnchanterQDashServerRpc(
                startPosition,
                direction,
                dashDistance,
                abilityData.damageCoefficient,
                hitRadius,
                enemyLayers.value
            );

            abilityManager.ApplyAbilityCostAndCooldown(AbilitySlot.Ability1, this);

            if (_dashRoutine != null)
            {
                StopCoroutine(_dashRoutine);
            }
            _dashRoutine = StartCoroutine(DashRoutine(direction, dashDistance));
        }

        private IEnumerator DashRoutine(Vector3 direction, float distance)
        {
            _playerLayer = gameObject.layer;
            SetEnemyCollisionIgnored(true);

            OnDashStarted?.Invoke(transform.position, direction, distance);
            if (dashStartVfxPrefab != null)
                Instantiate(dashStartVfxPrefab, transform.position, Quaternion.identity);

            float elapsed = 0f;
            float speed = distance / Mathf.Max(0.01f, dashDuration);
            CharacterController controller = playerController != null ? playerController.GetComponent<CharacterController>() : null;

            if (dashThruVfxPrefab != null) {
                // create the dash vfx, parent it to the player so it moves with them
                GameObject dashVFX = Instantiate(dashThruVfxPrefab, transform.position, Quaternion.LookRotation(direction));
                dashVFX.transform.parent = controller.transform;
                Destroy(dashVFX, 2); // Destroy the VFX after 2 secs
            }

            // track enemies hit during the dash
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

            OnDashEnded?.Invoke(transform.position);
            if (dashEndVfxPrefab != null)
                Instantiate(dashEndVfxPrefab, transform.position, Quaternion.identity);
        }

        // temporarily ignore collisions with enemies during the dash
        private void SetEnemyCollisionIgnored(bool ignore)
        {
            if (enemyLayers.value == 0) return;

            for (int layer = 0; layer < 32; layer++)
            {
                if ((enemyLayers.value & (1 << layer)) != 0)
                {
                    Physics.IgnoreLayerCollision(_playerLayer, layer, ignore);
                }
            }
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
