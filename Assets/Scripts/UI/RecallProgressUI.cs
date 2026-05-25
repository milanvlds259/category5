using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Category5.Player;
using Category5.Player.Van;

namespace Category5.UI
{
    public class RecallProgressUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Slider slider;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Settings")]
        [SerializeField] private float fadeSpeed = 6f;

        private RecallController _recallController;
        private bool _isSubscribed;
        private float _targetAlpha = 0f;

        private bool IsOffline()
        {
            return Unity.Netcode.NetworkManager.Singleton == null ||
                   !Unity.Netcode.NetworkManager.Singleton.IsListening;
        }

        private void Start()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
            }

            StartCoroutine(FindLocalPlayer());
        }

        private System.Collections.IEnumerator FindLocalPlayer()
        {
            yield return null;

            PlayerController localPlayer = FindFirstObjectByType<PlayerController>();
            while (localPlayer == null || !(localPlayer.IsOwner || IsOffline()))
            {
                localPlayer = FindFirstObjectByType<PlayerController>();
                yield return null;
            }

            _recallController = localPlayer.GetComponent<RecallController>();
            if (_recallController == null)
            {
                Debug.LogWarning("[RecallProgressUI] Local player has no RecallController");
                enabled = false;
                yield break;
            }

            Subscribe();
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
            _targetAlpha = 1f;

            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = true;
        }

        private void HandleRecallProgress(float progress)
        {
            if (slider != null)
                slider.value = progress;

            if (progressText != null)
                progressText.text = $"Recall {Mathf.RoundToInt(progress * 100f)}%";
        }

        private void HandleRecallEnded()
        {
            _targetAlpha = 0f;

            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = false;

            if (slider != null)
                slider.value = 0f;

            if (progressText != null)
                progressText.text = "";
        }

        private void Update()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _targetAlpha, fadeSpeed * Time.deltaTime);
            }
        }
    }
}
