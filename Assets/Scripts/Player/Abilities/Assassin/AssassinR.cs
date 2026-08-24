using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections;
using System.Collections.Generic;
using Category5.Player;
using Category5.Player.WindRiding;
using Category5.Enemies;
using Category5.Boss;

namespace Category5
{
    // jammer star - enter a cloud-surfing state for a few seconds
    // damages enemies on contact and refunds both q charges when 3 unique enemies are hit
    // the steering is cranked up so the assassin can pivot faster than normal cloud riding
    // exits early when the player dodges or after the duration timer runs out
    public class AssassinR : AbilityBase
    {
        [Header("Jammer Star")]
        [SerializeField] private float jammerStarDuration = 5f;
        [SerializeField] private float jammerStarHitRadius = 2.5f;
        [SerializeField] private int jammerStarHitThreshold = 3;

        [Header("Steering Overrides")]
        [Tooltip("cloud mode steering responsiveness while jammer star is active (higher = easier to turn)")]
        [SerializeField] private float steeringResponsivenessOverride = 50f;
        [Tooltip("cloud mode rotation speed while jammer star is active")]
        [SerializeField] private float rotationSpeedOverride = 14f;

        [Header("Damage")]
        [Tooltip("layers to check for enemies while jammer star is active")]
        [SerializeField] private LayerMask enemyLayers;

        private AssassinQ _assassinQ;
        private Coroutine _jammerStarRoutine;
        private bool _isJammerStarActive;
        private WindRiderController _windRider;
        private WindRiderController.CloudOverrideSnapshot _steeringSnapshot;
        private readonly HashSet<int> _uniqueEnemiesHit = new HashSet<int>();

        public static event Action<Vector3> OnJammerStarStarted;
        public static event Action<Vector3> OnJammerStarHit;
        public static event Action<Vector3> OnJammerStarEnded;
        public static event Action OnJammerStarRefund;

        public override void Initialize(PlayerController player, PlayerStats stats, PlayerAbilityManager manager)
        {
            base.Initialize(player, stats, manager);
            FindDashAbility();
        }

        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            return !_isJammerStarActive && _jammerStarRoutine == null;
        }

        public override void Execute()
        {
            if (!CanUse()) return;

            FindDashAbility();

            // face the aim direction so the surfer starts pointed where the player is aiming
            Vector3 direction = playerController != null ? playerController.GetAimDirection() : transform.forward;
            direction.y = 0f;
            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }
            direction.Normalize();
            transform.rotation = Quaternion.LookRotation(direction);

            // kick off cloud riding and apply the easier-to-turn steering overrides
            _windRider = playerController != null ? playerController.GetComponent<WindRiderController>() : null;
            if (_windRider != null)
            {
                _windRider.StartCloudRiding();
                _steeringSnapshot = _windRider.ApplyCloudOverride(steeringResponsivenessOverride, rotationSpeedOverride);
            }

            _jammerStarRoutine = StartCoroutine(JammerStarRoutine());
            OnJammerStarStarted?.Invoke(transform.position);
        }

        private void FindDashAbility()
        {
            if (abilityManager == null) return;

            _assassinQ = abilityManager.GetComponentInChildren<AssassinQ>();
        }

        // duration + dodge-to-exit handler
        // per-frame enemy detection runs here on the owner so the server rpc gets fresh enemy ids
        private IEnumerator JammerStarRoutine()
        {
            _isJammerStarActive = true;
            _uniqueEnemiesHit.Clear();

            float startTime = Time.time;
            float detectionAccumulator = 0f;
            const float detectionInterval = 0.1f;

            while (_isJammerStarActive)
            {
                // dodge exits the state early
                if (playerController != null && playerController.IsDodging)
                {
                    EndJammerStar();
                    yield break;
                }

                // duration timeout
                if (Time.time >= startTime + jammerStarDuration)
                {
                    EndJammerStar();
                    yield break;
                }

                // detect new enemies on the owner and report each unique one to the server
                detectionAccumulator += Time.deltaTime;
                if (detectionAccumulator >= detectionInterval)
                {
                    detectionAccumulator = 0f;
                    DetectAndReportEnemies();
                }

                yield return null;
            }
        }

        // owner-side per-frame enemy detection
        // collects any new unique enemy/boss ids and fires one server rpc per id
        private void DetectAndReportEnemies()
        {
            if (!IsOwner) return;

            Collider[] colliders = enemyLayers.value == 0
                ? Physics.OverlapSphere(transform.position, jammerStarHitRadius)
                : Physics.OverlapSphere(transform.position, jammerStarHitRadius, enemyLayers.value);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null) continue;

                NetworkObject netObj = collider.GetComponentInParent<NetworkObject>();
                if (netObj == null) continue;

                int id = netObj.GetInstanceID();
                if (_uniqueEnemiesHit.Contains(id)) continue;

                // only count enemies or bosses (avoid double-counting via colliders on child objects)
                bool isDamageable = collider.GetComponentInParent<EnemyBase>() != null
                    || collider.GetComponentInParent<BossBase>() != null;
                if (!isDamageable) continue;

                _uniqueEnemiesHit.Add(id);
                OnJammerStarHit?.Invoke(collider.transform.position);
                abilityManager.RequestAssassinJammerStarHitServerRpc(new NetworkObjectReference(netObj));
            }
        }

        // cleanup helper - called from duration timeout, dodge exit, or OnDisable
        private void EndJammerStar()
        {
            _isJammerStarActive = false;
            _jammerStarRoutine = null;

            if (_windRider != null)
            {
                _windRider.EndCloudRiding();
                if (_steeringSnapshot != null)
                {
                    _windRider.RestoreCloudOverride(_steeringSnapshot);
                    _steeringSnapshot = null;
                }
            }

            // free the server-side session dict for this player
            if (abilityManager != null && IsOwner)
            {
                abilityManager.EndAssassinJammerStarServerRpc();
            }

            OnJammerStarEnded?.Invoke(transform.position);
        }

        // server callback from RequestAssassinJammerStarHitServerRpc
        // called on the owner when the server confirms the threshold was hit
        public void OnJammerStarRefundGranted()
        {
            if (_assassinQ != null)
            {
                _assassinQ.ResetAllCharges();
            }
            OnJammerStarRefund?.Invoke();
        }

        private void OnDisable()
        {
            if (_jammerStarRoutine != null)
            {
                StopCoroutine(_jammerStarRoutine);
                _jammerStarRoutine = null;
            }

            _isJammerStarActive = false;
            EndJammerStar();
        }

        public static void InvokeJammerStarHit(Vector3 position)
        {
            OnJammerStarHit?.Invoke(position);
        }
    }
}