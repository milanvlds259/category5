using UnityEngine;
using Unity.Netcode;
using Category5.Player;

namespace Category5.Player.WindRiding
{
    // networked wind draft volume that pushes gliding players along its forward axis
    // cylinder shape (radius + length along transform.forward). Grounded players are launched
    // upward into gliding; gliding players are accelerated and steered toward the draft direction.
    // detection uses Physics.OverlapCapsule in FixedUpdate (owner-authoritative per player)
    // place in scenes or spawn at runtime; requires a NetworkObject
    [RequireComponent(typeof(NetworkObject))]
    public class WindDraftZone : NetworkBehaviour
    {
        [Header("Config")]
        [Tooltip("data asset with all tuning + vfx/sfx. If null, defaults are used.")]
        [SerializeField] private WindDraftData data;

        [Header("Runtime VFX")]
        [Tooltip("if true, instantiate the data.vfxPrefab as a child on spawn; if false, expect a pre-placed child")]
        [SerializeField] private bool spawnVfxOnEnable = true;

        // cached/derived values (fall back to built-in defaults if data is null)
        private float _pushAcceleration;
        private float _maxDraftSpeed;
        private float _groundLaunchUpForce;
        private float _launchClearThreshold;
        private float _endFalloffRatio;
        private float _cylinderRadius;
        private float _cylinderLength;
        private bool _invertFalloff;

        private GameObject _vfxInstance;
        private ParticleSystem[] _vfxParticleSystems;

        private void Awake()
        {
            ApplyData();
        }

        public override void OnNetworkSpawn()
        {
            ApplyData();

            if (spawnVfxOnEnable && data != null && data.vfxPrefab != null)
            {
                SpawnVfx();
            }
        }

        private void ApplyData()
        {
            if (data != null)
            {
                _pushAcceleration = data.pushAcceleration;
                _maxDraftSpeed = data.maxDraftSpeed;
                _groundLaunchUpForce = data.groundLaunchUpForce;
                _launchClearThreshold = data.launchClearThreshold;
                _endFalloffRatio = data.endFalloffRatio;
                _cylinderRadius = data.cylinderRadius;
                _cylinderLength = data.cylinderLength;
                _invertFalloff = data.invertFalloff;
            }
            else
            {
                // built-in defaults matching WindDraftData defaults
                _pushAcceleration = 12f;
                _maxDraftSpeed = 45f;
                _groundLaunchUpForce = 18f;
                _launchClearThreshold = 1.5f;
                _endFalloffRatio = 0.2f;
                _cylinderRadius = 2.5f;
                _cylinderLength = 8f;
                _invertFalloff = false;
            }
        }

        private void SpawnVfx()
        {
            Vector3 spawnPos = transform.position;
            _vfxInstance = Instantiate(data.vfxPrefab, spawnPos, transform.rotation);
            _vfxInstance.transform.SetParent(transform, false);
            _vfxInstance.transform.localPosition = Vector3.zero;
            _vfxInstance.transform.localRotation = Quaternion.identity;

            // scale the vfx to the cylinder dimensions (assumes prefab authored as unit-cylinder along +Z)
            _vfxInstance.transform.localScale = new Vector3(_cylinderRadius, _cylinderRadius, _cylinderLength);

            _vfxParticleSystems = _vfxInstance.GetComponentsInChildren<ParticleSystem>(true);
            TuneVfx();
            foreach (var ps in _vfxParticleSystems)
            {
                ps.Clear(true);
                ps.Play(true);
            }
        }

        private void TuneVfx()
        {
            if (_vfxParticleSystems == null) return;
            float speedScale = (data != null ? data.vfxSpeedMultiplier : 1f) * Mathf.Max(0.1f, _pushAcceleration / 12f);

            foreach (var ps in _vfxParticleSystems)
            {
                var main = ps.main;
                main.startSpeedMultiplier = speedScale;
                main.startSpeed = Mathf.Max(0.5f, _pushAcceleration * 0.5f);
                var em = ps.emission;
                em.rateOverTimeMultiplier = Mathf.Max(2f, _pushAcceleration * 1.5f);
            }
        }

        private void FixedUpdate()
        {
            // offline (no network) or server/host runs detection; in a hosted game, each client
            // detects its own owner-authoritative player locally, so run on any machine that has one
            // simple rule: run whenever there is a local player. detection is per-owner.
            Vector3 start, end;
            GetCapsuleEndpoints(out start, out end);

            Collider[] hits = Physics.OverlapCapsule(start, end, _cylinderRadius);
            foreach (Collider col in hits)
            {
                PlayerController player = col.GetComponentInParent<PlayerController>();
                if (player == null) continue;

                // owner-only (or offline) — matches WindLaunchPad / WindRiderController pattern
                if (!player.IsOwner && !IsOffline()) continue;
                if (player.IsPlayerDead) continue;

                WindDraftAccumulator accumulator = player.GetComponent<WindDraftAccumulator>();
                if (accumulator == null) continue;

                WindDraftZone.RegisterContributionTo(player, accumulator, this);
            }
        }

        // split out so the per-player contribution computation is testable and clear
        private static void RegisterContributionTo(PlayerController player, WindDraftAccumulator accumulator, WindDraftZone zone)
        {
            Vector3 forward = zone.transform.forward.normalized;

            // normalized position along the cylinder's local Z axis (0 at base, 1 at tip)
            Vector3 localPos = zone.transform.InverseTransformPoint(player.transform.position);
            float t = Mathf.Clamp01(localPos.z / zone._cylinderLength);
            float strength = zone.FalloffStrength(t);

            float deltaSpeed = zone._pushAcceleration * strength * Time.fixedDeltaTime;

            // launch check: grounded or below the clear threshold
            float heightAboveGround = player.GetHeightAboveGround();
            bool wantsLaunch = heightAboveGround < zone._launchClearThreshold;

            Vector3 launchAdd = Vector3.zero;
            if (wantsLaunch)
            {
                launchAdd = Vector3.up * zone._groundLaunchUpForce * strength * Time.fixedDeltaTime;
            }

            accumulator.RegisterContribution(
                forward: forward,
                strength: strength,
                deltaSpeed: deltaSpeed,
                launchAdd: launchAdd,
                maxSpeedCap: zone._maxDraftSpeed,
                wantsLaunch: wantsLaunch);
        }

        // smoothstep falloff at both ends of the cylinder
        // returns 0 at t=0 and t=1, 1 in the middle (outside the falloff bands)
        private float FalloffStrength(float t)
        {
            return WindDraftMath.FalloffStrength(t, _endFalloffRatio, _invertFalloff);
        }

        // world-space endpoints of the detection capsule along the forward axis
        private void GetCapsuleEndpoints(out Vector3 start, out Vector3 end)
        {
            Vector3 fwd = transform.forward;
            Vector3 basePos = transform.position;
            start = basePos;
            end = basePos + fwd * _cylinderLength;
        }

        private bool IsOffline()
        {
            return Unity.Netcode.NetworkManager.Singleton == null ||
                   !Unity.Netcode.NetworkManager.Singleton.IsListening;
        }

        private void OnValidate()
        {
            // keep derived values in sync while editing in the inspector before play
            if (data != null) ApplyData();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            ApplyData();
            Vector3 start, end;
            GetCapsuleEndpoints(out start, out end);

            // falloff gradient along the length: green at full strength, fading to grey at the ends
            int segments = 24;
            Vector3 prev = start;
            Vector3 fwd = transform.forward;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float s = FalloffStrength(t);
                Gizmos.color = new Color(0.4f * (1f - s) + 0.2f * s, 0.8f * s + 0.3f * (1f - s), 1f, 0.9f);
                Vector3 cur = start + fwd * (_cylinderLength * t);
                if (i > 0)
                {
                    // draw a small ring at this segment
                    DrawRing(prev, fwd, _cylinderRadius, 12);
                }
                prev = cur;
            }

            // main capsule wireframe
            Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.4f);
            DrawCapsuleWire(start, end, _cylinderRadius);

            // forward arrow at the tip
            Gizmos.color = Color.cyan;
            Vector3 tip = end;
            Gizmos.DrawLine(start, tip);
            Vector3 right = Vector3.Cross(fwd, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, fwd).normalized;
            Gizmos.DrawLine(tip, tip - fwd * 0.6f + right * 0.3f);
            Gizmos.DrawLine(tip, tip - fwd * 0.6f - right * 0.3f);
            Gizmos.DrawLine(tip, tip - fwd * 0.6f + up * 0.3f);
            Gizmos.DrawLine(tip, tip - fwd * 0.6f - up * 0.3f);

            // strength label at the middle
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(start + fwd * (_cylinderLength * 0.5f) + Vector3.up * _cylinderRadius,
                $"Wind Draft\npush: {_pushAcceleration} m/s²\ncap: {_maxDraftSpeed} m/s\nlen: {_cylinderLength}m  r: {_cylinderRadius}m");
            #endif
        }

        private static void DrawRing(Vector3 center, Vector3 axis, float radius, int count)
        {
            Vector3 n1 = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.99f ? Vector3.right : Vector3.up).normalized;
            Vector3 n2 = Vector3.Cross(axis, n1).normalized;
            Vector3 prev = center + n1 * radius;
            for (int i = 1; i <= count; i++)
            {
                float a = (i / (float)count) * Mathf.PI * 2f;
                Vector3 cur = center + (n1 * Mathf.Cos(a) + n2 * Mathf.Sin(a)) * radius;
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }

        private static void DrawCapsuleWire(Vector3 start, Vector3 end, float radius)
        {
            Vector3 axis = (end - start).normalized;
            Vector3 n1 = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.99f ? Vector3.right : Vector3.up).normalized;
            Vector3 n2 = Vector3.Cross(axis, n1).normalized;

            // two ring caps
            DrawRing(start, axis, radius, 24);
            DrawRing(end, axis, radius, 24);

            // 4 longitudinal lines
            Gizmos.DrawLine(start + n1 * radius, end + n1 * radius);
            Gizmos.DrawLine(start - n1 * radius, end - n1 * radius);
            Gizmos.DrawLine(start + n2 * radius, end + n2 * radius);
            Gizmos.DrawLine(start - n2 * radius, end - n2 * radius);
        }
#endif
    }
}
