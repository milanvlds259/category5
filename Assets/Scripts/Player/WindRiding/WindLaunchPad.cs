using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Category5.Player;
using Category5.Audio;
using Category5.Map;
using Category5.Core;

namespace Category5.Player.WindRiding
{
    // launch pad that sends the local player into a wind tunnel when they jump on it
    // place at each end of a WindTunnel with a trigger collider
    [RequireComponent(typeof(Collider))]
    public class WindLaunchPad : MonoBehaviour
    {
        [Header("Tunnel Connection")]
        [SerializeField] public WindTunnel targetTunnel;
        [Tooltip("true = ride from t=0 to t=1, false = ride from t=1 to t=0")]
        [SerializeField] private bool launchForward = true;

        [Header("Launch Settings")]
        [SerializeField] private float launchUpwardForce = 15f;

        [Header("Room Context (set by MapGenerator)")]
        [Tooltip("the room this pad is in (source room)")]
        [SerializeField] private StormRoom sourceRoom;
        [Tooltip("the room this pad leads to (destination room)")]
        [SerializeField] private StormRoom destinationRoom;

        // players currently standing on this pad (only tracked locally)
        private HashSet<PlayerController> _playersOnPad = new HashSet<PlayerController>();
        private InputSystem_Actions _inputActions;
        private bool _inputBound;

        private bool notYetOut; // Set when the player ends wind ride, used to prevent immediately re-launching if they are still on the end pad

        private void Awake()
        {
            // make sure the collider is a trigger
            var col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                Debug.LogWarning("WindLaunchPad: collider should be set to trigger, forcing it now");
                col.isTrigger = true;
            }

            WindRideEvents.OnRideEnded += OnThisRideEnded;
        }

        private void OnEnable()
        {
            if (_inputActions == null)
                _inputActions = new InputSystem_Actions();

            _inputActions.Player.Enable();
            _inputActions.Player.Jump.performed += OnJumpPerformed;
            _inputBound = true;
        }

        private void OnDisable()
        {
            if (_inputBound)
            {
                _inputActions.Player.Jump.performed -= OnJumpPerformed;
                _inputActions.Player.Disable();
                _inputBound = false;
            }
            _playersOnPad.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[WindRide] OnTriggerEnter fired on {gameObject.name} — hit: {other.gameObject.name} (layer: {LayerMask.LayerToName(other.gameObject.layer)})");
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            // only track the local player
            if (!player.IsOwner && !IsOffline(player)) return;

            // block launch if destination room is hidden
            if (!IsDestinationAccessible())
            {
                return;
            }

            _playersOnPad.Add(player);

            WindRiderController rider = player.GetComponent<WindRiderController>();
            if (!notYetOut) // Rider isn't already riding this tunnel
            {
                // Auto launch
                rider.StartRiding(targetTunnel, launchForward, launchUpwardForce);

                // Add player to the tunnel's rider list
                targetTunnel.riders.Add(player);

                // remove from pad tracking since they are now airborne
                _playersOnPad.Remove(player);
            }

        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            _playersOnPad.Remove(player);

            if (notYetOut)
            {
                targetTunnel.riders.Remove(player);
            }
            notYetOut = false;
        }

        private void OnThisRideEnded(PlayerController player, Vector3 position, Vector3 exitVelocity)
        {
            if (targetTunnel.riders.Contains(player))
            {
                notYetOut = true;

                // notify RoomTransitionManager that player arrived at destination room
                if (destinationRoom != null && RoomTransitionManager.Instance != null)
                {
                    RoomTransitionManager.Instance.OnPlayersArrived(destinationRoom);
                }
            }
        }

        private void OnJumpPerformed(InputAction.CallbackContext ctx)
        {
            if (targetTunnel == null)
            {
                Debug.LogError("WindLaunchPad: no target tunnel assigned");
                return;
            }

            // find the local player on the pad and launch them
            foreach (var player in _playersOnPad)
            {
                if (player == null || player.IsDead.Value) continue;

                var rider = player.GetComponent<WindRiderController>();
                if (rider == null)
                {
                    Debug.LogError("WindLaunchPad: player is missing WindRiderController component");
                    continue;
                }

                // dont launch if already riding
                if (rider.IsWindRiding) continue;

                rider.StartRiding(targetTunnel, launchForward, launchUpwardForce);

                // remove from pad tracking since they are now airborne
                _playersOnPad.Remove(player);
                break;
            }
        }

        public void ConfigureTunnel(WindTunnel tunnel, bool forward)
        {
            targetTunnel = tunnel;
            launchForward = forward;
        }

        /// <summary>
        /// sets the source and destination rooms for this launch pad (called by MapGenerator)
        /// </summary>
        public void ConfigureRooms(StormRoom source, StormRoom destination)
        {
            sourceRoom = source;
            destinationRoom = destination;
        }

        /// <summary>
        /// returns the destination room for this pad
        /// </summary>
        public StormRoom GetDestinationRoom() => destinationRoom;

        /// <summary>
        /// returns the source room for this pad
        /// </summary>
        public StormRoom GetSourceRoom() => sourceRoom;

        /// <summary>
        /// checks if the destination room is accessible (Active or Cleared)
        /// </summary>
        public bool IsDestinationAccessible()
        {
            if (destinationRoom == null) return true; // no room context = always accessible
            return destinationRoom.CurrentState == StormRoomState.Active ||
                   destinationRoom.CurrentState == StormRoomState.Cleared;
        }

        // simple offline check matching PlayerController's pattern
        private bool IsOffline(PlayerController player)
        {
            return Unity.Netcode.NetworkManager.Singleton == null ||
                   !Unity.Netcode.NetworkManager.Singleton.IsListening;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f);
            Gizmos.DrawWireCube(transform.position, new Vector3(2f, 0.3f, 2f));

            // draw arrow showing launch direction
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 3f, 0.3f);

            // draw line to tunnel if assigned
            if (targetTunnel != null)
            {
                Gizmos.color = Color.yellow;
                float t = launchForward ? 0f : 1f;
                Vector3 tunnelEnd = targetTunnel.EvaluatePosition(t);
                Gizmos.DrawLine(transform.position, tunnelEnd);
            }
        }
#endif
    }
}
