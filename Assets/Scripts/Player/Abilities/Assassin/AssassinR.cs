using UnityEngine;
using System;
using System.Collections;
using Category5.Player;

namespace Category5
{
    // convergence dash - dash to a target location and create a burst explosion around the landing spot. 
    // if the explosion hits 3+ enemies both Q charges reset immediately.
    public class AssassinR : AbilityBase
    {
        [Header("Dash Settings")]
        [SerializeField] private float dashDistance = 10f;
        [SerializeField] private float dashDuration = 0.18f;
        [SerializeField] private float hitRadius = 4f;
        [SerializeField] private LayerMask enemyLayers;
        [SerializeField] private float buffDamageMultiplier = 1.2f;

        private AssassinQ _assassinQ;
        private Coroutine _convergenceRoutine;
        private int _playerLayer;
        private bool _enemyCollisionIgnored;

        public static event Action<Vector3, Vector3, float> OnConvergenceStarted;
        public static event Action<Vector3, int, bool> OnConvergenceExplosion;
        public static event Action<Vector3> OnConvergenceEnded;

        public override void Initialize(PlayerController player, PlayerStats stats, PlayerAbilityManager manager)
        {
            base.Initialize(player, stats, manager);
            FindDashAbility();
        }

        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            return _convergenceRoutine == null;
        }

        public override void Execute()
        {
            if (!CanUse()) return;

            FindDashAbility();

            Vector3 direction = playerController != null ? playerController.GetAimDirection() : transform.forward;
            direction.y = 0f;
            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }
            direction.Normalize();

            transform.rotation = Quaternion.LookRotation(direction);

            float adjustedDamage = CalculateDamage();
            if (_assassinQ != null && _assassinQ.ConsumeDamageBuff())
            {
                adjustedDamage *= buffDamageMultiplier;
            }

            Vector3 startPosition = transform.position;
            abilityManager.ExecuteAssassinRConvergenceServerRpc(
                startPosition,
                direction,
                dashDistance,
                Mathf.RoundToInt(adjustedDamage),
                hitRadius,
                enemyLayers.value
            );

            if (_convergenceRoutine != null)
            {
                StopCoroutine(_convergenceRoutine);
            }

            _convergenceRoutine = StartCoroutine(ConvergenceRoutine(direction));
        }

        private void FindDashAbility()
        {
            if (abilityManager == null) return;

            _assassinQ = abilityManager.GetComponentInChildren<AssassinQ>();
        }

        private IEnumerator ConvergenceRoutine(Vector3 direction)
        {
            _playerLayer = gameObject.layer;
            SetEnemyCollisionIgnored(true);
            OnConvergenceStarted?.Invoke(transform.position, direction, dashDistance);

            float elapsed = 0f;
            float speed = dashDistance / Mathf.Max(0.01f, dashDuration);
            CharacterController controller = playerController != null ? playerController.GetComponent<CharacterController>() : null;

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
            _convergenceRoutine = null;
            OnConvergenceEnded?.Invoke(transform.position);
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
            if (_convergenceRoutine != null)
            {
                StopCoroutine(_convergenceRoutine);
                _convergenceRoutine = null;
            }

            SetEnemyCollisionIgnored(false);
        }

        public static void InvokeConvergenceExplosion(Vector3 position, int hitCount, bool resetCharges)
        {
            OnConvergenceExplosion?.Invoke(position, hitCount, resetCharges);
        }
    }
}