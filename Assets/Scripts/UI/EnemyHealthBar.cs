using UnityEngine;
using UnityEngine.UI;
using Category5.Enemies;

namespace Category5.UI
{
    // world space health bar that floats above an enemy
    // this component should be added to the enemy prefab as a child object
    // it automatically faces the camera and fades based on distance
    public class EnemyHealthBar : MonoBehaviour
    {
        [Header("ui references")]
            [SerializeField] private UnityEngine.UI.Slider slider;
            [SerializeField] private UnityEngine.UI.Image fillImage;
            [SerializeField] private CanvasGroup canvasGroup;
        
        [Header("settings")]
        [SerializeField] private float minDistance = 2f;
        [SerializeField] private float fadeDistance = 20f;
        [SerializeField] private float maxDistance = 30f;
        [SerializeField] private bool hideWhenFull = false;
        [SerializeField] private float smoothSpeed = 5f;
        
        [Header("colors")]
        [SerializeField] private Color healthyColor = Color.green;
        [SerializeField] private Color damagedColor = Color.yellow;
        [SerializeField] private Color criticalColor = Color.red;
        [SerializeField] private float criticalThreshold = 0.25f;
        [SerializeField] private float damagedThreshold = 0.5f;
        
        private EnemyBase _enemy;
        private Transform _cameraTransform;
        private bool _isInitialized;
        private float _targetFill;
        private float _currentFill;
        
        // called by EnemyBase after OnNetworkSpawn
        public void Initialize(EnemyBase enemy)
        {
            _enemy = enemy;
            
            if (_enemy == null)
            {
                Debug.LogError("EnemyHealthBar: Cannot initialize with null enemy!");
                return;
            }
            
            // subscribe to health changes
            _enemy.CurrentHealth.OnValueChanged += OnHealthChanged;

            // initialize slider if present
            if (slider != null)
            {
                slider.maxValue = _enemy.MaxHealth;
                slider.value = _enemy.CurrentHealth.Value;
            }

            // set initial fill values for smooth lerp fallback
            _targetFill = (slider != null && slider.maxValue > 0f) ? (float)slider.value / slider.maxValue : 1f;
            _currentFill = _targetFill;
            UpdateFillImmediate();
            
            _isInitialized = true;
            
            // hide if full health and setting enabled
            if (hideWhenFull && canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }
        
        private void OnDestroy()
        {
            if (_enemy != null)
            {
                _enemy.CurrentHealth.OnValueChanged -= OnHealthChanged;
            }
        }
        
        private void OnHealthChanged(int oldHealth, int newHealth)
        {
            if (_enemy == null || _enemy.MaxHealth <= 0) return;

            float newFill = (float)newHealth / _enemy.MaxHealth;
            newFill = Mathf.Clamp01(newFill);

            // update slider immediately
            if (slider != null)
            {
                slider.value = newHealth;
            }

            // keep lerped visuals in sync
            _targetFill = newFill;

            // show health bar when damaged
            if (hideWhenFull && _targetFill < 1f && canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }
        
        private void LateUpdate()
        {
            if (!_isInitialized) return;
            
            // update camera reference if needed
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
            
            if (_cameraTransform == null) return;
            
            // billboard - always face the camera
            transform.rotation = Quaternion.LookRotation(transform.position - _cameraTransform.position);
            
            // smooth fill animation for color transition
            if (Mathf.Abs(_currentFill - _targetFill) > 0.001f)
            {
                _currentFill = Mathf.Lerp(_currentFill, _targetFill, smoothSpeed * Time.deltaTime);
                UpdateFillVisual();
            }
            
            // distance-based visibility
            if (canvasGroup != null)
            {
                float distance = Vector3.Distance(_cameraTransform.position, transform.position);
                
                // don't override alpha if hiding when full
                if (hideWhenFull && _targetFill >= 1f)
                {
                    return;
                }
                
                if (distance > maxDistance)
                {
                    canvasGroup.alpha = 0f;
                }
                else if (distance < minDistance)
                {
                    canvasGroup.alpha = 0f;
                }
                else if (distance > fadeDistance)
                {
                    canvasGroup.alpha = 1f - ((distance - fadeDistance) / (maxDistance - fadeDistance));
                }
                else
                {
                    canvasGroup.alpha = 1f;
                }
            }
        }
        
        private void UpdateFillImmediate()
        {
            _currentFill = _targetFill;
            UpdateFillVisual();
        }
        
        private void UpdateFillVisual()
        {
            if (fillImage != null)
            {
                // update color based on health percentage
                if (_currentFill <= criticalThreshold)
                {
                    fillImage.color = criticalColor;
                }
                else if (_currentFill <= damagedThreshold)
                {
                    fillImage.color = damagedColor;
                }
                else
                {
                    fillImage.color = healthyColor;
                }
            }
        }
        
        // force update health bar (called when enemy is reset/respawned)
        public void ForceUpdate()
        {
            if (_enemy == null) return;
            
            _targetFill = (float)_enemy.CurrentHealth.Value / _enemy.MaxHealth;
            _targetFill = Mathf.Clamp01(_targetFill);
            UpdateFillImmediate();
        }
    }
}
