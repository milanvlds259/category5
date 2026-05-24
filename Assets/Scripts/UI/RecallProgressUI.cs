using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Category5.Player.Van;

namespace Category5.UI
{
    // world space progress bar that shows recall channel progress above the player
    // should be a child of the player prefab
    public class RecallProgressUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image fillBar;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Settings")]
        [SerializeField] private float fadeSpeed = 4f;

        private RecallController _recallController;
        private Transform _cameraTransform;
        private bool _isSubscribed;
        private float _targetAlpha = 0f;

        private void Start()
        {
            _recallController = GetComponentInParent<RecallController>();
            if (_recallController == null)
            {
                enabled = false;
                return;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            Subscribe();
            SetVisible(false);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_isSubscribed || _recallController == null) return;
            _isSubscribed = true;

            _recallController.OnRecallStarted += HandleRecallStarted;
            _recallController.OnRecallProgress += HandleRecallProgress;
            _recallController.OnRecallCompleted += HandleRecallEnded;
            _recallController.OnRecallInterrupted += HandleRecallEnded;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _recallController == null) return;
            _isSubscribed = false;

            _recallController.OnRecallStarted -= HandleRecallStarted;
            _recallController.OnRecallProgress -= HandleRecallProgress;
            _recallController.OnRecallCompleted -= HandleRecallEnded;
            _recallController.OnRecallInterrupted -= HandleRecallEnded;
        }

        private void HandleRecallStarted()
        {
            SetVisible(true);
            _targetAlpha = 1f;
        }

        private void HandleRecallProgress(float progress)
        {
            if (fillBar != null)
                fillBar.fillAmount = progress;

            if (progressText != null)
                progressText.text = $"Recall {Mathf.RoundToInt(progress * 100f)}%";
        }

        private void HandleRecallEnded()
        {
            _targetAlpha = 0f;

            if (fillBar != null)
                fillBar.fillAmount = 0f;

            if (progressText != null)
                progressText.text = "";
        }

        private void LateUpdate()
        {
            if (_recallController == null) return;

            // billboard toward camera
            if (_cameraTransform == null)
            {
                if (Camera.main != null)
                    _cameraTransform = Camera.main.transform;
            }

            if (_cameraTransform != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - _cameraTransform.position);
            }

            // fade in/out
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _targetAlpha, fadeSpeed * Time.deltaTime);
            }
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = visible ? 1f : 0f;

            if (!visible)
            {
                if (fillBar != null)
                    fillBar.fillAmount = 0f;
                if (progressText != null)
                    progressText.text = "";
            }
        }
    }
}
