using UnityEngine;

namespace Category5.Boss
{
    // handles all visual feedback for the boss: animator state, hit flash, and spawn/death hooks
    // runs on all clients - subscribes to the synced currentState network variable via bossbase
    // mirrors the enemyvisuals pattern for consistency
    [RequireComponent(typeof(BossBase))]
    public class BossVisuals : MonoBehaviour
    {
        [Header("references")]
        [Tooltip("skinned mesh renderer on the boss model - used for hit flash")]
        [SerializeField] private Renderer meshRenderer;

        [Header("locomotion")]
        [Tooltip("walk speed used to normalize the speed parameter (0-1 blend). match to the boss move speed")]
        [SerializeField] private float walkSpeed = 5f;

        [Header("hit flash")]
        [SerializeField] private Color hitFlashColor = Color.red;
        [Range(0.02f, 0.5f)]
        [SerializeField] private float hitFlashDuration = 0.1f;

        // cached property block reused every flash - no material allocation ever
        private MaterialPropertyBlock _propBlock;
        private static readonly int _colorId = Shader.PropertyToID("_BaseColor");

        // animator parameter hashes - computed once at startup
        private static readonly int _stateHash       = Animator.StringToHash("State");
        private static readonly int _speedHash       = Animator.StringToHash("Speed");
        private static readonly int _attackIndexHash = Animator.StringToHash("AttackIndex");
        private static readonly int _hurtHash        = Animator.StringToHash("Hurt");
        private static readonly int _isDeadHash      = Animator.StringToHash("IsDead");
        private static readonly int _spawnHash       = Animator.StringToHash("SpawnTrigger");

        private BossBase _boss;
        private Animator _animator;

        private Color _originalColor;
        private bool _isFlashing;
        private float _flashTimer;
        private Vector3 _lastPosition;
        private bool _isDead;

        // =====================================
        // lifecycle
        // =====================================

        private void Awake()
        {
            _boss     = GetComponent<BossBase>();
            _animator = GetComponentInChildren<Animator>();
            _propBlock = new MaterialPropertyBlock();

            if (meshRenderer != null)
                _originalColor = meshRenderer.sharedMaterial != null ? meshRenderer.sharedMaterial.color : Color.white;
        }

        // called by bossbase on all clients after network spawn
        public void Initialize(BossBase boss)
        {
            _boss = boss;
            _boss.CurrentBossState.OnValueChanged += OnStateChanged;

            // sync to current state in case we joined mid-game
            if (_animator != null)
                _animator.SetInteger(_stateHash, (int)_boss.CurrentBossState.Value);
        }

        private void OnDestroy()
        {
            if (_boss != null)
                _boss.CurrentBossState.OnValueChanged -= OnStateChanged;
        }

        private void Update()
        {
            if (_animator == null) return;

            // compute normalized move speed for the locomotion blend tree
            float speed = (transform.position - _lastPosition).magnitude / Time.deltaTime;
            _animator.SetFloat(_speedHash, Mathf.Clamp01(speed / Mathf.Max(walkSpeed, 0.1f)));
            _lastPosition = transform.position;

            // count down hit flash timer
            if (_isFlashing)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f)
                {
                    _isFlashing = false;
                    if (meshRenderer != null)
                    {
                        meshRenderer.GetPropertyBlock(_propBlock);
                        _propBlock.SetColor(_colorId, _originalColor);
                        meshRenderer.SetPropertyBlock(_propBlock);
                    }
                }
            }
        }

        // =====================================
        // animation state
        // =====================================

        private void OnStateChanged(BossState oldState, BossState newState)
        {
            if (_animator == null) return;
            _animator.SetInteger(_stateHash, (int)newState);
        }

        // sets which attack clip slot to use before transitioning to attack state
        // lets a single Attack animator state branch into different clips per attack index
        public void SetAttackIndex(int index)
        {
            if (_animator == null) return;
            _animator.SetInteger(_attackIndexHash, index);
        }

        // =====================================
        // hurt
        // =====================================

        // triggered on all clients by bossbase.notifybosshurtclientrpc
        public void TriggerHurtAnimation()
        {
            if (_animator == null || _isDead) return;
            _animator.SetTrigger(_hurtHash);
            TriggerHitFlash();
        }

        private void TriggerHitFlash()
        {
            if (meshRenderer == null) return;
            meshRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(_colorId, hitFlashColor);
            meshRenderer.SetPropertyBlock(_propBlock);
            _isFlashing = true;
            _flashTimer = hitFlashDuration;
        }

        // =====================================
        // death
        // =====================================

        // triggered on all clients when the boss dies - locks into death state
        public void TriggerDeathAnimation()
        {
            _isDead = true;
            if (_animator == null) return;
            _animator.SetBool(_isDeadHash, true);
        }

        // =====================================
        // spawn
        // =====================================

        // called when the boss respawns/resets for a new round
        public void PlaySpawnAnimation()
        {
            _isDead = false;
            if (_animator == null) return;
            _animator.SetBool(_isDeadHash, false);
            _animator.SetTrigger(_spawnHash);
        }
    }
}
