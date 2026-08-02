using UnityEngine;
using TMPro;
using Category5.Core;
using Category5.Map;

namespace Category5.UI
{
    // shows countdown during the prep timer
    // "Arriving in X seconds..."
    public class PrepTimerUI : MonoBehaviour
    {
        [Header("references")]
        [SerializeField] private GameObject timerPanel;
        [SerializeField] private TextMeshProUGUI timerText;

        private float _prepTimeRemaining;
        private bool _isPrepping;

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
            if (timerPanel != null) timerPanel.SetActive(true);
            _isPrepping = true;

            // get prep time from storm data
            var storm = GameFlowManager.Instance != null ? GameFlowManager.Instance.GetCurrentStorm() : null;
            _prepTimeRemaining = storm != null ? storm.prepTimer : 30f;
        }

        private void HandleRoomTransitioning()
        {
            if (timerPanel != null) timerPanel.SetActive(false);
            _isPrepping = false;
        }

        private void Update()
        {
            if (!_isPrepping) return;

            _prepTimeRemaining -= Time.deltaTime;
            if (_prepTimeRemaining < 0f) _prepTimeRemaining = 0f;

            if (timerText != null)
            {
                timerText.text = $"Arriving in {Mathf.CeilToInt(_prepTimeRemaining)}s";
            }
        }
    }
}
