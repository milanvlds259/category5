using UnityEngine;
using System;
using Category5.Player;

namespace Category5.Audio
{
    // static event hub for wind riding audio/vfx events
    // gameplay scripts fire these, AudioManager or vfx systems listen
    public static class WindRideEvents
    {
        // fired when a player enters a wind tunnel
        public static event Action<PlayerController, Vector3> OnRideStarted;

        // fired when a player exits a wind tunnel
        public static event Action<PlayerController, Vector3, Vector3> OnRideEnded;

        // fired each frame during riding with progress and speed
        public static event Action<PlayerController, float, float> OnRideProgress;

        // fired when lateral sway changes, value is -1 to 1
        public static event Action<PlayerController, float> OnSwayChanged;

        // fired when a player enters a wind draft volume (rising edge)
        // args: player, world-space draft direction (normalized), initial strength (0-1)
        public static event Action<PlayerController, Vector3, float> OnDraftEntered;

        // fired when a player leaves all wind drafts (falling edge)
        // args: player, exit velocity (the ride velocity at the moment of exit)
        public static event Action<PlayerController, Vector3> OnDraftExited;

        // =====================================
        // invoke methods - call these from gameplay scripts
        // =====================================

        public static void InvokeRideStarted(PlayerController player, Vector3 position)
        {
            OnRideStarted?.Invoke(player, position);
        }

        public static void InvokeRideEnded(PlayerController player, Vector3 position, Vector3 exitVelocity)
        {
            OnRideEnded?.Invoke(player, position, exitVelocity);
        }

        public static void InvokeRideProgress(PlayerController player, float normalizedProgress, float currentSpeed)
        {
            OnRideProgress?.Invoke(player, normalizedProgress, currentSpeed);
        }

        public static void InvokeSwayChanged(PlayerController player, float swayNormalized)
        {
            OnSwayChanged?.Invoke(player, swayNormalized);
        }

        public static void InvokeDraftEntered(PlayerController player, Vector3 direction, float strength)
        {
            OnDraftEntered?.Invoke(player, direction, strength);
        }

        public static void InvokeDraftExited(PlayerController player, Vector3 exitVelocity)
        {
            OnDraftExited?.Invoke(player, exitVelocity);
        }
    }
}
