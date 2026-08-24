using UnityEngine;
using System;
using System.Collections;
using Category5.Player;
using Category5.WeakPoints;

namespace Category5
{
    // blade dance - spin-dash in a circle hitting all enemies around you
    // breaking a weak point while blade dance is active refunds one q charge
    public class AssassinE : AbilityBase
    {
        [Header("Dash Settings")]
        [SerializeField] private float dashDistance = 3f;
        [SerializeField] private float dashDuration = 0.2f;
        [SerializeField] private float hitRadius = 3f;
        [SerializeField] private LayerMask enemyLayers;

        [Header("Vfx Settings")]
        [SerializeField] private bool spawnRadiusVfx = true;
        [SerializeField] private float vfxHeightOffset = 0.1f;
        [SerializeField] private float vfxDiameterMultiplier = 1f;
        [SerializeField] private float vfxCleanupDelay = 0.15f;
        [SerializeField] private bool forceLocalParticleSimulation = true;

        private AssassinQ _assassinQ;
        private Coroutine _bladeDanceRoutine;
        private int _playerLayer;
        private bool _enemyCollisionIgnored;
        private GameObject _bladeDanceVfxInstance;
        private bool _isBladeDanceActive;

        public static event Action<Vector3, Vector3, float> OnBladeDanceStarted;
        public static event Action<Vector3, int> OnBladeDanceHit;
        public static event Action<Vector3> OnBladeDanceEnded;
        public static event Action<Vector3> OnBladeDanceWeakPointBroken;

        public override void Initialize(PlayerController player, PlayerStats stats, PlayerAbilityManager manager)
        {
            base.Initialize(player, stats, manager);
            FindDashAbility();
        }

        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            return _bladeDanceRoutine == null;
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

            // calculate damage coefficient
            // note: q damage buff used to apply here but now applies only to basic attacks
            float coefficient = abilityData.damageCoefficient;

            // execute the blade dance dash on the server
            Vector3 startPosition = transform.position;
            abilityManager.TriggerAssassinEWhirlwindStartServerRpc(startPosition, direction, hitRadius);
            abilityManager.ExecuteAssassinEWhirlwindServerRpc(
                startPosition,
                direction,
                dashDistance,
                coefficient,
                hitRadius,
                enemyLayers.value
            );

            if (_bladeDanceRoutine != null)
            {
                StopCoroutine(_bladeDanceRoutine);
            }

            SpawnBladeDanceRadiusVfx();
            _bladeDanceRoutine = StartCoroutine(BladeDanceRoutine(direction));
        }

        // helper to find the AssassinQ ability for charge refund
        private void FindDashAbility()
        {
            if (abilityManager == null) return;

            _assassinQ = abilityManager.GetComponentInChildren<AssassinQ>();
        }

        // coroutine to handle the blade dance dash movement and effects
        private IEnumerator BladeDanceRoutine(Vector3 direction)
        {
            _playerLayer = gameObject.layer;
            SetEnemyCollisionIgnored(true);
            _isBladeDanceActive = true;

            float elapsed = 0f;
            float speed = dashDistance / Mathf.Max(0.01f, dashDuration);
            CharacterController controller = playerController != null ? playerController.GetComponent<CharacterController>() : null;

            // move the player over time (same as q)
            while (elapsed < dashDuration)
            {
                float step = speed * Time.deltaTime;
                transform.Rotate(Vector3.up, 1080f * Time.deltaTime);

                if (_bladeDanceVfxInstance != null)
                {
                    _bladeDanceVfxInstance.transform.position = transform.position + Vector3.up * vfxHeightOffset;
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
            _bladeDanceRoutine = null;
            _isBladeDanceActive = false;
            OnBladeDanceEnded?.Invoke(transform.position);
        }

        private void SpawnBladeDanceRadiusVfx()
        {
            if (!spawnRadiusVfx) return;
            if (abilityData == null || abilityData.vfxPrefab == null) return;

            if (_bladeDanceVfxInstance != null)
            {
                Destroy(_bladeDanceVfxInstance);
            }

            Vector3 spawnPosition = transform.position + Vector3.up * vfxHeightOffset;
            _bladeDanceVfxInstance = Instantiate(abilityData.vfxPrefab, spawnPosition, Quaternion.identity);
            _bladeDanceVfxInstance.transform.SetParent(transform, true);

            float diameter = hitRadius * 2f * vfxDiameterMultiplier;
            _bladeDanceVfxInstance.transform.localScale = new Vector3(diameter, diameter, diameter);

            ParticleSystem[] particleSystems = _bladeDanceVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
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

            Destroy(_bladeDanceVfxInstance, dashDuration + vfxCleanupDelay);
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

        private void OnEnable()
        {
            WeakPoint.OnWeakPointBroken += HandleWeakPointBroken;
        }

        private void OnDisable()
        {
            WeakPoint.OnWeakPointBroken -= HandleWeakPointBroken;

            if (_bladeDanceRoutine != null)
            {
                StopCoroutine(_bladeDanceRoutine);
                _bladeDanceRoutine = null;
            }

            _isBladeDanceActive = false;

            if (_bladeDanceVfxInstance != null)
            {
                Destroy(_bladeDanceVfxInstance);
                _bladeDanceVfxInstance = null;
            }

            SetEnemyCollisionIgnored(false);
        }

        // weak point break handler - refunds one q charge if blade dance is currently active and we broke it
        private void HandleWeakPointBroken(WeakPoint weakPoint, ulong attackerClientId, Vector3 position)
        {
            if (!_isBladeDanceActive) return;
            if (attackerClientId != OwnerClientId) return;
            if (_assassinQ == null) return;

            _assassinQ.RefundOneCharge();
            OnBladeDanceWeakPointBroken?.Invoke(position);
        }

        public static void InvokeBladeDanceStarted(Vector3 position, Vector3 direction, float radius)
        {
            OnBladeDanceStarted?.Invoke(position, direction, radius);
        }

        public static void InvokeBladeDanceHit(Vector3 position, int hitCount)
        {
            OnBladeDanceHit?.Invoke(position, hitCount);
        }
    }
}