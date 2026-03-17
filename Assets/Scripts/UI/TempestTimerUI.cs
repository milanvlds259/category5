using UnityEngine;
using UnityEngine.UI;

namespace Category5.UI
{
    // shows a countdown bar above the R icon while tempest engine is active
    // hides itself when the ult expires or the big move fires
    // mirrors the ChargeIndicatorUI pattern: self-contained, subscribes to static events
    // add this component directly on the Slider GameObject - no separate container needed
    [RequireComponent(typeof(Slider))]
    [RequireComponent(typeof(CanvasGroup))]
    public class TempestTimerUI : MonoBehaviour
    {
        [Header("ui references")]
        [Tooltip("fill image on the slider")]
        [SerializeField] private Image timerFillImage;

        [Header("color settings")]
        [Tooltip("color when the timer is full (ult just activated)")]
        [SerializeField] private Color fullColor = new Color(0.3f, 0.85f, 1f);    // cyan

        [Tooltip("color at the midpoint of the timer")]
        [SerializeField] private Color midColor = new Color(1f, 0.85f, 0.1f);     // yellow

        [Tooltip("color when time is almost out")]
        [SerializeField] private Color emptyColor = new Color(1f, 0.2f, 0.15f);   // red

        [Header("urgency pulse")]
        [Tooltip("seconds remaining at which the pulse starts")]
        [SerializeField] private float urgencyThreshold = 2f;

        [Tooltip("how fast the pulse oscillates")]
        [SerializeField] private float pulseFrequency = 4f;

        [Tooltip("scale added on top of base scale during pulse (0-1 range)")]
        [SerializeField] private float pulseIntensity = 0.12f;

        [Header("fade settings")]
        [SerializeField] private float fadeSpeed = 10f;

        // internal state
        private CanvasGroup _canvasGroup;
        private Slider _slider;
        private bool _isVisible;
        private float _targetAlpha;
        private Vector3 _baseScale;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _slider = GetComponent<Slider>();

            _baseScale = transform.localScale;

            // auto-find the fill image from the slider if not assigned
            if (timerFillImage == null && _slider != null && _slider.fillRect != null)
                timerFillImage = _slider.fillRect.GetComponent<Image>();

            if (_slider != null)
            {
                _slider.minValue = 0f;
                _slider.maxValue = 1f;
                _slider.value = 1f;
            }

            SetVisible(false, true);
        }

        private void OnEnable()
        {
            FighterR.OnTempestActivate   += HandleActivate;
            FighterR.OnTempestTimerTick  += HandleTimerTick;
            FighterR.OnTempestDeactivated += HandleDeactivate;
            FighterR.OnTempestBigMove    += HandleBigMove;
        }

        private void OnDisable()
        {
            FighterR.OnTempestActivate   -= HandleActivate;
            FighterR.OnTempestTimerTick  -= HandleTimerTick;
            FighterR.OnTempestDeactivated -= HandleDeactivate;
            FighterR.OnTempestBigMove    -= HandleBigMove;
        }

        private void Update()
        {
            if (_canvasGroup == null) return;

            // smooth fade
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, fadeSpeed * Time.deltaTime);

            // urgency pulse when visible and in low-time zone
            if (_isVisible)
            {
                float fill = _slider != null ? _slider.value : 0f;
                float remainingApprox = fill * (_ultDuration > 0f ? _ultDuration : 7f);

                if (remainingApprox <= urgencyThreshold && remainingApprox > 0f)
                {
                    float pulse = 1f + Mathf.Abs(Mathf.Sin(Time.time * pulseFrequency * Mathf.PI)) * pulseIntensity;
                    transform.localScale = _baseScale * pulse;
                }
                else
                {
                    transform.localScale = _baseScale;
                }
            }
        }

        // ---- event handlers ----

        private float _ultDuration;

        private void HandleActivate(Vector3 _)
        {
            _ultDuration = 0f; // will be set on first tick
            SetFill(1f);
            SetVisible(true);
        }

        private void HandleTimerTick(float remaining, float total)
        {
            _ultDuration = total;
            float fill = total > 0f ? remaining / total : 0f;
            SetFill(fill);
        }

        private void HandleDeactivate(Vector3 _, bool __)
        {
            SetVisible(false);
        }

        private void HandleBigMove(Vector3 _, Vector3 __)
        {
            // second press fires the big move - ult is over, hide the bar
            SetVisible(false);
        }

        // ---- helpers ----

        private void SetFill(float fill)
        {
            if (_slider != null)
                _slider.value = Mathf.Clamp01(fill);

            if (timerFillImage != null)
            {
                // full (1) -> mid (0.5) -> empty (0)
                if (fill > 0.5f)
                    timerFillImage.color = Color.Lerp(midColor, fullColor, (fill - 0.5f) * 2f);
                else
                    timerFillImage.color = Color.Lerp(emptyColor, midColor, fill * 2f);
            }
        }

        private void SetVisible(bool visible, bool instant = false)
        {
            _isVisible = visible;
            _targetAlpha = visible ? 1f : 0f;

            if (instant && _canvasGroup != null)
                _canvasGroup.alpha = _targetAlpha;

            if (!visible)
                transform.localScale = _baseScale;
        }
    }
}
