using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Category5;
using Category5.Core;
using Category5.PowerUps;

namespace Category5.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private int baseMaxHealth = 100;
        public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(100);
        
        // reference to player stats for power-up modifiers
        private PlayerStats _playerStats;
        
        // effective max health including power-up bonuses
        public int MaxHealth => _playerStats != null ? _playerStats.TotalMaxHealth : baseMaxHealth;

        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float jumpHeight = 3f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float rotationSpeed = 15f;

        [Header("Ground Check")]
        [SerializeField] private float groundCheckRadius = 0.2f;
        [SerializeField] private Vector3 groundCheckOffset = new Vector3(0, 0.1f, 0);
        [SerializeField] private LayerMask groundLayers = 1; // Default layer

        [Header("Dodge Settings")]
        [SerializeField] private float dodgeDuration = 0.5f;
        [SerializeField] private float dodgeDistance = 8f;
        [SerializeField] private float dodgeCooldown = 2f;

        private CharacterController _controller;
        private InputSystem_Actions _inputActions;
        private Vector2 _moveInput;
        private Vector3 _velocity;
        private bool _isGrounded;
        private bool _isOffline = false;
        
        [Header("Debug")]
        [SerializeField] private bool invertMovement = false;

        // Jump Buffering
        private float _jumpBufferTime = 0.2f;
        private float _jumpBufferCounter;

        // Dodge State
        private bool _isDodging;
        private float _dodgeTimer;
        private float _lastDodgeTime = -10f;
        private Vector3 _dodgeDirection;
        private Transform _cameraTransform;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _inputActions = new InputSystem_Actions();
            _playerStats = GetComponent<PlayerStats>();
        }

        private void Start()
        {
            // if NetworkManager is missing or not running we are in offline mode
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                _isOffline = true;
                Debug.Log("PlayerController: Offline mode detected. Enabling local control.");
                
                var camera = FindFirstObjectByType<Category5.ThirdPersonCamera>();
                if (camera != null)
                {
                    camera.SetTarget(transform);
                    _cameraTransform = camera.transform;
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            _isOffline = false; // we are definitely networked now
            
            // cache stats reference
            if (_playerStats == null)
            {
                _playerStats = GetComponent<PlayerStats>();
            }

            if (IsServer)
            {
                CurrentHealth.Value = MaxHealth;
            }
            CurrentHealth.OnValueChanged += OnHealthChanged;
            
            // subscribe to stats changes to update max health
            if (_playerStats != null)
            {
                _playerStats.OnStatsChanged += OnStatsChanged;
            }

            // Register with UI
            if (IsOwner && Category5.UI.UIManager.Instance != null)
            {
                Category5.UI.UIManager.Instance.RegisterPlayer(this);
            }

            // spawn position is handled by NetworkManagerBootstrap before spawning

            if (!IsOwner)
            {
                enabled = false;
                return;
            }
            
            // lock and hide cursor for gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Assign camera target
            var camera = FindFirstObjectByType<Category5.ThirdPersonCamera>();
            if (camera != null)
            {
                camera.SetTarget(transform);
                _cameraTransform = camera.transform;
            }
        }

        public override void OnNetworkDespawn()
        {
            CurrentHealth.OnValueChanged -= OnHealthChanged;
            
            if (_playerStats != null)
            {
                _playerStats.OnStatsChanged -= OnStatsChanged;
            }
        }
        
        // called when power-ups change player stats
        private void OnStatsChanged()
        {
            if (IsServer)
            {
                // if max health increased, also increase current health
                int newMax = MaxHealth;
                if (CurrentHealth.Value < newMax)
                {
                    // heal the difference when getting max hp bonus
                    int oldMax = baseMaxHealth + (_playerStats != null ? _playerStats.MaxHealthBonus - 30 : 0); // rough estimate
                    int hpGain = newMax - oldMax;
                    if (hpGain > 0)
                    {
                        CurrentHealth.Value = Mathf.Min(CurrentHealth.Value + hpGain, newMax);
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (_inputActions != null)
            {
                _inputActions.Player.Enable();
                _inputActions.Player.Jump.performed += OnJump;
                _inputActions.Player.Dodge.performed += OnDodge;
            }
        }

        private void OnDisable()
        {
            if (_inputActions != null)
            {
                _inputActions.Player.Jump.performed -= OnJump;
                _inputActions.Player.Dodge.performed -= OnDodge;
                _inputActions.Player.Disable();
            }
        }

        private void Update()
        {
            if (!IsOwner && !_isOffline) return;
            
            // check if input should be blocked (pause menu or power-up selection)
            bool inputBlocked = Category5.UI.PauseMenu.GameIsPaused || IsInPowerUpSelection();

            // ensure we have a camera reference
            if (_cameraTransform == null)
            {
                // try to find the ThirdPersonCamera component
                var tpCamera = FindFirstObjectByType<Category5.ThirdPersonCamera>();
                if (tpCamera != null)
                {
                    _cameraTransform = tpCamera.transform;
                    tpCamera.SetTarget(transform);
                }
                // fallback to Main Camera
                else if (Camera.main != null)
                {
                    _cameraTransform = Camera.main.transform;
                }
            }

            // always process gravity so player doesn't float when paused
            // but skip movement input when blocked
            if (_isDodging)
            {
                // finish dodge even if input blocked
                HandleDodge();
            }
            else if (inputBlocked)
            {
                // only apply gravity no movement input
                HandleGravity();
            }
            else
            {
                HandleMovement();
                HandleGravity();
            }
        }
        
        // check if power-up selection is active
        private bool IsInPowerUpSelection()
        {
            return PowerUpManager.Instance != null && 
                   PowerUpManager.Instance.CurrentPhase.Value == GamePhase.PowerUpSelection;
        }

        private void HandleMovement()
        {
            _moveInput = _inputActions.Player.Move.ReadValue<Vector2>();
            
            Vector3 move = Vector3.zero;
            Vector3 lookDirection = transform.forward;

            // if we still don't have a camera we can't move relative to it
            if (_cameraTransform != null)
            {
                // get camera rotation flattened to XZ plane
                Vector3 cameraEuler = _cameraTransform.eulerAngles;
                Quaternion flatCameraRotation = Quaternion.Euler(0, cameraEuler.y, 0);
                
                // rotate input vector by camera rotation
                Vector3 input3D = new Vector3(_moveInput.x, 0, _moveInput.y);
                move = flatCameraRotation * input3D;
                
                // look direction is camera forward
                lookDirection = flatCameraRotation * Vector3.forward;
            }
            else
            {
                // fallback to world space
                move = new Vector3(_moveInput.x, 0, _moveInput.y);
                if (move != Vector3.zero) lookDirection = move;
            }
            
            if (invertMovement) move = -move;

            if (move.magnitude > 1f) move.Normalize();

            // always rotate to look direction (which is camera forward if camera exists)
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            if (move != Vector3.zero)
            {
                _controller.Move(move * moveSpeed * Time.deltaTime);
            }
        }

        private void HandleGravity()
        {
            // custom ground check is more reliable than CharacterController.isGrounded
            _isGrounded = Physics.CheckSphere(transform.position + groundCheckOffset, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);

            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f; // small downward force to keep grounded
            }

            // process jump buffer
            if (_jumpBufferCounter > 0)
            {
                _jumpBufferCounter -= Time.deltaTime;
                
                if (_isGrounded)
                {
                    // v = sqrt(h * -2 * g)
                    _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    _jumpBufferCounter = 0;
                }
            }

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            // don't accept input if blocked
            if (Category5.UI.PauseMenu.GameIsPaused || IsInPowerUpSelection()) return;
            
            // instead of jumping immediately we buffer the input
            _jumpBufferCounter = _jumpBufferTime;
        }

        private void OnDodge(InputAction.CallbackContext context)
        {
            // don't accept input if blocked
            if (Category5.UI.PauseMenu.GameIsPaused || IsInPowerUpSelection()) return;
            
            if (_isDodging || !_isGrounded) return;
            
            // use effective cooldown from player stats if available
            float effectiveCooldown = _playerStats != null ? _playerStats.EffectiveDodgeCooldown : dodgeCooldown;
            if (Time.time < _lastDodgeTime + effectiveCooldown) return;

            StartDodge();
        }

        private void StartDodge()
        {
            _isDodging = true;
            _dodgeTimer = dodgeDuration;
            _lastDodgeTime = Time.time;

            // determine dodge direction
            Vector2 input = _inputActions.Player.Move.ReadValue<Vector2>();
            Vector3 moveDir = new Vector3(input.x, 0, input.y);

            // use cached camera transform if available, otherwise try Camera.main
            Transform camTransform = _cameraTransform;
            if (camTransform == null && Camera.main != null) camTransform = Camera.main.transform;

            if (camTransform != null)
            {
                Vector3 cameraForward = camTransform.forward;
                Vector3 cameraRight = camTransform.right;
                cameraForward.y = 0;
                cameraRight.y = 0;
                cameraForward.Normalize();
                cameraRight.Normalize();
                
                moveDir = (cameraForward * input.y + cameraRight * input.x).normalized;
            }

            // if no input, dodge forward
            if (moveDir == Vector3.zero)
            {
                moveDir = transform.forward;
            }

            _dodgeDirection = moveDir;
            
            // rotate to face dodge direction immediately
            transform.rotation = Quaternion.LookRotation(_dodgeDirection);
        }

        private void HandleDodge()
        {
            _dodgeTimer -= Time.deltaTime;

            if (_dodgeTimer <= 0)
            {
                _isDodging = false;
                _velocity = Vector3.zero; // reset velocity after dodge
                return;
            }

            float speed = dodgeDistance / dodgeDuration;
            _controller.Move(_dodgeDirection * speed * Time.deltaTime);
        }

        public void TakeDamage(int damage)
        {
            if (!IsServer) return;
            
            // i-frame check
            if (_isDodging) 
            {
                Debug.Log("Player dodged damage!");
                return;
            }

            CurrentHealth.Value -= damage;
            Debug.Log($"Player took {damage} damage. Health: {CurrentHealth.Value}");
            
            if (CurrentHealth.Value <= 0)
            {
                Debug.Log("Player Died!");
                // TODO: Handle death (respawn or game over)
            }
        }
        
        // heals the player (server only) - used by lifesteal power-up
        public void Heal(int amount)
        {
            if (!IsServer) return;
            
            int newHealth = Mathf.Min(CurrentHealth.Value + amount, MaxHealth);
            if (newHealth > CurrentHealth.Value)
            {
                CurrentHealth.Value = newHealth;
                Debug.Log($"Player healed {amount} HP. Health: {CurrentHealth.Value}/{MaxHealth}");
            }
        }

        private void OnHealthChanged(int oldHealth, int newHealth)
        {
            if (newHealth < oldHealth)
            {
                // simple visual feedback for now
                // Debug.Log($"Ouch! Health: {newHealth}");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position + groundCheckOffset, groundCheckRadius);
        }
    }
}
