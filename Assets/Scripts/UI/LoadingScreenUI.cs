using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Category5.UI
{
    // full screen loading overlay with progress bar and gameplay tips
    // lives as a child of SceneTransitionManager so it persists across scenes
    public class LoadingScreenUI : MonoBehaviour
    {
        [Header("ui references")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private Image progressBarFill;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("fade settings")]
        [SerializeField] private float fadeDuration = 0.25f;

        [Header("tips")]
        [SerializeField] private string[] tips = new string[]
        {
            "Dodge at the right time to avoid damage with i-frames!",
            "Work together with your team to take down bosses faster.",
            "Choose your items wisely between rounds.",
            "Each class has unique abilities on Q, E, and R.",
            "Stay close to your teammates for a better chance of survival.",
            "Bosses get stronger each round — be ready!",
            "Rangers can charge their shots for bonus damage.",
            "Watch for boss telegraph animations to dodge their attacks.",
            "Lifesteal items can keep you alive in tough fights.",
            "The Enchanter supports the team — protect them!"
        };

        private float _targetProgress;
        private float _currentProgress;
        private int _lastTipIndex = -1;
        private Coroutine _fadeCoroutine;
        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
                _canvas = GetComponent<Canvas>();

            if (loadingPanel != null)
                loadingPanel.SetActive(false);
        }

        // shows the loading screen with a status message
        public void Show(string status = "Loading...")
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(true);

            // make sure canvas renders on top of everything
            if (_canvas != null)
                _canvas.sortingOrder = 999;

            _targetProgress = 0f;
            _currentProgress = 0f;

            if (progressBarFill != null)
                progressBarFill.fillAmount = 0f;

            SetStatus(status);
            PickRandomTip();

            // fade in
            if (canvasGroup != null)
            {
                if (_fadeCoroutine != null)
                    StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = StartCoroutine(FadeCanvasGroup(0f, 1f));
            }
        }

        // hides the loading screen with a fade out
        public void Hide()
        {
            if (canvasGroup != null)
            {
                if (_fadeCoroutine != null)
                    StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = StartCoroutine(FadeAndDeactivate());
            }
            else
            {
                if (loadingPanel != null)
                    loadingPanel.SetActive(false);
            }
        }

        // set the progress bar target (0 to 1), lerps smoothly in Update
        public void UpdateProgress(float progress)
        {
            _targetProgress = Mathf.Clamp01(progress);
        }

        // update the status text
        public void SetStatus(string status)
        {
            if (statusText != null)
                statusText.text = status;
        }

        // whether the loading screen is currently visible
        public bool IsVisible => loadingPanel != null && loadingPanel.activeSelf;

        private void Update()
        {
            if (!IsVisible) return;

            // smoothly lerp progress bar toward target
            if (progressBarFill != null && !Mathf.Approximately(_currentProgress, _targetProgress))
            {
                _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, Time.unscaledDeltaTime * 2f);
                progressBarFill.fillAmount = _currentProgress;
            }
        }

        private void PickRandomTip()
        {
            if (tipText == null || tips == null || tips.Length == 0) return;

            int index;
            if (tips.Length == 1)
            {
                index = 0;
            }
            else
            {
                // avoid showing the same tip twice in a row
                do
                {
                    index = Random.Range(0, tips.Length);
                } while (index == _lastTipIndex);
            }

            _lastTipIndex = index;
            tipText.text = tips[index];
        }

        private IEnumerator FadeCanvasGroup(float from, float to)
        {
            if (canvasGroup == null) yield break;

            canvasGroup.alpha = from;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
                yield return null;
            }

            canvasGroup.alpha = to;
            _fadeCoroutine = null;
        }

        private IEnumerator FadeAndDeactivate()
        {
            yield return FadeCanvasGroup(1f, 0f);

            if (loadingPanel != null)
                loadingPanel.SetActive(false);

            _fadeCoroutine = null;
        }
    }
}
