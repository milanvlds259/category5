using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using Unity.Mathematics;
using Category5.Audio;
using Category5.Core;
using Category5.Player;
using Category5.Player.WindRiding;

namespace Category5.Player.Van
{
    [RequireComponent(typeof(Collider))]
    public class VanExitController : MonoBehaviour
    {
        [Header("Exit Position")]
        [Tooltip("Transform where the player will be placed before starting the ride")]
        [SerializeField] private Transform exitPosition;

        [Header("Launch Settings")]
        [SerializeField] private float launchUpwardForce = 15f;

        [Header("Prompt")]
        [Tooltip("UI element to show when the player is near the exit door")]
        [SerializeField] private GameObject exitPrompt;

        private PlayerController _currentPlayer;
        private GameObject _activeTunnelObject;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                col.isTrigger = true;
            }

            WindRideEvents.OnRideEnded += OnAnyRideEnded;
        }

        private void OnDestroy()
        {
            WindRideEvents.OnRideEnded -= OnAnyRideEnded;
        }

        private void OnAnyRideEnded(PlayerController player, Vector3 position, Vector3 exitVelocity)
        {
            if (player == _currentPlayer && _activeTunnelObject != null)
            {
                Destroy(_activeTunnelObject);
                _activeTunnelObject = null;
            }
        }

        private void Update()
        {
            if (_currentPlayer == null) return;
            if (_currentPlayer.IsDead.Value) return;

            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                WindRiderController rider = _currentPlayer.GetComponent<WindRiderController>();
                if (rider == null || rider.IsWindRiding) return;

                PlayerSpawnPoint spawnPoint = PlayerSpawnPoint.GetNextIslandSpawnPoint();
                if (spawnPoint == null)
                {
                    Debug.LogError("VanExitController: no island spawn point found");
                    return;
                }

                // build runtime tunnel from exit position to the spawn point
                WindTunnel tunnel = BuildExitTunnel(exitPosition.position, spawnPoint.transform.position);
                if (tunnel == null) return;

                // teleport player to exit position to clear van geometry
                CharacterController cc = _currentPlayer.GetComponent<CharacterController>();
                if (cc != null)
                    cc.enabled = false;

                _currentPlayer.transform.position = exitPosition.position;
                _currentPlayer.transform.rotation = exitPosition.rotation;

                if (cc != null)
                    cc.enabled = true;

                rider.StartRiding(tunnel, true, launchUpwardForce);
                ClearCurrentPlayer();
            }
        }

        private WindTunnel BuildExitTunnel(Vector3 start, Vector3 end)
        {
            _activeTunnelObject = new GameObject("VanExitTunnel_Runtime");

            SplineContainer container = _activeTunnelObject.AddComponent<SplineContainer>();
            WindTunnel tunnel = _activeTunnelObject.AddComponent<WindTunnel>();

            Spline spline = container.Spline;
            spline.Clear();

            spline.Add(new BezierKnot(new float3(start.x, start.y, start.z)));
            spline.Add(new BezierKnot(new float3(end.x, end.y, end.z)));

            return tunnel;
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            if (!player.IsOwner && !IsOffline()) return;

            _currentPlayer = player;

            if (exitPrompt != null)
                exitPrompt.SetActive(true);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            if (player != _currentPlayer) return;

            ClearCurrentPlayer();
        }

        private void ClearCurrentPlayer()
        {
            _currentPlayer = null;

            if (exitPrompt != null)
                exitPrompt.SetActive(false);
        }

        private bool IsOffline()
        {
            return Unity.Netcode.NetworkManager.Singleton == null ||
                   !Unity.Netcode.NetworkManager.Singleton.IsListening;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawWireCube(transform.position, new Vector3(2f, 0.3f, 2f));

            if (exitPosition != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, exitPosition.position);
                Gizmos.DrawWireSphere(exitPosition.position, 0.5f);
                Gizmos.DrawLine(exitPosition.position, exitPosition.position + Vector3.down * 3f);
            }
        }
#endif
    }
}
