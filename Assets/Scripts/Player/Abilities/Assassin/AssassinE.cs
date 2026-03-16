using UnityEngine;
using System;
using System.Collections;
using Category5.Player;

namespace Category5
{
    // whirlwind dash - spin-dash in a circle hitting all enemies around you
    public class AssassinE : AbilityBase
    {
        [Header("Dash Settings")]
        [SerializeField] private float dashDistance = 3f;
        [SerializeField] private float dashDuration = 0.2f;
        [SerializeField] private float hitRadius = 3f;
        [SerializeField] private LayerMask enemyLayers;
        [SerializeField] private float buffDamageMultiplier = 1.2f;

        [Header("Vfx Settings")]
        [SerializeField] private bool spawnRadiusVfx = true;
        [SerializeField] private float vfxHeightOffset = 0.1f;
        [SerializeField] private float vfxDiameterMultiplier = 1f;
        [SerializeField] private float vfxCleanupDelay = 0.15f;
        [SerializeField] private bool forceLocalParticleSimulation = true;

        private AssassinQ _assassinQ;
        private Coroutine _whirlwindRoutine;
        private int _playerLayer;
        private bool _enemyCollisionIgnored;
        private GameObject _whirlwindVfxInstance;

        public static event Action<Vector3, Vector3, float> OnWhirlwindStarted;
        public static event Action<Vector3, int> OnWhirlwindHit;
        public static event Action<Vector3> OnWhirlwindEnded;

        public override void Initialize(PlayerController player, PlayerStats stats, PlayerAbilityManager manager)
        {
            base.Initialize(player, stats, manager);
            FindDashAbility();
        }

        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            return _whirlwindRoutine == null;
        }

        public override void Execute()
        {
            if (!CanUse()) return;

            FindDashAbility();

            // match q and dodge behavior by using movement input direction first
            Vector3 direction = playerController != null ? playerController.GetMovementInputDirection() : Vector3.zero;
            direction.y = 0f;
            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }
            direction.Normalize();

            transform.rotation = Quaternion.LookRotation(direction);

            // calculate damage with potential buff
            float adjustedDamage = CalculateDamage();
            if (_assassinQ != null && _assassinQ.ConsumeDamageBuff())
            {
                adjustedDamage *= buffDamageMultiplier;
            }

            // execute the whirlwind dash on the server
            Vector3 startPosition = transform.position;
            abilityManager.TriggerAssassinEWhirlwindStartServerRpc(startPosition, direction, hitRadius);
            abilityManager.ExecuteAssassinEWhirlwindServerRpc(
                startPosition,
                direction,
                dashDistance,
                Mathf.RoundToInt(adjustedDamage),
                hitRadius,
                enemyLayers.value
            );

            if (_whirlwindRoutine != null)
            {
                StopCoroutine(_whirlwindRoutine);
            }

            SpawnWhirlwindRadiusVfx();
            _whirlwindRoutine = StartCoroutine(WhirlwindRoutine(direction));
        }

        // helper to find the AssassinQ ability for damage buff checks
        private void FindDashAbility()
        {
            if (abilityManager == null) return;

            _assassinQ = abilityManager.GetComponentInChildren<AssassinQ>();
        }

        // coroutine to handle the whirlwind dash movement and effects
        private IEnumerator WhirlwindRoutine(Vector3 direction)
        {
            _playerLayer = gameObject.layer;
            SetEnemyCollisionIgnored(true);

            float elapsed = 0f;
            float speed = dashDistance / Mathf.Max(0.01f, dashDuration);
            CharacterController controller = playerController != null ? playerController.GetComponent<CharacterController>() : null;

            // move the player over time (same as q)
            while (elapsed < dashDuration)
            {
                float step = speed * Time.deltaTime;
                transform.Rotate(Vector3.up, 1080f * Time.deltaTime);

                if (_whirlwindVfxInstance != null)
                {
                    _whirlwindVfxInstance.transform.position = transform.position + Vector3.up * vfxHeightOffset;
                }

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
            _whirlwindRoutine = null;
            OnWhirlwindEnded?.Invoke(transform.position);
        }

        private void SpawnWhirlwindRadiusVfx()
        {
            if (!spawnRadiusVfx) return;
            if (abilityData == null || abilityData.vfxPrefab == null) return;

            if (_whirlwindVfxInstance != null)
            {
                Destroy(_whirlwindVfxInstance);
            }

            Vector3 spawnPosition = transform.position + Vector3.up * vfxHeightOffset;
            _whirlwindVfxInstance = Instantiate(abilityData.vfxPrefab, spawnPosition, Quaternion.identity);
            _whirlwindVfxInstance.transform.SetParent(transform, true);

            float diameter = hitRadius * 2f * vfxDiameterMultiplier;
            _whirlwindVfxInstance.transform.localScale = new Vector3(diameter, diameter, diameter);

            ParticleSystem[] particleSystems = _whirlwindVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (forceLocalParticleSimulation)
                {
                    var main = particleSystem.main;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                }

                particleSystem.transform.position = spawnPosition;
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }

            Destroy(_whirlwindVfxInstance, dashDuration + vfxCleanupDelay);
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
            if (_whirlwindRoutine != null)
            {
                StopCoroutine(_whirlwindRoutine);
                _whirlwindRoutine = null;
            }

            if (_whirlwindVfxInstance != null)
            {
                Destroy(_whirlwindVfxInstance);
                _whirlwindVfxInstance = null;
            }

            SetEnemyCollisionIgnored(false);
        }

        public static void InvokeWhirlwindStarted(Vector3 position, Vector3 direction, float radius)
        {
            OnWhirlwindStarted?.Invoke(position, direction, radius);
        }

        public static void InvokeWhirlwindHit(Vector3 position, int hitCount)
        {
            OnWhirlwindHit?.Invoke(position, hitCount);
        }
    }
}