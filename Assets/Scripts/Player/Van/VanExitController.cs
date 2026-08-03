using UnityEngine;
using UnityEngine.InputSystem;
using Category5.Core;
using Category5.Player;
using Category5.Player.WindRiding;
using System;

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

        // fired when any player exits the van — used by StormRoom to start the spawner
        public static event Action OnPlayerExitedVan;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                col.isTrigger = true;
            }
        }

        private void Update()
        {
            if (_currentPlayer == null) return;
            if (_currentPlayer.IsDead.Value) return;

            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                Debug.Log("[VanExit] F key pressed.");
                WindRiderController rider = _currentPlayer.GetComponent<WindRiderController>();
                if (rider == null || rider.IsWindRiding)
                {
                    Debug.LogWarning("[VanExit] Cannot exit: rider=" + (rider != null) + ", isWindRiding=" + (rider?.IsWindRiding ?? false));
                    return;
                }

                // teleport player to exit position to clear van geometry
                CharacterController cc = _currentPlayer.GetComponent<CharacterController>();
                if (cc != null)
                    cc.enabled = false;

                _currentPlayer.transform.position = exitPosition.position;
                _currentPlayer.transform.rotation = exitPosition.rotation;

                if (cc != null)
                    cc.enabled = true;

                // Give the player an initial horizontal boost in the exit direction
                // and an upward boost. StartGliding will inherit this speed.
                Vector3 exitBoost = exitPosition.forward * 20f + Vector3.up * launchUpwardForce;
                _currentPlayer.SetExternalVelocity(exitBoost);

                rider.StartGliding();

                // notify systems that a player has left the van
                OnPlayerExitedVan?.Invoke();

                ClearCurrentPlayer();
            }
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
