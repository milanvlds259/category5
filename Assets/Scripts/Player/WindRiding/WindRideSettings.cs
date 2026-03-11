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
        public float minSpeedMultiplier = 0.7f;
        
        [Tooltip("maximum speed multiplier when leaning forward (W key)")]
        [Range(1f, 2f)]
        public float maxSpeedMultiplier = 1.3f;

        [Header("Lateral Sway")]
        [Tooltip("max lateral offset from the center path in meters")]
        public float maxSwayOffset = 3.5f;
        
        [Tooltip("how quickly sway responds to A/D input")]
        public float swaySpeed = 5f;
        
        [Tooltip("how quickly sway returns to center when no input")]
        public float swayReturnSpeed = 3f;

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
    }
}
