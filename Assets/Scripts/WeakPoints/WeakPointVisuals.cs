using UnityEngine;

namespace Category5.WeakPoints
{
    // handles visual state changes for a weak point based on health and active state
    // color shifts: intact (intactColor) → damaged (damagedColor) → broken (hidden)
    // respawning: fade in / re-enable renderer
    [RequireComponent(typeof(WeakPoint))]
    public class WeakPointVisuals : MonoBehaviour
    {
        [Header("renderer")]
        [Tooltip("the renderer to tint (auto-finds child renderer if empty)")]
        [SerializeField] private Renderer targetRenderer;

        [Header("pulse")]
        [Tooltip("pulse the intact color when the weak point spawns/resets")]
        [SerializeField] private bool enablePulseOnSpawn = true;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseIntensity = 0.3f;

        private WeakPoint _weakPoint;
        private MaterialPropertyBlock _propBlock;
        private static readonly int _colorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _emissionId = Shader.PropertyToID("_EmissionColor");

        private float _pulseTimer;
        private bool _isPulsing;

        private void Awake()
        {
            _weakPoint = GetComponent<WeakPoint>();
            _propBlock = new MaterialPropertyBlock();

            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();
        }

        private void OnEnable()
        {
            if (_weakPoint != null)
            {
                _weakPoint.CurrentHealth.OnValueChanged += OnHealthChanged;
                _weakPoint.IsActive.OnValueChanged += OnActiveChanged;
            }
        }

        private void OnDisable()
        {
            if (_weakPoint != null)
            {
                _weakPoint.CurrentHealth.OnValueChanged -= OnHealthChanged;
                _weakPoint.IsActive.OnValueChanged -= OnActiveChanged;
            }
        }

        private void Update()
        {
            if (_isPulsing && enablePulseOnSpawn)
            {
                _pulseTimer += Time.deltaTime * pulseSpeed;

                if (_pulseTimer > Mathf.PI * 2f)
                    _isPulsing = false;
            }
        }

        private void OnHealthChanged(int oldValue, int newValue)
        {
            UpdateColor();
        }

        private void OnActiveChanged(bool oldValue, bool newValue)
        {
            UpdateColor();

            if (newValue && enablePulseOnSpawn)
            {
                // start pulse on activation
                _pulseTimer = 0f;
                _isPulsing = true;
            }
        }

        private void UpdateColor()
        {
            if (targetRenderer == null || _weakPoint == null) return;

            Color baseColor;
            if (!_weakPoint.IsActive.Value)
            {
                baseColor = _weakPoint.BrokenColor;
            }
            else
            {
                // lerp between intact and damaged based on health percentage
                float healthPct = _weakPoint.HealthPercent;
                if (healthPct <= _weakPoint.DamageColorThreshold)
                {
                    baseColor = Color.Lerp(_weakPoint.BrokenColor, _weakPoint.DamagedColor,
                        healthPct / Mathf.Max(_weakPoint.DamageColorThreshold, 0.01f));
                }
                else
                {
                    baseColor = Color.Lerp(_weakPoint.DamagedColor, _weakPoint.IntactColor,
                        (healthPct - _weakPoint.DamageColorThreshold)
                        / Mathf.Max(1f - _weakPoint.DamageColorThreshold, 0.01f));
                }

                // add pulse overlay
                if (_isPulsing)
                {
                    float pulse = Mathf.Sin(_pulseTimer) * pulseIntensity;
                    baseColor += new Color(pulse, pulse, pulse, 0f);
                }
            }

            // apply color via property block (no material allocation)
            targetRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(_colorId, baseColor);

            // set emission to glow with the base color
            _propBlock.SetColor(_emissionId, baseColor * 0.5f);

            targetRenderer.SetPropertyBlock(_propBlock);

            // show/hide the renderer based on active state
            targetRenderer.enabled = _weakPoint.IsActive.Value;
        }
    }
}
