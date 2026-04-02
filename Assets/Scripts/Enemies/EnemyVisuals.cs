using UnityEngine;
using Unity.Netcode;

namespace Category5.Enemies
{
    // handles all visual feedback for an enemy - animator, hit flash, vfx spawning
    // lives on the same root gameobject but is separate from EnemyBase so it stays focused
    // subscribes to EnemyBase network variable callbacks (runs on all clients)
    [RequireComponent(typeof(EnemyBase))]
    public class EnemyVisuals : MonoBehaviour
    {
        [Header("references")]
        [Tooltip("the skinned mesh renderer of the enemy model - used for hit flash")]
        [SerializeField] private Renderer meshRenderer;

        [Header("hit flash")]
        [Tooltip("color flashed on the model when the enemy takes damage")]
        [SerializeField] private Color hitFlashColor = Color.red;

        [Tooltip("duration of the hit flash in seconds")]
        [Range(0.02f, 0.5f)]
        [SerializeField] private float hitFlashDuration = 0.1f;

        // cached property block - reused every flash so we never allocate a new material instance
        private MaterialPropertyBlock _propBlock;
        private static readonly int _colorId = Shader.PropertyToID("_BaseColor");

        private EnemyBase _enemy;
        private Animator _animator;
        private static readonly int _stateHash = Animator.StringToHash("State");

        private Color _originalColor;
        private bool _isFlashing;
        private float _flashTimer;

        // =====================================
        // lifecycle
        // =====================================

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _animator = GetComponentInChildren<Animator>();
            _propBlock = new MaterialPropertyBlock();

            if (meshRenderer == null)
                meshRenderer = GetComponentInChildren<Renderer>();
        }

        private void Start()
        {
            // cache the original color from the shared material (no allocation)
            if (meshRenderer != null)
            {
                _originalColor = meshRenderer.sharedMaterial != null
                    ? meshRenderer.sharedMaterial.color
                    : Color.white;
            }

            // subscribe after start so NetworkVariables are ready
            if (_enemy != null)
            {
                _enemy.CurrentState.OnValueChanged += OnStateChanged;
                _enemy.CurrentHealth.OnValueChanged += OnHealthChanged;

                // set correct animation state immediately (handles late joiners)
                UpdateAnimatorState(_enemy.CurrentState.Value);
            }
        }

        private void OnDestroy()
        {
            if (_enemy != null)
            {
                _enemy.CurrentState.OnValueChanged -= OnStateChanged;
                _enemy.CurrentHealth.OnValueChanged -= OnHealthChanged;
            }
        }

        private void Update()
        {
            // tick down hit flash
            if (!_isFlashing) return;

            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
            {
                _isFlashing = false;
                SetRendererColor(_originalColor);
            }
        }

        // =====================================
        // initialization
        // =====================================

        // called by EnemyBase.OnNetworkSpawn after data is initialized so we can
        // apply the color tint and cache the post-tint color as the original
        public void Initialize(EnemyData data)
        {
            if (meshRenderer == null || data == null) return;

            if (data.enemyColor != Color.white)
            {
                SetRendererColor(data.enemyColor);
                _originalColor = data.enemyColor;
            }
            else
            {
                // read from shared material to avoid allocating a per-instance material
                _originalColor = meshRenderer.sharedMaterial != null
                    ? meshRenderer.sharedMaterial.color
                    : Color.white;
            }
        }

        // =====================================
        // network variable callbacks (all clients)
        // =====================================

        private void OnStateChanged(EnemyState oldState, EnemyState newState)
        {
            UpdateAnimatorState(newState);
        }

        private void OnHealthChanged(int oldHealth, int newHealth)
        {
            if (newHealth < oldHealth)
                PlayHitFlash();
        }

        // =====================================
        // animation
        // =====================================

        private void UpdateAnimatorState(EnemyState state)
        {
            if (_animator != null)
                _animator.SetInteger(_stateHash, (int)state);
        }

        // =====================================
        // visual feedback
        // =====================================

        private void PlayHitFlash()
        {
            if (meshRenderer == null) return;

            SetRendererColor(hitFlashColor);
            _isFlashing = true;
            _flashTimer = hitFlashDuration;
        }

        // spawn the death vfx from enemy data at the current position
        public void PlayDeathVfx(EnemyData data)
        {
            if (data == null || data.deathVfxPrefab == null) return;
            Instantiate(data.deathVfxPrefab, transform.position, Quaternion.identity);
        }

        // spawn the spawn vfx from enemy data at the current position
        public void PlaySpawnVfx(EnemyData data)
        {
            if (data == null || data.spawnVfxPrefab == null) return;
            Instantiate(data.spawnVfxPrefab, transform.position, Quaternion.identity);
        }

        // =====================================
        // helpers
        // =====================================

        private void SetRendererColor(Color color)
        {
            if (meshRenderer == null) return;

            meshRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(_colorId, color);
            meshRenderer.SetPropertyBlock(_propBlock);
        }
    }
}
