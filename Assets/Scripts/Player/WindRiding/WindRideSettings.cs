using UnityEngine;

namespace Category5.Player.WindRiding
{
    // designer-tunable parameters for wind riding behavior
    // attach to WindRiderController as a serialized field
    [System.Serializable]
    public class WindRideSettings
    {
        [Header("Speed")]
        [Tooltip("base travel speed along the tunnel in m/s")]
        public float baseSpeed = 28f;
        
        [Tooltip("minimum speed multiplier when leaning backward (S key)")]
        [Range(0.1f, 1f)]
        public float minSpeedMultiplier = 0.5f;
        
        [Tooltip("maximum speed multiplier when leaning forward (W key)")]
        [Range(1f, 5f)]
        public float maxSpeedMultiplier = 2.0f;

        [Header("Surfing Logic")]
        [Tooltip("how fast the player gains speed over time while riding (m/s^2)")]
        public float acceleration = 8f;
        
        [Tooltip("how fast the player loses speed when braking (m/s^2)")]
        public float brakingDeceleration = 12f;

        [Header("Lateral Sway / Handling")]
        [Tooltip("max lateral offset from the center path in meters")]
        public float maxSwayOffset = 4.5f;
        
        [Tooltip("how quickly the board accelerates sideways")]
        public float steeringResponsiveness = 25f;
        
        [Tooltip("multiplier for lateral momentum decay (0-1)")]
        public float steeringInertia = 0.92f;

        [Header("Wind Physics")]
        [Tooltip("how strongly the wind counters gravity along the tangent direction")]
        public float windLiftMultiplier = 1.1f;
        
        [Tooltip("spring force pulling the player toward the spline path")]
        public float pathFollowStiffness = 10f;

        [Header("Entry / Exit")]
        [Tooltip("upward launch force when entering the tunnel from a launch pad")]
        public float entryLaunchForce = 15f;
        
        [Tooltip("how much speed carries over on tunnel exit (0-1)")]
        [Range(0f, 1f)]
        public float exitMomentumMultiplier = 0.8f;

        [Header("Rotation")]
        [Tooltip("how fast the player aligns to the tunnel tangent direction")]
        public float playerRotationSpeed = 8f;
        
        [Tooltip("extra rotation lean based on lateral velocity")]
        public float maxLeanAngle = 50f;

        [Tooltip("multiplier for the visual lean intensity")]
        public float leanWeight = 2.0f;

        [Header("Cloud Riding")]
        [Tooltip("distance to maintain above the cloud surface")]
        public float cloudHoverHeight = 1.0f;

        [Tooltip("how strongly the player sticks to the cloud surface height")]
        public float cloudFollowStiffness = 10.0f;

        [Header("Gliding")]
        [Tooltip("initial speed when entering glide mode")]
        public float glideBaseSpeed = 15f;

        [Tooltip("maximum horizontal speed reached at maximum dive angle")]
        public float glideMaxSpeed = 35f;

        [Tooltip("minimum horizontal speed reached when leveled out")]
        public float glideMinSpeed = 8f;

        [Tooltip("how fast speed increases when diving (m/s^2)")]
        public float glideAcceleration = 10f;

        [Tooltip("how fast speed decays when leveled out (m/s^2)")]
        public float glideDeceleration = 5f;

        [Tooltip("base downward velocity (gravity) while gliding")]
        public float glideGravity = -4f;

        [Tooltip("how much looking up counters gravity (0 = none, 1 = can fully stop falling)")]
        public float glidePitchLift = 0.8f;

        [Tooltip("maximum pitch angle (diving) for full acceleration")]
        public float glideMaxDiveAngle = 45f;
    }
}
