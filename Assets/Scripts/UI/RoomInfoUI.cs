using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Category5.Core;
using Category5.Map;

namespace Category5.UI
{
    // shows current room info: name, task type, eyewall indicator
    // appears when entering a new room, fades out after a delay
    public class RoomInfoUI : MonoBehaviour
    {
        [Header("ui references")]
        [SerializeField] private GameObject infoPanel;
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI taskTypeText;
        [SerializeField] private TextMeshProUGUI eyewallText;
        [SerializeField] private Image backgroundImage;

        [Header("display settings")]
        [Tooltip("how long the info panel stays visible after entering a room (seconds)")]
        [SerializeField] private float displayDuration = 4f;

        [Tooltip("fade out duration (seconds)")]
        [SerializeField] private float fadeDuration = 1f;

        [Header("colors")]
        [SerializeField] private Color outerRingColor = new Color(0.4f, 0.7f, 1f, 0.9f);
        [SerializeField] private Color middleRingColor = new Color(1f, 0.8f, 0.3f, 0.9f);
        [SerializeField] private Color innerRingColor = new Color(1f, 0.4f, 0.3f, 0.9f);
        [SerializeField] private Color eyeColor = new Color(1f, 0.2f, 0.2f, 0.9f);

        // runtime state
        private CanvasGroup _canvasGroup;
        private float _displayTimer;
        private bool _isShowing;
        private StormRoom _currentRoom;

        private void Awake()
        {
            _canvasGroup = infoPanel != null ? infoPanel.GetComponent<CanvasGroup>() : null;
            if (_canvasGroup == null && infoPanel != null)
            {
                _canvasGroup = infoPanel.AddComponent<CanvasGroup>();
            }

            if (infoPanel != null)
            {
                infoPanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            RoomTransitionManager.OnRoomEntered += HandleRoomEntered;
        }

        private void OnDisable()
        {
            RoomTransitionManager.OnRoomEntered -= HandleRoomEntered;
        }

        private void Update()
        {
            if (!_isShowing) return;

            _displayTimer -= Time.deltaTime;

            if (_displayTimer <= 0f)
            {
                StartFadeOut();
            }
        }

        // =====================================
        // room entry
        // =====================================

        private void HandleRoomEntered(StormRoom room)
        {
            if (room == null) return;

            _currentRoom = room;
            ShowRoomInfo(room);
        }

        private void ShowRoomInfo(StormRoom room)
        {
            if (infoPanel == null) return;

            // populate text fields
            if (roomNameText != null)
            {
                roomNameText.text = $"Room {room.RoomIndex}";
            }

            if (taskTypeText != null)
            {
                taskTypeText.text = GetTaskDescription(room.TaskType);
            }

            if (eyewallText != null)
            {
                eyewallText.text = GetEyewallLabel(room.EyewallIndex);
            }

            // set background color based on eyewall
            if (backgroundImage != null)
            {
                backgroundImage.color = GetEyewallColor(room.EyewallIndex);
            }

            // show panel
            infoPanel.SetActive(true);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            _displayTimer = displayDuration;
            _isShowing = true;
        }

        private void StartFadeOut()
        {
            if (_canvasGroup == null)
            {
                infoPanel.SetActive(false);
                _isShowing = false;
                return;
            }

            StartCoroutine(FadeOutCoroutine());
        }

        private System.Collections.IEnumerator FadeOutCoroutine()
        {
            float elapsed = 0f;
            float startAlpha = _canvasGroup.alpha;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            infoPanel.SetActive(false);
            _isShowing = false;
        }

        // =====================================
        // helpers
        // =====================================

        private string GetTaskDescription(RoomTaskType taskType)
        {
            switch (taskType)
            {
                case RoomTaskType.EnemyWave: return "Clear all enemies";
                case RoomTaskType.EliteEncounter: return "Defeat the elite";
                case RoomTaskType.DefendPoint: return "Defend the point";
                case RoomTaskType.CollectItems: return "Collect items";
                case RoomTaskType.EventRoom: return "Special event";
                default: return "Unknown";
            }
        }

        private string GetEyewallLabel(int eyewallIndex)
        {
            if (eyewallIndex == -1) return "EYE — Boss Room";
            if (eyewallIndex == 0) return "Outer Ring";
            if (eyewallIndex == 1) return "Middle Ring";
            return $"Inner Ring {eyewallIndex}";
        }

        private Color GetEyewallColor(int eyewallIndex)
        {
            if (eyewallIndex == -1) return eyeColor;
            if (eyewallIndex == 0) return outerRingColor;
            if (eyewallIndex == 1) return middleRingColor;
            return innerRingColor;
        }
    }
}
