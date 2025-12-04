using UnityEngine;
using UnityEngine.UI;
using Category5.Player;

namespace Category5.UI
{
    /// <summary>
    /// ui component that displays a charge indicator near the crosshair
    /// shows charge progress when player is charging a ranged attack
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
        
        [Tooltip("pulse effect when fully charged")]
        [SerializeField] private bool pulseWhenFull = true;
        
        [Tooltip("pulse speed multiplier")]
        [SerializeField] private float pulseSpeed = 4f;
        
        [Tooltip("pulse intensity (0-1 range added to base scale)")]
        [SerializeField] private float pulseIntensity = 0.1f;
        
        // internal state
        private CanvasGroup _canvasGroup;
        private bool _isVisible;
        private float _targetAlpha;
        private float _currentChargePercent;
        private Vector3 _baseScale;
        
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
            
            // pulse effect when fully charged
            if (pulseWhenFull && _isVisible && _currentChargePercent >= 1f && chargeContainer != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
                chargeContainer.transform.localScale = _baseScale * pulse;
            }
            else if (chargeContainer != null && chargeContainer.transform.localScale != _baseScale)
            {
                // reset scale when not pulsing
                chargeContainer.transform.localScale = _baseScale;
            }
        }
        
        private void OnChargeStarted(Vector3 position)
        {
            _currentChargePercent = 0f;
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
