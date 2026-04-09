using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine.InputSystem;
using System;
using Category5;
using Category5.Core;
using Category5.Audio;
using Category5.UI;
using Category5.Player.WindRiding;
using Unity.InferenceEngine;

namespace Category5.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour, IDamageable
    {
        [Header("Player Identity")]
        // player name synced across network
        public NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>(
            "Player",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        // event fired when any player's name changes (for UI updates)
        public static event System.Action<PlayerController> OnPlayerNameChanged;
        
        // event fired when this player's max health changes (for UI updates)
        public event System.Action<int> OnMaxHealthChanged;
        
        [Header("Health")]
        [SerializeField] private int baseMaxHealth = 100; // fallback before class data loads
        public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(100);
        
        [Header("Mana")]
        [SerializeField] private int baseMaxMana = 10; // fallback before class data loads
        public NetworkVariable<int> CurrentMana = new NetworkVariable<int>(10);
        
        // event fired when mana changes (for UI updates)
        public event System.Action<int, int> OnManaChanged; // current, max
        
        // death state synced across network
        public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(false);
        
        // reference to player stats for stat modifiers
        private PlayerStats _playerStats;
        
        // effective max health including item/power-up bonuses
        public int MaxHealth => _playerStats != null ? _playerStats.TotalMaxHealth : baseMaxHealth;
        
        // effective max mana including item/power-up bonuses
        public int MaxMana => _playerStats != null ? _playerStats.TotalMaxMana : baseMaxMana;
        
        [Header("Death Settings")]
        [SerializeField] private GameObject[] visualsToHideOnDeath; // optional: specific objects to hide
        private Renderer[] _renderers; // cached renderers for visibility toggle

        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float sprintSpeedMultiplier = 1.5f;
        [SerializeField] private float jumpHeight = 3f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float rotationSpeed = 15f;

        [Header("External Momentum")]
        [SerializeField] private float groundedMomentumDecay = 18f;
        [SerializeField] private float airborneMomentumDecay = 4f;
        [SerializeField] private float movementMomentumCancelRate = 24f;

        [Header("Ground Check")]
        [SerializeField] private float groundCheckRadius = 0.2f;
        [SerializeField] private Vector3 groundCheckOffset = new Vector3(0, 0.1f, 0);
        [SerializeField] private LayerMask groundLayers = 1; // Default layer

        [Header("Dodge Settings")]
        [SerializeField] private float dodgeDuration = 0.5f;
        [SerializeField] private float dodgeDistance = 8f;
        [SerializeField] private float dodgeCooldown = 2f;
        
        private float _manaRegenAccumulator = 0f;

        private CharacterController _controller;
        private InputSystem_Actions _inputActions;
        private Vector2 _moveInput;
        private Vector3 _velocity;
        private Vector3 _externalVelocity;
        private bool _isGrounded;
        private bool _isClouded; // Surfing on clouds (wait I'm clouded)
        private LayerMask cloudLayer = 1 << 8; // CloudSurface layer
        private bool _isGliding = false;
        private bool _isOffline = false;
        
        // cached reference to player combat for charge state
        private PlayerCombat _playerCombat;
        
        // cached reference to model manager for animation
        private PlayerModelManager _playerModelManager;
        
        // animation parameter hashes (matched to animator controller parameters)
        private static readonly int _animSpeedHash = Animator.StringToHash("Speed");
        private static readonly int _animIsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int _animIsDodgingHash = Animator.StringToHash("IsDodging");
        private static readonly int _animIsDeadHash = Animator.StringToHash("IsDead");
        private static readonly int _animIsSprintingHash = Animator.StringToHash("IsSprinting");
        private static readonly int _animVerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
        private static readonly int _animMoveXHash = Animator.StringToHash("MoveX");
        private static readonly int _animMoveYHash = Animator.StringToHash("MoveY");
        private static readonly int _animSpeedXHash = Animator.StringToHash("SpeedX");
        private static readonly int _animSpeedYHash = Animator.StringToHash("SpeedY");
        private static readonly int _animIsWindRidingHash = Animator.StringToHash("IsWindRiding");

        // animator parameter cache to avoid per frame warnings when a parameter is missing
        private RuntimeAnimatorController _cachedAnimatorController;
        private bool _animParamsCached;
        private bool _hasAnimSpeed;
        private bool _hasAnimIsGrounded;
        private bool _hasAnimIsDodging;
        private bool _hasAnimIsDead;
        private bool _hasAnimIsSprinting;
        private bool _hasAnimVerticalVelocity;
        private bool _hasAnimMoveX;
        private bool _hasAnimMoveY;
        private bool _hasAnimSpeedX;
        private bool _hasAnimSpeedY;
        private bool _hasAnimIsWindRiding;
        
        [Header("Debug")]
        [SerializeField] private bool invertMovement = false;

        // Jump Buffering
        private float _jumpBufferTime = 0.2f;
        private float _jumpBufferCounter;

        // Dodge State
        private bool _isDodging;
        private bool _isInvulnerable;  // set by items like Backup Plan
        public bool IsInvulnerable
        {
            get => _isInvulnerable;
            set => _isInvulnerable = value;
        }
        private float _dodgeTimer;
        private float _lastDodgeTime = -10f;
        private Vector3 _dodgeDirection;
        private Transform _cameraTransform;
        
        // Sprint State
        private bool _isSprinting;
        
        // sprint events for ui/vfx integration
        public static event Action<Vector3> OnSprintStarted;
        public static event Action<Vector3> OnSprintEnded;

        // item behaviour events

        // fired when player dodges damage via i-frames. passes remaining dodge timer so items can detect timing
        public event Action<float> OnPlayerDodgedAttack;

        // fired before Die() executes. subscribers can set preventDeath = true to cancel death
        public delegate void AboutToDieHandler(PlayerController player, ref bool preventDeath);
        public event AboutToDieHandler OnPlayerAboutToDie;

        // fired when player grounded state changes (true = just became airborne, false = just landed)
        public event Action<bool> OnPlayerAirborneStateChanged;
        
        // public property (ui can read this later)
        public bool IsSprinting => _isSprinting;
        public bool IsDodging => _isDodging;

        // fired on owner when CharacterController hits a non-ground surface while sprinting or dodging
        // passes (this player, hit gameobject) — used by ForcefulImpactBehaviour
        public static event System.Action<PlayerController, GameObject> OnBodyContact;
        
        // cached reference to wind rider controller
        private WindRiderController _windRider;
        
        // true when the player is surfing through a wind tunnel
        public bool IsWindRiding => _windRider != null && _windRider.IsWindRiding;
        
        // expose gravity value for external systems (wind riding lift calculations)
        public float Gravity => gravity;

        // whether the player is currently airborne (inverse of grounded, used by Weather Balloon)
        public bool IsAirborne => !_isGrounded;

        // multipliers applied by item behaviours (Weather Balloon)
        // jumpHeightMultiplier scales jumpHeight when computing jump velocity
        // fallSpeedMultiplier scales downward gravity when player is falling
        // airborneResistanceMultiplier reduces incoming damage while airborne (0 = none, 0.15 = 15% reduction)
        public float JumpHeightMultiplier { get; set; } = 1f;
        public float FallSpeedMultiplier { get; set; } = 1f;
        public float AirborneResistanceMultiplier { get; set; } = 0f;

        // whether the player is currently on the ground (used by fighter q dual-mode)
        public bool IsGrounded => _isGrounded;
        
        // horizontal movement speed in world units per second (used by tempest engine damage formula)
        public float CurrentMovementSpeed => _controller != null
            ? new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude
            : 0f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _inputActions = new InputSystem_Actions();
            _playerStats = GetComponent<PlayerStats>();
            _playerCombat = GetComponent<PlayerCombat>();
            _playerModelManager = GetComponent<PlayerModelManager>();
            _windRider = GetComponent<WindRiderController>();
            
            // cache all renderers for death visibility toggle
            _renderers = GetComponentsInChildren<Renderer>();
        }

        private void Start()
        {
            // if NetworkManager is missing or not running we are in offline mode
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                _isOffline = true;
                // Debug.Log("PlayerController: Offline mode detected. Enabling local control.");
                
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
                CurrentMana.Value = MaxMana;
            }
            _lastMaxHealth = MaxHealth;
            _lastMaxMana = MaxMana;
            CurrentHealth.OnValueChanged += OnHealthChanged;
            CurrentMana.OnValueChanged += OnManaValueChanged;
            IsDead.OnValueChanged += OnDeadStateChanged;
            PlayerName.OnValueChanged += OnPlayerNameChangedCallback;
            
            // subscribe to stat changes to update max health
            if (_playerStats != null)
            {
                _playerStats.OnStatsChanged += OnStatsChanged;
            }

            // Register with UI
            if (IsOwner && Category5.UI.UIManager.Instance != null)
            {
                Category5.UI.UIManager.Instance.RegisterPlayer(this);
            }
            
            // register with team health ui
            var teamHealthUI = FindFirstObjectByType<Category5.UI.TeamHealthUI>();
            if (teamHealthUI != null)
            {
                teamHealthUI.OnPlayerSpawned(this);
            }
            
            // if this is the local owner, request to set our name on the server
            if (IsOwner)
            {
                string localName = PlayerNameManager.Instance != null 
                    ? PlayerNameManager.Instance.GetDisplayName() 
                    : "Player";
                RequestSetNameServerRpc(localName);
            }
            
            // initialize name tag if present (name tag is a child of player prefab)
            var nameTag = GetComponentInChildren<PlayerNameTag>(true);
            if (nameTag != null)
            {
                nameTag.Initialize();
            }

            // initialize minimap trackable for radar display (players are blue)
            InitializeMinimapTrackable();

            // spawn position is handled by NetworkManagerBootstrap before spawning
            // server syncs spawn position to owning client since we dont use NetworkTransform
            if (IsServer)
            {
                SyncSpawnPositionClientRpc(transform.position, transform.rotation);
            }

            if (!IsOwner)
            {
                // manually kill input for non-owners so we can keep fixed update active for mana regen
                // this allows the server to keep mana regen running for all players without needing a separate manager
                if (_inputActions != null)
                {
                    _inputActions.Player.Disable();
                    _inputActions.Player.Jump.performed -= OnJump;
                    _inputActions.Player.Dodge.performed -= OnDodge;
                    _inputActions.Player.Sprint.performed -= OnSprint;
                }
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
            IsDead.OnValueChanged -= OnDeadStateChanged;
            PlayerName.OnValueChanged -= OnPlayerNameChangedCallback;
            
            if (_playerStats != null)
            {
                _playerStats.OnStatsChanged -= OnStatsChanged;
            }
        }
        
        // called when player name changes on network
        private void OnPlayerNameChangedCallback(FixedString64Bytes oldName, FixedString64Bytes newName)
        {
            // Debug.Log($"PlayerController: Name changed from '{oldName}' to '{newName}'");
            OnPlayerNameChanged?.Invoke(this);
        }
        
        // server rpc to set player name (called by owner after spawn)
        [Rpc(SendTo.Server)]
        private void RequestSetNameServerRpc(string name, RpcParams rpcParams = default)
        {
            // validate the name
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Player";
            }
            
            // limit length
            if (name.Length > 20)
            {
                name = name.Substring(0, 20);
            }
            
            PlayerName.Value = new FixedString64Bytes(name);
            // Debug.Log($"PlayerController: Server set name to '{name}' for client {OwnerClientId}");
        }
        
        // get the display name as a string (convenience method)
        public string GetPlayerName()
        {
            return PlayerName.Value.ToString();
        }
        
        // called when death state changes, syncs visual state on all clients
        private void OnDeadStateChanged(bool wasDead, bool isDead)
        {
            SetDeathVisuals(isDead);
            
            // set death animation parameter on all clients for responsive feedback
            var anim = _playerModelManager != null ? _playerModelManager.ModelAnimator : null;
            if (anim != null)
            {
                EnsureAnimatorParameterCache(anim);
                if (_hasAnimIsDead)
                {
                    anim.SetBool(_animIsDeadHash, isDead);
                }
            }

            // death transitions the animator out of the attack clip, so attack animation events
            // will never fire — reset combat state so _isAttacking doesn't stay stuck true
            if (isDead && _playerCombat != null)
            {
                _playerCombat.ResetCombatState();
            }
        }
        
        // enables/disables visuals and collider based on death state
        private void SetDeathVisuals(bool isDead)
        {
            
            // toggle specific objects if set
            if (visualsToHideOnDeath != null)
            {
                foreach (var obj in visualsToHideOnDeath)
                {
                    if (obj != null)
                    {
                        obj.SetActive(!isDead);
                    }
                }
            }
            
            // toggle name tag visibility (hide nametag if player dead)
            var nameTag = GetComponentInChildren<Category5.UI.PlayerNameTag>(true);
            if (nameTag != null)
            {
                nameTag.SetVisible(!isDead);
            }
            
            // disable collider when dead so boss attacks don't hit us
            if (_controller != null)
            {
                _controller.enabled = !isDead;
            }
        }
        
        // called when items change player stats
        private int _lastMaxHealth = 0;
        private int _lastMaxMana = 0;
        private void OnStatsChanged()
        {
            int newMax = MaxHealth;
            int previousMax = _lastMaxHealth > 0 ? _lastMaxHealth : newMax;
            int newMaxMana = MaxMana;
            int previousMaxMana = _lastMaxMana > 0 ? _lastMaxMana : newMaxMana;
            
            // check if max health changed (track on all clients for UI updates)
            if (_lastMaxHealth != newMax)
            {
                _lastMaxHealth = newMax;
                OnMaxHealthChanged?.Invoke(newMax);
            }

            if (_lastMaxMana != newMaxMana)
            {
                _lastMaxMana = newMaxMana;
                OnManaChanged?.Invoke(CurrentMana.Value, newMaxMana);
            }
            
            if (IsServer)
            {
                // preserve current missing-health ratio by applying only the max-health delta
                if (newMax > previousMax)
                {
                    CurrentHealth.Value = Mathf.Min(CurrentHealth.Value + (newMax - previousMax), newMax);
                }
                else if (CurrentHealth.Value > newMax)
                {
                    CurrentHealth.Value = newMax;
                }

                if (newMaxMana > previousMaxMana)
                {
                    CurrentMana.Value = Mathf.Min(CurrentMana.Value + (newMaxMana - previousMaxMana), newMaxMana);
                }
                else if (CurrentMana.Value > newMaxMana)
                {
                    CurrentMana.Value = newMaxMana;
                }
            }
        }

        // sets up minimap trackable component for radar visibility
        private void InitializeMinimapTrackable()
        {
            var trackable = GetComponent<MinimapTrackable>();
            if (trackable == null)
            {
                trackable = gameObject.AddComponent<MinimapTrackable>();
            }
            trackable.Configure(TrackableType.Player, new Color(0.2f, 0.6f, 1f), 1f);
        }

        private void OnEnable()
        {
            if (_inputActions != null)
            {
                _inputActions.Player.Enable();
                _inputActions.Player.Jump.performed += OnJump;
                _inputActions.Player.Dodge.started += OnDodge;
                _inputActions.Player.Sprint.performed += OnSprint;
            }
        }

        private void OnDisable()
        {
            if (_inputActions != null)
            {
                _inputActions.Player.Jump.performed -= OnJump;
                _inputActions.Player.Dodge.performed -= OnDodge;
                _inputActions.Player.Sprint.performed -= OnSprint;
                _inputActions.Player.Disable();
            }
        }

        private void Update()
        {
            if (!IsOwner && !_isOffline) return;
            
            // dead players cannot do anything
            if (IsDead.Value) return;
            
            // wind riding: WindRiderController drives all movement, skip everything else
            if (IsWindRiding)
            {
                UpdateAnimationParameters();
                return;
            }
            
            // check if input should be blocked (pause menu, power-up selection, or boss intro)
            bool inputBlocked = Category5.UI.PauseMenu.GameIsPaused || IsInPowerUpSelection() || Category5.UI.BossIntroUI.IntroIsPlaying;

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

            // autocancel sprint if blocking state active
            if (_isSprinting && (IsDead.Value || inputBlocked))
            {
                CancelSprint();
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
            
            // update animator parameters after all state changes
            UpdateAnimationParameters();
        }
        
        // check if power-up selection (now item selection) is active
        private bool IsInPowerUpSelection()
        {
            // check GameFlowManager for current phase
            if (Category5.Core.GameFlowManager.Instance != null)
            {
                return Category5.Core.GameFlowManager.Instance.CurrentPhase.Value == Category5.Core.GamePhase.PowerUpSelection;
            }
            return false;
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
                _externalVelocity = Vector3.MoveTowards(
                    _externalVelocity,
                    Vector3.zero,
                    movementMomentumCancelRate * Time.deltaTime
                );

                // apply charge movement speed reduction if charging
                float effectiveSpeed = _playerStats != null ? _playerStats.EffectiveMoveSpeed : moveSpeed;
                
                // apply speed multiplier from inventory (items and abilities)
                if (_playerStats != null)
                {
                    effectiveSpeed *= _playerStats.GetEffectiveSpeedMultiplier();
                }
                
                // apply sprint multiplier
                if (_isSprinting)
                {
                    effectiveSpeed *= sprintSpeedMultiplier;
                }
                
                if (_playerCombat != null && _playerCombat.IsCharging)
                {
                    effectiveSpeed *= _playerCombat.ChargeMovementMultiplier;
                }
                
                _controller.Move(move * effectiveSpeed * Time.deltaTime);
            }
        }

        private void HandleGravity()
        {
            if (Physics.CheckSphere(transform.position + groundCheckOffset, groundCheckRadius, cloudLayer, QueryTriggerInteraction.Collide)
                && _isGliding)
            {   
                _isClouded = true;
            }
            else
            {
                _isClouded = false;
            }

            // custom ground check is more reliable than CharacterController.isGrounded
            bool wasGrounded = _isGrounded;
            
            if (Physics.CheckSphere(transform.position + groundCheckOffset, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore))
            {
                _isGrounded = true;
                _isGliding = false;
            }
            else
            {
                _isGrounded = false;
            }
            

            // fire airborne state change event for item behaviours
            if (wasGrounded && !_isGrounded)
            {
                OnPlayerAirborneStateChanged?.Invoke(true); // became airborne
            }
            else if (!wasGrounded && _isGrounded)
            {
                OnPlayerAirborneStateChanged?.Invoke(false); // landed
            }

            if ( (_isGrounded) && _velocity.y < 0)
            {
                _velocity.y = -2f; // small downward force to keep grounded
            }
            if (_isClouded && _velocity.y < 0)
            {
                _velocity.y = 1f;
                // Want some bounce/give later
                // _velocity.y = Mathf.Clamp(_velocity.y, -1f, 1f);
                // _velocity.y += -_velocity.y * 70f * Time.deltaTime;
            }

            // process jump buffer
            if (_jumpBufferCounter > 0)
            {
                _jumpBufferCounter -= Time.deltaTime;
                
                if (_isGrounded || _isClouded)
                {
                    // v = sqrt(h * -2 * g)
                    _velocity.y = Mathf.Sqrt(jumpHeight * JumpHeightMultiplier * -2f * gravity);
                    
                    _jumpBufferCounter = 0;
                }
            }

            // apply gravity — scale downward pull by FallSpeedMultiplier when falling
            float gravityThisFrame = gravity;
            if (_velocity.y < 0f)
            {
                gravityThisFrame *= FallSpeedMultiplier;
            }
            _velocity.y += gravityThisFrame * Time.deltaTime;

            Vector3 frameVelocity = _externalVelocity + Vector3.up * _velocity.y;
            _controller.Move(frameVelocity * Time.deltaTime);

            float momentumDecay = _isGrounded ? groundedMomentumDecay : airborneMomentumDecay;
            _externalVelocity = Vector3.MoveTowards(_externalVelocity, Vector3.zero, momentumDecay * Time.deltaTime);
        }
        
        private void FixedUpdate()
        {
            // server-only mana regeneration
            if (!IsServer) return;
            if (IsDead.Value) return;
            if (_playerStats == null) return;
            
            float manaPerSecond = Mathf.Max(0f, _playerStats.EffectiveManaRegenRate);
            if (manaPerSecond <= 0f)
            {
                _manaRegenAccumulator = 0f;
                return;
            }

            if (CurrentMana.Value >= MaxMana)
            {
                _manaRegenAccumulator = 0f;
                return;
            }

            _manaRegenAccumulator += manaPerSecond * Time.fixedDeltaTime;
            int manaToRestore = Mathf.FloorToInt(_manaRegenAccumulator);
            if (manaToRestore <= 0)
            {
                return;
            }

            CurrentMana.Value = Mathf.Min(MaxMana, CurrentMana.Value + manaToRestore);
            _manaRegenAccumulator -= manaToRestore;
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            // don't accept input if dead or blocked
            if (IsDead.Value) return;
            if (Category5.UI.PauseMenu.GameIsPaused || IsInPowerUpSelection() || Category5.UI.BossIntroUI.IntroIsPlaying) return;
            if (IsWindRiding) return;
            
            // instead of jumping immediately we buffer the input
            _jumpBufferCounter = _jumpBufferTime;
            
            // fire audio event for jump
            PlayerEvents.InvokeJump(transform.position);
        }

        private void OnDodge(InputAction.CallbackContext context)
        {
            // don't accept input if dead or blocked
            if (IsDead.Value) return;
            if (Category5.UI.PauseMenu.GameIsPaused || IsInPowerUpSelection() || Category5.UI.BossIntroUI.IntroIsPlaying) return;
            if (IsWindRiding) return;
            
            // block dodge while charging ranged attack
            if (_playerCombat != null && _playerCombat.IsCharging) return;
            
            if (_isDodging) return;

            if (!_isGrounded) 
            {
                _isGliding = true;
                return;
            }
            
            // use effective cooldown from player inventory if available
            float effectiveCooldown = _playerStats != null ? _playerStats.EffectiveDodgeCooldown : dodgeCooldown;
            if (Time.time < _lastDodgeTime + effectiveCooldown) return;

            StartDodge();
        }

        private void StartDodge()
        {
            _isDodging = true;
            _dodgeTimer = dodgeDuration;
            _lastDodgeTime = Time.time;
            
            // fire audio event for dodge
            PlayerEvents.InvokeDodge(transform.position);

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
        
        // sets the player velocity from an external system (wind riding exit momentum, knockback, etc)
        public void SetExternalVelocity(Vector3 velocity)
        {
            _externalVelocity = new Vector3(velocity.x, 0f, velocity.z);

            if (Mathf.Abs(velocity.y) > 0.001f)
            {
                _velocity.y = velocity.y;
            }
        }
        
        private void OnSprint(InputAction.CallbackContext context)
        {
            // dont accept input if dead or blocked
            if (IsDead.Value) return;
            if (Category5.UI.PauseMenu.GameIsPaused || IsInPowerUpSelection() || Category5.UI.BossIntroUI.IntroIsPlaying) return;
            if (IsWindRiding) return;
            
            _isSprinting = !_isSprinting;
            
            // fire events for ui/vfx
            if (_isSprinting)
            {
                OnSprintStarted?.Invoke(transform.position);
            }
            else
            {
                OnSprintEnded?.Invoke(transform.position);
            }
        }
        
        // method to cancel sprint (called by combat/abilities/etc)
        public void CancelSprint()
        {
            if (!_isSprinting) return;
            
            _isSprinting = false;
            OnSprintEnded?.Invoke(transform.position);
        }

        // spend mana — server only, used by item behaviours like Spiritual Well
        public void SpendMana(int amount)
        {
            if (!IsServer) return;
            CurrentMana.Value = Mathf.Max(0, CurrentMana.Value - amount);
        }

        // restore mana — server only
        public void RestoreMana(int amount)
        {
            if (!IsServer) return;
            CurrentMana.Value = Mathf.Min(MaxMana, CurrentMana.Value + amount);
        }

        public void TakeDamage(int damage)
        {
            if (!IsServer) return;
            
            // can't take damage if already dead
            if (IsDead.Value) return;
            
            // i-frame check
            if (_isDodging || _isInvulnerable)
            {
                // Debug.Log("Player dodged damage!");
                OnPlayerDodgedAttack?.Invoke(_dodgeTimer);
                return;
            }

            // apply airborne resistance before armor (Weather Balloon)
            int effectiveDamage = damage;
            if (!_isGrounded && AirborneResistanceMultiplier > 0f)
            {
                effectiveDamage = Mathf.RoundToInt(damage * (1f - AirborneResistanceMultiplier));
            }

            CurrentHealth.Value -= (_playerStats != null ? _playerStats.ApplyArmor(effectiveDamage) : effectiveDamage);
            // Debug.Log($"Player took {damage} damage (after armor). Health: {CurrentHealth.Value}");
            
            // cancel any charging attack when taking damage
            CancelChargeOnDamageClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { OwnerClientId }
                }
            });
            
            // trigger damage feedback on the player who took damage
            TriggerDamageFeedbackClientRpc(transform.position, damage, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { OwnerClientId }
                }
            });
            
            // notify all clients for vfx hook events
            NotifyPlayerDamagedClientRpc(transform.position, damage);
            
            if (CurrentHealth.Value <= 0)
            {
                Die();
            }
        }
        
        // cancel charge attack on the owning client when taking damage
        [ClientRpc]
        private void CancelChargeOnDamageClientRpc(ClientRpcParams clientRpcParams = default)
        {
            if (_playerCombat != null)
            {
                _playerCombat.CancelCharge();
            }
        }
        
        // trigger damage feedback for the player who took damage
        [ClientRpc]
        private void TriggerDamageFeedbackClientRpc(Vector3 position, int damage, ClientRpcParams clientRpcParams = default)
        {
            if (Core.HitFeedbackManager.Instance != null)
            {
                Core.HitFeedbackManager.Instance.TriggerPlayerDamaged(position);
            }
        }
        
        // notify all clients for vfx hook events
        [ClientRpc]
        private void NotifyPlayerDamagedClientRpc(Vector3 position, int damage)
        {
            if (Core.HitFeedbackManager.Instance != null)
            {
                Core.HitFeedbackManager.Instance.NotifyPlayerTakeDamage(position, damage);
            }
        }
        
        // handles player death (server only)
        private void Die()
        {
            if (!IsServer) return;
            if (IsDead.Value) return;

            // let items prevent death (e.g. Backup Plan)
            bool preventDeath = false;
            OnPlayerAboutToDie?.Invoke(this, ref preventDeath);
            if (preventDeath) return;
            
            IsDead.Value = true;
            
            // fire audio event for death on all clients
            NotifyPlayerDeathClientRpc(transform.position);
            
            // notify game flow manager for game over check
            if (Category5.Core.GameFlowManager.Instance != null)
            {
                Category5.Core.GameFlowManager.Instance.OnPlayerDied(OwnerClientId);
            }
        }
        
        [ClientRpc]
        private void NotifyPlayerDeathClientRpc(Vector3 position)
        {
            PlayerEvents.InvokeDeath(position);
        }
        
        // respawns the player at a spawn point with full health (server only)
        // works for both dead players (revives) and alive players (repositions and heals)
        public void Respawn()
        {
            if (!IsServer) return;
            
            bool wasDead = IsDead.Value;
            // Debug.Log($"Respawning player {OwnerClientId} (was dead: {wasDead})");
            
            // reset health and mana to max and revive if dead
            CurrentHealth.Value = MaxHealth;
            CurrentMana.Value = MaxMana;
            _manaRegenAccumulator = 0f;
            IsDead.Value = false;
            
            // move to spawn point
            var spawnPoint = Category5.Core.PlayerSpawnPoint.GetNextSpawnPoint();
            if (spawnPoint != null)
            {
                // need to temporarily disable character controller to move
                RespawnAtPositionClientRpc(spawnPoint.transform.position, spawnPoint.transform.rotation);
            }
        }
        
        [ClientRpc]
        private void RespawnAtPositionClientRpc(Vector3 position, Quaternion rotation)
        {
            // disable controller to allow teleport
            if (_controller != null)
            {
                _controller.enabled = false;
            }
            
            transform.position = position;
            transform.rotation = rotation;
            
            // re-enable controller
            if (_controller != null)
            {
                _controller.enabled = true;
            }
            
            // reset velocity
            _velocity = Vector3.zero;

            // clear any stale attack state so basic attacks work immediately on respawn
            if (_playerCombat != null)
            {
                _playerCombat.ResetCombatState();
            }
        }
        
        // consume mana (server rpc)
        [Rpc(SendTo.Server)]
        public void RequestConsumeManaServerRpc(int amount)
        {
            if (!IsServer) return;
            
            CurrentMana.Value = Mathf.Max(0, CurrentMana.Value - amount);
            
            // notify all clients
            NotifyManaChangedClientRpc(CurrentMana.Value, MaxMana);
        }
        
        // notify all clients of mana change
        [Rpc(SendTo.Everyone)]
        private void NotifyManaChangedClientRpc(int current, int max)
        {
            OnManaChanged?.Invoke(current, max);
        }
        
        // callback for mana network variable changes
        private void OnManaValueChanged(int oldValue, int newValue)
        {
            OnManaChanged?.Invoke(newValue, MaxMana);
        }
        
        // syncs spawn position to owning client after network spawn
        // only the owner needs to teleport since server already has correct position
        [ClientRpc]
        private void SyncSpawnPositionClientRpc(Vector3 position, Quaternion rotation)
        {
            // only owner needs to sync position
            if (!IsOwner) return;
            
            // disable controller to allow teleport
            if (_controller != null)
            {
                _controller.enabled = false;
            }
            
            transform.position = position;
            transform.rotation = rotation;
            
            // re-enable controller
            if (_controller != null)
            {
                _controller.enabled = true;
            }
            
            // reset velocity
            _velocity = Vector3.zero;
            
            // Debug.Log($"PlayerController: Synced spawn position to {position}");
        }
        
        // heals the player (server only) - used by lifesteal power-up
        public void Heal(int amount)
        {
            if (!IsServer) return;
            
            int newHealth = Mathf.Min(CurrentHealth.Value + amount, MaxHealth);
            if (newHealth > CurrentHealth.Value)
            {
                CurrentHealth.Value = newHealth;
                // Debug.Log($"Player healed {amount} HP. Health: {CurrentHealth.Value}/{MaxHealth}");
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

        public Vector3 GetMovementInputDirection()
        {
            if (_inputActions == null)
            {
                return Vector3.zero;
            }

            Vector2 moveInput = _inputActions.Player.Move.ReadValue<Vector2>();
            Vector3 move = Vector3.zero;

            if (_cameraTransform != null)
            {
                Vector3 cameraEuler = _cameraTransform.eulerAngles;
                Quaternion flatCameraRotation = Quaternion.Euler(0, cameraEuler.y, 0);
                move = flatCameraRotation * new Vector3(moveInput.x, 0, moveInput.y);
            }
            else
            {
                move = new Vector3(moveInput.x, 0, moveInput.y);
            }

            if (invertMovement)
            {
                move = -move;
            }

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            return move;
        }
        
        // get the aim direction (forward direction the player is facing)
        public Vector3 GetAimDirection()
        {
            return transform.forward;
        }
        
        // apply knockback to the player (used by abilities like grappling hook)
        public void ApplyKnockback(Vector3 knockbackForce)
        {
            if (_controller == null) return;
            
            // apply the knockback
            _controller.Move(knockbackForce * Time.deltaTime);
        }
        
        // called by Unity when CharacterController collides with something
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // ignore ground collisions (normal pointing upward)
            // dot product > 0.7 means the surface is mostly horizontal (ground/floor)
            float upwardDot = Vector3.Dot(hit.normal, Vector3.up);
            if (upwardDot > 0.7f)
            {
                // this is ground, ignore it
                return;
            }
            
            // Debug.Log($"PlayerController: OnControllerColliderHit with {hit.gameObject.name} (normal: {hit.normal}, upwardDot: {upwardDot:F2})");
            
            // fire body contact event when sprinting or dodging (used by Forceful Impact item)
            if (_isSprinting || _isDodging)
                OnBodyContact?.Invoke(this, hit.gameObject);

            // notify FighterE ability if it's grappling
            if (GetComponent<PlayerAbilityManager>() != null)
            {
                var abilityManager = GetComponent<PlayerAbilityManager>();
                // check if ability2 is FighterE and if it's grappling
                var fighterE = abilityManager.GetComponentInChildren<FighterE>();
                if (fighterE != null && fighterE.IsGrappling)
                {
                    // Debug.Log("PlayerController: Notifying FighterE of collision");
                    fighterE.OnPlayerCollision(hit.gameObject);
                }
            }
        }

        // re-caches renderer array after model swap so death visibility toggling works
        public void RefreshRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>();
        }
        
        // forwards movement/state data to the animator each frame
        private void UpdateAnimationParameters()
        {
            var anim = _playerModelManager != null ? _playerModelManager.ModelAnimator : null;
            if (anim == null) return;

            EnsureAnimatorParameterCache(anim);

            if (_hasAnimIsWindRiding)
            {
                anim.SetBool(_animIsWindRidingHash, IsWindRiding);
                if (_isGliding)
                {
                    anim.SetBool(_animIsWindRidingHash, true);
                }
                else
                {
                    
                    anim.SetBool(_animIsWindRidingHash, false);
                }
            }
            // movement speed (0 during dodge since dodge has its own animation)
            float speed = _isDodging ? 0f : Mathf.Clamp01(_moveInput.magnitude);
            if (_hasAnimSpeed)
            {
                anim.SetFloat(_animSpeedHash, speed, 0.1f, Time.deltaTime);
            }
            
            if (_hasAnimIsGrounded)
            {
                anim.SetBool(_animIsGroundedHash, _isGrounded);
            }

            if (_hasAnimIsDodging)
            {
                anim.SetBool(_animIsDodgingHash, _isDodging);
            }

            if (_hasAnimIsSprinting)
            {
                anim.SetBool(_animIsSprintingHash, _isSprinting);
            }

            if (_hasAnimVerticalVelocity)
            {
                anim.SetFloat(_animVerticalVelocityHash, _velocity.y);
            }

            // strafing directional inputs for 2d blend trees
            float directionalX = _isDodging ? 0f : Mathf.Clamp(_moveInput.x, -1f, 1f);
            float directionalY = _isDodging ? 0f : Mathf.Clamp(_moveInput.y, -1f, 1f);

            if (_hasAnimMoveX)
            {
                anim.SetFloat(_animMoveXHash, directionalX, 0.1f, Time.deltaTime);
            }

            if (_hasAnimMoveY)
            {
                anim.SetFloat(_animMoveYHash, directionalY, 0.1f, Time.deltaTime);
            }

            if (_hasAnimSpeedX)
            {
                anim.SetFloat(_animSpeedXHash, directionalX, 0.1f, Time.deltaTime);
            }

            if (_hasAnimSpeedY)
            {
                anim.SetFloat(_animSpeedYHash, directionalY, 0.1f, Time.deltaTime);
            }

            
        }

        // caches which animator params exist on the currently assigned controller
        // this is so i dont get spam of "parameter not found" warnings in the console
        private void EnsureAnimatorParameterCache(Animator anim)
        {
            var controller = anim.runtimeAnimatorController;
            if (_animParamsCached && _cachedAnimatorController == controller)
            {
                return;
            }

            _cachedAnimatorController = controller;
            _animParamsCached = true;

            _hasAnimSpeed = false;
            _hasAnimIsGrounded = false;
            _hasAnimIsDodging = false;
            _hasAnimIsDead = false;
            _hasAnimIsSprinting = false;
            _hasAnimVerticalVelocity = false;
            _hasAnimMoveX = false;
            _hasAnimMoveY = false;
            _hasAnimSpeedX = false;
            _hasAnimSpeedY = false;
            _hasAnimIsWindRiding = false;

            if (controller == null)
            {
                Debug.LogWarning("PlayerController: Animator has no controller assigned.");
                return;
            }

            var parameters = anim.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.nameHash == _animSpeedHash) _hasAnimSpeed = true;
                if (parameter.nameHash == _animIsGroundedHash) _hasAnimIsGrounded = true;
                if (parameter.nameHash == _animIsDodgingHash) _hasAnimIsDodging = true;
                if (parameter.nameHash == _animIsDeadHash) _hasAnimIsDead = true;
                if (parameter.nameHash == _animIsSprintingHash) _hasAnimIsSprinting = true;
                if (parameter.nameHash == _animVerticalVelocityHash) _hasAnimVerticalVelocity = true;
                if (parameter.nameHash == _animMoveXHash) _hasAnimMoveX = true;
                if (parameter.nameHash == _animMoveYHash) _hasAnimMoveY = true;
                if (parameter.nameHash == _animSpeedXHash) _hasAnimSpeedX = true;
                if (parameter.nameHash == _animSpeedYHash) _hasAnimSpeedY = true;
                if (parameter.nameHash == _animIsWindRidingHash) _hasAnimIsWindRiding = true;
            }

            LogMissingAnimatorParamsOnce();
        }

        // logs missing parameters once per controller assignment to make setup issues obvious
        private void LogMissingAnimatorParamsOnce()
        {
            bool hasMovePair = _hasAnimMoveX && _hasAnimMoveY;
            bool hasSpeedPair = _hasAnimSpeedX && _hasAnimSpeedY;

            if (!_hasAnimSpeed && !hasMovePair && !hasSpeedPair) Debug.LogWarning("PlayerController: Animator parameter missing: Speed");
            if (!_hasAnimIsGrounded) Debug.LogWarning("PlayerController: Animator parameter missing: IsGrounded");
            if (!_hasAnimIsDodging) Debug.LogWarning("PlayerController: Animator parameter missing: IsDodging");
            if (!_hasAnimIsDead) Debug.LogWarning("PlayerController: Animator parameter missing: IsDead");
            if (!_hasAnimIsSprinting) Debug.LogWarning("PlayerController: Animator parameter missing: IsSprinting");
            if (!_hasAnimVerticalVelocity) Debug.LogWarning("PlayerController: Animator parameter missing: VerticalVelocity");

            if (!hasMovePair && !hasSpeedPair)
            {
                Debug.LogWarning("PlayerController: Animator strafing parameters missing. add MoveX/MoveY (preferred) or SpeedX/SpeedY for strafe blend trees.");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position + groundCheckOffset, groundCheckRadius);
        }
    }
}
