using UnityEngine;
using UnityEngine.UI;
using Category5.Player;

namespace Category5.UI
{
    /// <summary>
    /// ui component that displays a charge indicator near the crosshair
    /// shows charge progress when player is charging a ranged attack lol
    /// </summary>
    public class ChargeIndicatorUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("the slider that shows charge progress (set Min=0, Max=1)")]
        [SerializeField] private Slider chargeSlider;

        [Tooltip("optional image used to tint the slider fill (child of slider fill area)")]
        [SerializeField] private Image chargeFillImage;
        
        [Tooltip("the container/parent object to show/hide")]
        [SerializeField] private GameObject chargeContainer;
        
        [Header("Visual Settings")]
        [Tooltip("color at 0% charge")]
        [SerializeField] private Color emptyColor = Color.white;
        
        [Tooltip("color at 50% charge")]
        [SerializeField] private Color midColor = Color.yellow;
        
        [Tooltip("color at 100% charge")]
        [SerializeField] private Color fullColor = new Color(1f, 0.5f, 0f); // orange
        
        [Header("Animation Settings")]
        [Tooltip("how fast the indicator fades in/out")]
        [SerializeField] private float fadeSpeed = 8f;
        
        [Tooltip("pop effect when fully charged")]
        [SerializeField] private bool popWhenFull = true;
        
        [Tooltip("how fast the pop animation plays (higher = faster)")]
        [SerializeField] private float popSpeed = 8f;
        
        [Tooltip("pop intensity (0-1 range added to base scale)")]
        [SerializeField] private float popIntensity = 0.15f;
        
        // internal state
        private CanvasGroup _canvasGroup;
        private bool _isVisible;
        private float _targetAlpha;
        private float _currentChargePercent;
        private Vector3 _baseScale;
        
        // pop effect state
        private bool _hasPopped;
        private float _popProgress; // 0 to 1, where 1 means pop is complete
        
        private void Awake()
        {
            // get or add canvas group for fading
            _canvasGroup = chargeContainer?.GetComponent<CanvasGroup>();
            if (_canvasGroup == null && chargeContainer != null)
            {
                _canvasGroup = chargeContainer.AddComponent<CanvasGroup>();
            }
            
            // cache base scale for pulse effect
            if (chargeContainer != null)
            {
                _baseScale = chargeContainer.transform.localScale;
            }
            
            // start hidden
            SetVisible(false, true);

            // initialize slider if present
            if (chargeSlider != null)
            {
                chargeSlider.minValue = 0f;
                chargeSlider.maxValue = 1f;
                chargeSlider.value = 0f;
            }

            // if no explicit fill image assigned, try to auto-find it from the slider's FillRect
            if (chargeFillImage == null && chargeSlider != null && chargeSlider.fillRect != null)
            {
                chargeFillImage = chargeSlider.fillRect.GetComponent<Image>();
            }
        }
        
        private void OnEnable()
        {
            // subscribe to charge events
            PlayerCombat.OnChargeStarted += OnChargeStarted;
            PlayerCombat.OnChargeProgress += OnChargeProgress;
            PlayerCombat.OnChargeReleased += OnChargeReleased;
            PlayerCombat.OnChargeCanceled += OnChargeCanceled;
        }
        
        private void OnDisable()
        {
            // unsubscribe from charge events
            PlayerCombat.OnChargeStarted -= OnChargeStarted;
            PlayerCombat.OnChargeProgress -= OnChargeProgress;
            PlayerCombat.OnChargeReleased -= OnChargeReleased;
            PlayerCombat.OnChargeCanceled -= OnChargeCanceled;
        }
        
        private void Update()
        {
            // smooth fade in/out
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, fadeSpeed * Time.deltaTime);
            }
            
            // pop effect when fully charged (one-time forward pop)
            if (popWhenFull && _isVisible && chargeContainer != null)
            {
                if (_currentChargePercent >= 0.99f && !_hasPopped)
                {
                    // trigger the pop
                    _hasPopped = true;
                    _popProgress = 0f;
                }
                
                if (_hasPopped && _popProgress < 1f)
                {
                    // animate the pop: scale up then back down
                    _popProgress += Time.deltaTime * popSpeed;
                    _popProgress = Mathf.Clamp01(_popProgress);
                    
                    // use a curve that goes up then down: sin(0 to pi) = 0 -> 1 -> 0 <- random ahh calculus nonsnense
                    float popCurve = Mathf.Sin(_popProgress * Mathf.PI);
                    float scale = 1f + popCurve * popIntensity;
                    chargeContainer.transform.localScale = _baseScale * scale;
                }
                else if (_hasPopped && _popProgress >= 1f)
                {
                    // pop finished, keep at base scale
                    chargeContainer.transform.localScale = _baseScale;
                }
            }
            else if (chargeContainer != null && chargeContainer.transform.localScale != _baseScale)
            {
                // reset scale when not visible or pop disabled
                chargeContainer.transform.localScale = _baseScale;
            }
        }
        
        private void OnChargeStarted(Vector3 position)
        {
            _currentChargePercent = 0f;
            _hasPopped = false;
            _popProgress = 0f;
            UpdateFillAmount(0f);
            SetVisible(true);
        }
        
        private void OnChargeProgress(float percent, Vector3 position)
        {
            _currentChargePercent = percent;
            UpdateFillAmount(percent);
        }
        
        private void OnChargeReleased(float percent, Vector3 position)
        {
            SetVisible(false);
        }
        
        private void OnChargeCanceled(Vector3 position)
        {
            SetVisible(false);
        }
        
        private void UpdateFillAmount(float percent)
        {
            // update slider value if available
            if (chargeSlider != null)
            {
                chargeSlider.value = percent;
            }

            // update fill image color if provided
            if (chargeFillImage != null)
            {
                if (percent < 0.5f)
                {
                    chargeFillImage.color = Color.Lerp(emptyColor, midColor, percent * 2f);
                }
                else
                {
                    chargeFillImage.color = Color.Lerp(midColor, fullColor, (percent - 0.5f) * 2f);
                }
            }
        }
        
        private void SetVisible(bool visible, bool instant = false)
        {
            _isVisible = visible;
            _targetAlpha = visible ? 1f : 0f;
            
            if (instant && _canvasGroup != null)
            {
                _canvasGroup.alpha = _targetAlpha;
            }
            
            // reset scale when hiding
            if (!visible && chargeContainer != null)
            {
                chargeContainer.transform.localScale = _baseScale;
            }
        }
    }
}
