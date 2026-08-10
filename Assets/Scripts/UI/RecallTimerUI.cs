using UnityEngine;
using TMPro;
using Category5.Core;
using Category5.Map;

namespace Category5.UI
{
    // shows countdown during the automatic recall timer
    // "Recalling in Xs..."
    // fires after all players have selected their items from the room clear drops
    public class RecallTimerUI : MonoBehaviour
    {
        [Header("references")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI timerText;

        private float _recallTimeRemaining;
        private bool _isRecalling;

        private void OnEnable()
        {
            RoomManager.OnRecallingStarted += HandleRecallingStarted;
            RoomManager.OnRoomEntered += HandleRoomEntered;
        }

        private void OnDisable()
        {
            RoomManager.OnRecallingStarted -= HandleRecallingStarted;
            RoomManager.OnRoomEntered -= HandleRoomEntered;
        }

        private void HandleRecallingStarted()
        {
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            _isRecalling = true;

            // get recall time from storm data
            var storm = GameFlowManager.Instance != null ? GameFlowManager.Instance.GetCurrentStorm() : null;
            _recallTimeRemaining = storm != null ? storm.recallTimer : 5f;
        }

        private void HandleRoomEntered(StormRoom room)
        {
            // hide when entering a new room
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            _isRecalling = false;
        }

        private void Update()
        {
            if (!_isRecalling) return;

            _recallTimeRemaining -= Time.deltaTime;
            if (_recallTimeRemaining < 0f) _recallTimeRemaining = 0f;

            if (timerText != null)
            {
                timerText.text = $"Recalling in {Mathf.CeilToInt(_recallTimeRemaining)}s";
            }

            // auto-hide when timer expires
            if (_recallTimeRemaining <= 0f)
            {
                if (canvasGroup != null) canvasGroup.alpha = 0f;
                _isRecalling = false;
            }
        }
    }
}
