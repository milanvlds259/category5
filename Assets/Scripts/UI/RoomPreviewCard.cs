using UnityEngine;
using TMPro;
using Category5.Core;
using Category5.Map;

namespace Category5.UI
{
    // static info card shown during the prep timer
    // displays the next room's name, difficulty, and enemy info
    public class RoomPreviewCard : MonoBehaviour
    {
        [Header("references")]
        [SerializeField] private GameObject cardPanel;
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI difficultyText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        private void OnEnable()
        {
            RoomManager.OnPrepStarted += HandlePrepStarted;
            RoomManager.OnRoomTransitioning += HandleRoomTransitioning;
        }

        private void OnDisable()
        {
            RoomManager.OnPrepStarted -= HandlePrepStarted;
            RoomManager.OnRoomTransitioning -= HandleRoomTransitioning;
        }

        private void HandlePrepStarted()
        {
            if (cardPanel != null) cardPanel.SetActive(true);
            // the next room index is set by RoomManager before prep starts
            // for now, show a generic message
            if (roomNameText != null) roomNameText.text = "Next Room";
            if (difficultyText != null) difficultyText.text = "Preparing...";
            if (descriptionText != null) descriptionText.text = "Get ready for the next room!";
        }

        private void HandleRoomTransitioning()
        {
            if (cardPanel != null) cardPanel.SetActive(false);
        }
    }
}
