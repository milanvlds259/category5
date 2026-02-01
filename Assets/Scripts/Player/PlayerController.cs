using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine.InputSystem;
using Category5;
using Category5.Core;
using Category5.PowerUps;
using Category5.Items;
using Category5.Audio;
using Category5.UI;

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
        
        [Header("Health")]
        [SerializeField] private int baseMaxHealth = 100;
        public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(100);
        
        // death state synced across network
        public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(false);
        
        // reference to player stats for stat modifiers
        private PlayerStats _playerStats;
        
        // effective max health including item/power-up bonuses
        public int MaxHealth => _playerStats != null ? _playerStats.TotalMaxHealth : baseMaxHealth;
        
        [Header("Death Settings")]
        [SerializeField] private GameObject[] visualsToHideOnDeath; // optional: specific objects to hide
        private Renderer[] _renderers; // cached renderers for visibility toggle

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
        
        // cached reference to player combat for charge state
        private PlayerCombat _playerCombat;
        
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
            _playerCombat = GetComponent<PlayerCombat>();
            
            // cache all renderers for death visibility toggle
            _renderers = GetComponentsInChildren<Renderer>();
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
            Debug.Log($"PlayerController: Name changed from '{oldName}' to '{newName}'");
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
            Debug.Log($"PlayerController: Server set name to '{name}' for client {OwnerClientId}");
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
        }
        
        // enables/disables visuals and collider based on death state
        private void SetDeathVisuals(bool isDead)
        {
            // toggle renderers
            if (_renderers != null)
            {
                foreach (var renderer in _renderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = !isDead;
                    }
                }
            }
            
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
            
            // dead players cannot do anything
            if (IsDead.Value) return;
            
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
            // check ItemManager first (new system), fallback to PowerUpManager (legacy)
            if (Category5.Items.ItemManager.Instance != null)
            {
                return Category5.Items.ItemManager.Instance.CurrentPhase.Value == Category5.Core.GamePhase.PowerUpSelection;
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
                // apply charge movement speed reduction if charging
                float effectiveSpeed = moveSpeed;
                
                // apply speed multiplier from inventory (items and abilities)
                if (_playerStats != null)
                {
                    effectiveSpeed *= _playerStats.GetEffectiveSpeedMultiplier();
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
            // don't accept input if dead or blocked
            if (IsDead.Value) return;
            if (Category5.UI.PauseMenu.GameIsPaused || IsInPowerUpSelection()) return;
            
            // instead of jumping immediately we buffer the input
            _jumpBufferCounter = _jumpBufferTime;
            
            // fire audio event for jump
            PlayerEvents.InvokeJump(transform.position);
        }

        private void OnDodge(InputAction.CallbackContext context)
        {
            // don't accept input if dead or blocked
            if (IsDead.Value) return;
            if (Category5.UI.PauseMenu.GameIsPaused || IsInPowerUpSelection()) return;
            
            // block dodge while charging ranged attack
            if (_playerCombat != null && _playerCombat.IsCharging) return;
            
            if (_isDodging || !_isGrounded) return;
            
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

        public void TakeDamage(int damage)
        {
            if (!IsServer) return;
            
            // can't take damage if already dead
            if (IsDead.Value) return;
            
            // i-frame check
            if (_isDodging) 
            {
                Debug.Log("Player dodged damage!");
                return;
            }

            CurrentHealth.Value -= damage;
            Debug.Log($"Player took {damage} damage. Health: {CurrentHealth.Value}");
            
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
            
            Debug.Log($"Player {OwnerClientId} died!");
            IsDead.Value = true;
            
            // fire audio event for death on all clients
            NotifyPlayerDeathClientRpc(transform.position);
            
            // notify power-up manager for game over check
            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.OnPlayerDied(OwnerClientId);
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
            Debug.Log($"Respawning player {OwnerClientId} (was dead: {wasDead})");
            
            // reset health to max and revive if dead
            CurrentHealth.Value = MaxHealth;
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
            
            Debug.Log($"PlayerController: Synced spawn position to {position}");
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
            
            Debug.Log($"PlayerController: OnControllerColliderHit with {hit.gameObject.name} (normal: {hit.normal}, upwardDot: {upwardDot:F2})");
            
            // notify FighterE ability if it's grappling
            if (GetComponent<PlayerAbilityManager>() != null)
            {
                var abilityManager = GetComponent<PlayerAbilityManager>();
                // check if ability2 is FighterE and if it's grappling
                var fighterE = abilityManager.GetComponentInChildren<FighterE>();
                if (fighterE != null && fighterE.IsGrappling)
                {
                    Debug.Log("PlayerController: Notifying FighterE of collision");
                    fighterE.OnPlayerCollision(hit.gameObject);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position + groundCheckOffset, groundCheckRadius);
        }
    }
}
