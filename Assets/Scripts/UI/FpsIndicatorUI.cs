using TMPro;
using UnityEngine;

namespace Category5.UI
{
    // lightweight local fps counter for the gameplay hud
    // counts frames every render but only refreshes the text on an interval
    public class FpsIndicatorUI : MonoBehaviour
    {
        [Header("ui references")]
        [Tooltip("text component displaying fps")]
        [SerializeField] private TextMeshProUGUI fpsText;

        [Header("settings")]
        [Tooltip("how often to refresh the displayed value")]
        [SerializeField] private float updateInterval = 0.5f;

        [Tooltip("hides the counter while the game is paused")]
        [SerializeField] private bool hideWhilePaused;

        [Header("color thresholds")]
        [SerializeField] private float goodFpsThreshold = 55f;
        [SerializeField] private float okayFpsThreshold = 30f;
        [SerializeField] private Color goodColor = Color.green;
        [SerializeField] private Color okayColor = Color.yellow;
        [SerializeField] private Color badColor = Color.red;

        private int _frameCount;
        private float _elapsedTime;
        private float _updateTimer;

        private void Awake()
        {
            if (fpsText == null)
            {
                Debug.LogError("FpsIndicatorUI is missing its text reference", this);
            }

            updateInterval = Mathf.Max(0.1f, updateInterval);
            ResetCounters();
        }

        private void OnEnable()
        {
            ResetCounters();
            UpdateVisibility();
        }

        private void OnValidate()
        {
            updateInterval = Mathf.Max(0.1f, updateInterval);
            goodFpsThreshold = Mathf.Max(1f, goodFpsThreshold);
            okayFpsThreshold = Mathf.Clamp(okayFpsThreshold, 1f, goodFpsThreshold);
        }

        private void LateUpdate()
        {
            _frameCount++;
            _elapsedTime += Time.unscaledDeltaTime;
            _updateTimer -= Time.unscaledDeltaTime;

            if (_updateTimer > 0f)
            {
                return;
            }

            _updateTimer = updateInterval;
            UpdateVisibility();

            if (fpsText == null || !fpsText.gameObject.activeInHierarchy)
            {
                ResetCounters();
                return;
            }

            float measuredTime = Mathf.Max(_elapsedTime, 0.0001f);
            float fps = _frameCount / measuredTime;
            int roundedFps = Mathf.RoundToInt(fps);

            fpsText.color = GetFpsColor(fps);
            fpsText.SetText("{0} FPS", roundedFps);

            ResetCounters();
        }

        private void ResetCounters()
        {
            _frameCount = 0;
            _elapsedTime = 0f;
            _updateTimer = updateInterval;
        }

        private void UpdateVisibility()
        {
            if (fpsText == null)
            {
                return;
            }

            bool shouldShow = !hideWhilePaused || !PauseMenu.GameIsPaused;
            if (fpsText.gameObject.activeSelf != shouldShow)
            {
                fpsText.gameObject.SetActive(shouldShow);
            }
        }

        private Color GetFpsColor(float fps)
        {
            if (fps >= goodFpsThreshold)
            {
                return goodColor;
            }

            if (fps >= okayFpsThreshold)
            {
                return okayColor;
            }

            return badColor;
        }
    }
}