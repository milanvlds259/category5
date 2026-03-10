using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Category5.Player;

namespace Category5.Player.WindRiding
{
    // launch pad that sends the local player into a wind tunnel when they jump on it
    // place at each end of a WindTunnel with a trigger collider
    [RequireComponent(typeof(Collider))]
    public class WindLaunchPad : MonoBehaviour
    {
        [Header("Tunnel Connection")]
        [SerializeField] private WindTunnel targetTunnel;
        [Tooltip("true = ride from t=0 to t=1, false = ride from t=1 to t=0")]
        [SerializeField] private bool launchForward = true;

        [Header("Launch Settings")]
        [SerializeField] private float launchUpwardForce = 15f;

        // players currently standing on this pad (only tracked locally)
        private HashSet<PlayerController> _playersOnPad = new HashSet<PlayerController>();
        private InputSystem_Actions _inputActions;
        private bool _inputBound;

        private void Awake()
        {
            // make sure the collider is a trigger
            var col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                Debug.LogWarning("WindLaunchPad: collider should be set to trigger, forcing it now");
                col.isTrigger = true;
            }
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
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            // only track the local player
            if (!player.IsOwner && !IsOffline(player)) return;

            _playersOnPad.Add(player);
        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            _playersOnPad.Remove(player);
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
