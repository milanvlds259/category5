using UnityEngine;
using UnityEngine.InputSystem;
using Category5.Map;

namespace Category5.Player.Van
{
    // interactable map table in the center of the van
    // when the van is locked (room transition phase), pressing F opens the map UI
    // the host then selects the next room from the map
    public class MapTableController : MonoBehaviour
    {
        [Header("interaction")]
        [SerializeField] private float interactRange = 3f;
        [SerializeField] private GameObject exitPrompt;

        private PlayerController _currentPlayer;

        private void Update()
        {
            if (_currentPlayer == null) return;
            if (_currentPlayer.IsDead.Value) return;
            if (Category5.UI.PauseMenu.GameIsPaused) return;

            // only interactable when van is locked (after room cleared, between rooms)
            if (RoomManager.Instance == null || !RoomManager.Instance.IsVanLocked) return;

            // don't re-open if map is already showing
            if (Category5.UI.MapSelectionUI.IsOpen) return;

            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                OpenMap();
            }
        }

        private void OpenMap()
        {
            var mapUI = FindFirstObjectByType<Category5.UI.MapSelectionUI>();
            if (mapUI != null)
            {
                mapUI.Open();
            }
            else
            {
                Debug.LogWarning("[MapTable] no MapSelectionUI found in scene");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            if (!player.IsOwner && !IsOffline()) return;

            _currentPlayer = player;
            if (exitPrompt != null) exitPrompt.SetActive(true);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || player != _currentPlayer) return;

            _currentPlayer = null;
            if (exitPrompt != null) exitPrompt.SetActive(false);
        }

        private bool IsOffline()
        {
            return Unity.Netcode.NetworkManager.Singleton == null ||
                   !Unity.Netcode.NetworkManager.Singleton.IsListening;
        }
    }
}
