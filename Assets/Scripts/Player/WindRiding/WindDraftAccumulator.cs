using UnityEngine;
using System.Collections.Generic;
using Category5.Player;
using Category5.Audio;

namespace Category5.Player.WindRiding
{
    // per-player collector that sums contributions from all overlapping wind drafts each physics tick
    // add this component to the Player prefab alongside WindRiderController
    // runs FixedUpdate after the zones (DefaultExecutionOrder 100) so it can sum once, then
    // WindRiderController.ApplyDraft just reads the cached state during Update
    [DefaultExecutionOrder(100)]
    public class WindDraftAccumulator : MonoBehaviour
    {
        private struct Contribution
        {
            public Vector3 forward;       // normalized world-space draft direction
            public float strength;        // 0-1 after falloff
            public float deltaSpeed;      // pushAcceleration * strength * fixedDeltaTime (pre-cap)
            public Vector3 launchAdd;     // upward launch velocity to add this frame
            public float maxSpeedCap;     // this draft's maxDraftSpeed
            public bool wantsLaunch;      // true if the player was grounded/near-ground for this draft
        }

        private readonly List<Contribution> _contributions = new List<Contribution>();

        // cached state read by WindRiderController.ApplyDraft during Update
        public bool Active { get; private set; }
        public float DeltaSpeed { get; private set; }
        public Vector3 BlendedForward { get; private set; }
        public float BlendWeight { get; private set; }
        public Vector3 LaunchVelocityAdd { get; private set; }
        public bool WantsLaunch { get; private set; }
        public float MaxDraftSpeedCap { get; private set; }

        private bool _wasActiveLastTick;
        private PlayerController _player;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
        }

        private void FixedUpdate()
        {
            // runs after zone FixedUpdates (DefaultExecutionOrder 100) so the list is complete for this tick
            if (_contributions.Count > 0)
            {
                Accumulate();
                _contributions.Clear();
            }
            else
            {
                Active = false;
                DeltaSpeed = 0f;
                BlendedForward = Vector3.zero;
                BlendWeight = 0f;
                LaunchVelocityAdd = Vector3.zero;
                WantsLaunch = false;
                MaxDraftSpeedCap = float.MaxValue;
            }

            // fire falling-edge event once on active -> inactive transition
            if (_wasActiveLastTick && !Active && _player != null)
            {
                WindRideEvents.InvokeDraftExited(_player, Vector3.zero);
            }

            _wasActiveLastTick = Active;
        }

        // called by each overlapping WindDraftZone during its FixedUpdate
        public void RegisterContribution(
            Vector3 forward,
            float strength,
            float deltaSpeed,
            Vector3 launchAdd,
            float maxSpeedCap,
            bool wantsLaunch)
        {
            _contributions.Add(new Contribution
            {
                forward = forward,
                strength = strength,
                deltaSpeed = deltaSpeed,
                launchAdd = launchAdd,
                maxSpeedCap = maxSpeedCap,
                wantsLaunch = wantsLaunch
            });
        }

        private void Accumulate()
        {
            Active = true;

            // rising-edge event on inactive -> active transition
            if (!_wasActiveLastTick && _player != null)
            {
                Vector3 entryDir = _contributions[0].forward;
                float entryStrength = 0f;
                for (int i = 0; i < _contributions.Count; i++)
                {
                    if (_contributions[i].strength > entryStrength)
                    {
                        entryStrength = _contributions[i].strength;
                        entryDir = _contributions[i].forward;
                    }
                }
                WindRideEvents.InvokeDraftEntered(_player, entryDir, entryStrength);
            }

            float summedDelta = 0f;
            Vector3 weightedDir = Vector3.zero;
            float totalWeight = 0f;
            float tightestCap = float.MaxValue;
            Vector3 strongestLaunch = Vector3.zero;
            float strongestLaunchMag = -1f;
            bool anyWantsLaunch = false;

            for (int i = 0; i < _contributions.Count; i++)
            {
                var c = _contributions[i];
                summedDelta += c.deltaSpeed;
                weightedDir += c.forward * c.strength;
                totalWeight += c.strength;
                if (c.maxSpeedCap < tightestCap) tightestCap = c.maxSpeedCap;
                if (c.wantsLaunch)
                {
                    anyWantsLaunch = true;
                    float mag = c.launchAdd.sqrMagnitude;
                    if (mag > strongestLaunchMag)
                    {
                        strongestLaunchMag = mag;
                        strongestLaunch = c.launchAdd;
                    }
                }
            }

            DeltaSpeed = summedDelta;
            MaxDraftSpeedCap = tightestCap;
            WantsLaunch = anyWantsLaunch;
            LaunchVelocityAdd = strongestLaunch;

            if (totalWeight > 0.0001f)
            {
                BlendedForward = (weightedDir / totalWeight).normalized;
                BlendWeight = Mathf.Clamp01(totalWeight);
            }
            else
            {
                BlendedForward = Vector3.zero;
                BlendWeight = 0f;
            }
        }
    }
}
