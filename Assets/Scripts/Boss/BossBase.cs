using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Category5.Core;
using Category5.Player;
using Category5.Audio;
using Category5.UI;
using System.Collections.Generic;

namespace Category5.Boss
{
    public enum BossState
    {
        Idle,
        Telegraph,
        Attack,
        Cooldown
    }
    
    public enum BossMovementStyle
    {
        Direct,          // moves straight toward target
        Strafe,          // circles around target while approaching
        ChargeAndRetreat // rushes in, backs off after attacks
    }

    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(Rigidbody))]
    public abstract class BossBase : NetworkBehaviour, IDamageable
    {
        [Header("data")]
        [Tooltip("scriptable object defining all stats, attacks, and visuals for this boss")]
        [SerializeField] protected BossData bossData;

        [Header("stats")]
        [SerializeField] protected int maxHealth = 500;
        public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>();

        public int MaxHealth => maxHealth;

        // read-only access to the SO driving this boss — used by GameFlowManager for swap detection
        public BossData BossData => bossData;

        [Header("state timings")]
        [SerializeField] protected float idleDuration = 2f;
        [SerializeField] protected float telegraphDuration = 1.5f;
        [SerializeField] protected float cooldownDuration = 1f;
        
        [Header("targeting")]
        [SerializeField] protected float targetUpdateInterval = 0.5f;
        private float _targetUpdateTimer;
        protected Transform currentTarget;
        protected PlayerController currentTargetController;

        // damage reduction applied by items like Storm Suppressor (weaker on bosses by design)
        public float DamageOutputMultiplier { get; set; } = 1f;
        
        [Header("rotation")]
        [SerializeField] protected float rotationSpeed = 5f;
        [SerializeField] protected bool rotatesDuringIdle = true;
        [SerializeField] protected bool rotatesDuringTelegraph = true;
        [SerializeField] protected bool rotatesDuringAttack = false;
        
        [Header("movement")]
        [SerializeField] protected float moveSpeed = 3f;
        [SerializeField] protected float preferredDistance = 5f;
        [SerializeField] protected float chaseDistance = 15f;
        [SerializeField] protected bool movesDuringIdle = true;
        [SerializeField] protected bool movesDuringTelegraph = false;
        [SerializeField] protected BossMovementStyle movementStyle = BossMovementStyle.Direct;
        protected float _effectiveMoveSpeed;

        protected struct MovementModifier
        {
            public string sourceId;
            public float multiplier;
            public float remainingDuration;

            public MovementModifier(string sourceId, float multiplier, float duration)
            {
                this.sourceId = sourceId;
                this.multiplier = multiplier;
                remainingDuration = duration;
            }
        }

        protected readonly Dictionary<string, MovementModifier> _movementModifiers = new Dictionary<string, MovementModifier>();
        
        // rigidbody for physics collision
        protected Rigidbody _rigidbody;
        
        [Header("ground check")]
        public float groundCheckRadius = 0.3f;
        public Vector3 groundCheckOffset = new Vector3(0f, 0.3f, 0f);
        public LayerMask groundLayers = 1; // default layer
        public bool showGroundCheckGizmo = true;
        protected bool _isGrounded = false;

        [Header("ground check stability")]
        [SerializeField] protected int groundedConfirmFrames = 3;
        [SerializeField] protected int groundedLossFrames = 3;
        private int _groundedTrueCounter = 0;
        private int _groundedFalseCounter = 0;

        [Header("gravity")]
        public float gravity = -20f;
        public float groundedStickForce = -2f;
        public float terminalVelocity = -50f;
        protected float _verticalVelocity = 0f;
        
        [Header("vfx/feedback")]
        [Tooltip("Default attack type for vfx hooks, can be overridden by subclass")]
        [SerializeField] protected BossAttackType defaultAttackType = BossAttackType.Slam;

        protected NetworkVariable<BossState> currentState = new NetworkVariable<BossState>(BossState.Idle);
        // public read-only access for bossvisuals (and other external systems) to subscribe to state changes
        public NetworkVariable<BossState> CurrentBossState => currentState;
        protected float stateTimer;
        
        // current attack type for vfx hooks
        protected BossAttackType currentAttackType = BossAttackType.None;
        
        // cached spawn position for killbox teleport
        protected Vector3 _initialSpawnPosition;
        protected Quaternion _initialSpawnRotation;

        // flag to prevent multiple death triggers
        private bool _isDead = false;
        private bool _isHidden = false;

        // server-side countdown that keeps the boss frozen while the intro card plays
        private float _introDormancyTimer = 0f;
        protected BossVisuals _bossVisuals;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // copy stats from SO into runtime fields before anything else
                InitializeFromData();

                CurrentHealth.Value = maxHealth;
                currentState.Value = BossState.Idle;
                stateTimer = idleDuration;
                _isDead = false;
                _targetUpdateTimer = 0f;

                // cache initial spawn position for killbox recovery
                _initialSpawnPosition = transform.position;
                _initialSpawnRotation = transform.rotation;

                // freeze boss ai and trigger the intro card on all clients
                _introDormancyTimer = bossData != null ? bossData.introDuration : 0f;
                TriggerBossIntroClientRpc();
            }
            
            // configure rigidbody: kinematic so we control movement manually
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                // only freeze x/z rotation
                _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }

            CurrentHealth.OnValueChanged += OnHealthChanged;
            
            // try to register with ui, it may not be ready yet on scene load
            TryRegisterWithUI();
            
            // register with game flow manager
            if (IsServer && Category5.Core.GameFlowManager.Instance != null)
            {
                Category5.Core.GameFlowManager.Instance.RegisterBoss(this);
            }

            // initialize minimap trackable for radar display (boss icon is larger and orange)
            InitializeMinimapTrackable();

            // cache and initialize the visuals component for animation/hit flash (runs on all clients)
            _bossVisuals = GetComponent<BossVisuals>();
            _bossVisuals?.Initialize(this);
        }

        // copies stats from bossData SO into runtime fields — subclasses can override to pull extra data
        protected virtual void InitializeFromData()
        {
            if (bossData == null) return;

            maxHealth            = bossData.baseHealth;
            moveSpeed            = bossData.moveSpeed;
            rotationSpeed        = bossData.rotationSpeed;
            preferredDistance    = bossData.preferredDistance;
            chaseDistance        = bossData.chaseDistance;
            idleDuration         = bossData.idleDuration;
            cooldownDuration     = bossData.cooldownDuration;
            movementStyle        = bossData.movementStyle;
            rotatesDuringIdle    = bossData.rotatesDuringIdle;
            rotatesDuringTelegraph = bossData.rotatesDuringTelegraph;
            rotatesDuringAttack  = bossData.rotatesDuringAttack;
            movesDuringIdle      = bossData.movesDuringIdle;
            movesDuringTelegraph = bossData.movesDuringTelegraph;

            if (bossData.scaleMultiplier != 1f)
                transform.localScale = Vector3.one * bossData.scaleMultiplier;
        }

        // sets up minimap trackable component for radar visibility
        private void InitializeMinimapTrackable()
        {
            var trackable = GetComponent<MinimapTrackable>();
            if (trackable == null)
            {
                trackable = gameObject.AddComponent<MinimapTrackable>();
            }
            trackable.Configure(TrackableType.Boss, new Color(1f, 0.6f, 0f), 1.5f);
        }
        
        private void TryRegisterWithUI()
        {
            if (Category5.UI.UIManager.Instance != null)
            {
                Category5.UI.UIManager.Instance.RegisterBoss(this);
            }
            else
            {
                // UIManager not ready yet, it will find us when it initializes
                // Debug.Log("BossBase: UIManager not ready, waiting for it to register us");
            }
        }

        public override void OnNetworkDespawn()
        {
            CurrentHealth.OnValueChanged -= OnHealthChanged;
        }

        protected virtual void Update()
        {
            if (!IsServer) return;
            if (_isHidden) return;

            UpdateMovementModifiers();

            // ground check and gravity run before state logic so vertical displacement
            // is always applied regardless of what state the boss is in
            UpdateGroundCheck();
            ApplyGravity();
            UpdateTargetTimer();
            HandleStateMachine();
            ApplyVerticalDisplacement();
        }

        private void HandleStateMachine()
        {
            // boss stays frozen while the intro card is playing
            if (_introDormancyTimer > 0f)
            {
                _introDormancyTimer -= Time.deltaTime;
                return;
            }

            stateTimer -= Time.deltaTime;

            if (stateTimer <= 0)
            {
                TransitionState();
            }

            // execute logic for current state
            switch (currentState.Value)
            {
                case BossState.Idle:
                    if (rotatesDuringIdle) RotateTowardTarget();
                    if (movesDuringIdle) MoveTowardTarget();
                    OnIdleUpdate();
                    break;
                case BossState.Telegraph:
                    if (rotatesDuringTelegraph) RotateTowardTarget();
                    if (movesDuringTelegraph) MoveTowardTarget();
                    OnTelegraphUpdate();
                    break;
                case BossState.Attack:
                    if (rotatesDuringAttack) RotateTowardTarget();
                    OnAttackUpdate();
                    break;
                case BossState.Cooldown:
                    OnCooldownUpdate();
                    break;
            }
        }

        protected virtual void TransitionState()
        {
            // basic loop: idle -> telegraph -> attack -> cooldown -> idle
            switch (currentState.Value)
            {
                case BossState.Idle:
                    StartTelegraph();
                    break;
                case BossState.Telegraph:
                    StartAttack();
                    break;
                case BossState.Attack:
                    StartCooldown();
                    break;
                case BossState.Cooldown:
                    StartIdle();
                    break;
            }
        }

        // state entry methods
        protected virtual void StartIdle()
        {
            currentState.Value = BossState.Idle;
            stateTimer = idleDuration;
            // sync visuals if needed
        }

        protected virtual void StartTelegraph()
        {
            currentState.Value = BossState.Telegraph;
            stateTimer = telegraphDuration;
            SelectNextAttack();
            // show telegraph visual
            
            // notify vfx hooks for telegraph
            NotifyBossTelegraphClientRpc(currentAttackType, transform.position);
        }

        protected virtual void StartAttack()
        {
            currentState.Value = BossState.Attack;
            // duration depends on the specific attack
            stateTimer = 1f; 
            ExecuteAttack();
            
            // notify vfx hooks for attack execution
            NotifyBossAttackClientRpc(currentAttackType, transform.position);
        }

        protected virtual void StartCooldown()
        {
            currentState.Value = BossState.Cooldown;
            stateTimer = cooldownDuration;
        }

        // abstract/virtual methods for specific boss implementations
        protected abstract void SelectNextAttack();
        protected abstract void ExecuteAttack();

        protected virtual void OnIdleUpdate() { }
        protected virtual void OnTelegraphUpdate() { }
        protected virtual void OnAttackUpdate() { }
        protected virtual void OnCooldownUpdate() { }

        // =====================================
        // target tracking
        // =====================================
        
        private void UpdateTargetTimer()
        {
            _targetUpdateTimer -= Time.deltaTime;
            if (_targetUpdateTimer <= 0f)
            {
                _targetUpdateTimer = targetUpdateInterval;
                UpdateTarget();
            }
        }
        
        protected virtual void UpdateTarget()
        {
            if (!IsServer) return;
            if (NetworkManager.Singleton == null) return;
            
            Transform nearestPlayer = null;
            PlayerController nearestController = null;
            float nearestDistance = float.MaxValue;
            
            // iterate through all connected clients to find the nearest alive player
            foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
            {
                NetworkClient client = clientPair.Value;
                if (client.PlayerObject == null) continue;
                
                PlayerController playerController = client.PlayerObject.GetComponent<PlayerController>();
                if (playerController == null) continue;
                
                // skip dead players
                if (playerController.IsDead.Value) continue;
                
                float distance = Vector3.Distance(transform.position, playerController.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPlayer = playerController.transform;
                    nearestController = playerController;
                }
            }
            
            currentTarget = nearestPlayer;
            currentTargetController = nearestController;
        }
        
        // =====================================
        // rotation
        // =====================================
        
        protected virtual void RotateTowardTarget()
        {
            if (currentTarget == null) return;
            
            // get direction to target, flattened on y-axis
            Vector3 directionToTarget = currentTarget.position - transform.position;
            directionToTarget.y = 0f;
            
            if (directionToTarget.sqrMagnitude < 0.001f) return;
            
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        
        // =====================================
        // movement
        // =====================================
        
        protected virtual void MoveTowardTarget()
        {
            if (currentTarget == null) return;
            
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
            
            // dont move if already at preferred distance
            if (distanceToTarget <= preferredDistance) return;
            
            // only start chasing if beyond chase distance (or always chase if chase distance is 0)
            if (chaseDistance > 0f && distanceToTarget < chaseDistance && distanceToTarget > preferredDistance)
            {
                // in the chase zone, move toward target
            }
            else if (distanceToTarget <= chaseDistance)
            {
                // within chase distance but above preferred, dont move
                return;
            }
            
            Vector3 moveDirection = GetMoveDirection();
            ApplyMovement(moveDirection);
        }
        
        protected virtual Vector3 GetMoveDirection()
        {
            if (currentTarget == null) return Vector3.zero;
            
            Vector3 directionToTarget = currentTarget.position - transform.position;
            directionToTarget.y = 0f;
            
            switch (movementStyle)
            {
                case BossMovementStyle.Direct:
                    return directionToTarget.normalized;
                    
                case BossMovementStyle.Strafe:
                    // add a perpendicular component for circling behavior
                    Vector3 perpendicular = Vector3.Cross(Vector3.up, directionToTarget.normalized);
                    return (directionToTarget.normalized * 0.7f + perpendicular * 0.3f).normalized;
                    
                case BossMovementStyle.ChargeAndRetreat:
                    // handled differently in subclass, default to direct
                    return directionToTarget.normalized;
                    
                default:
                    return directionToTarget.normalized;
            }
        }
        
        protected virtual void ApplyMovement(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;
            
            Vector3 movement = direction * _effectiveMoveSpeed * Time.deltaTime;
            
            if (_rigidbody != null)
            {
                // use MovePosition for correct kinematic physics integration
                _rigidbody.MovePosition(_rigidbody.position + movement);
            }
            else
            {
                // fallback to direct transform movement just in case
                transform.position += movement;
            }
        }

        protected virtual void UpdateMovementModifiers()
        {
            List<string> toRemove = new List<string>();
            foreach (var kvp in _movementModifiers)
            {
                MovementModifier modifier = kvp.Value;
                modifier.remainingDuration -= Time.deltaTime;

                if (modifier.remainingDuration <= 0f)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                _movementModifiers[kvp.Key] = modifier;
            }

            foreach (string key in toRemove)
            {
                _movementModifiers.Remove(key);
            }

            float lowestMultiplier = 1f;
            foreach (MovementModifier modifier in _movementModifiers.Values)
            {
                if (modifier.multiplier < lowestMultiplier)
                {
                    lowestMultiplier = modifier.multiplier;
                }
            }

            _effectiveMoveSpeed = moveSpeed * lowestMultiplier;
        }

        public void ApplyMovementModifier(float multiplier, float duration, string sourceId)
        {
            if (!IsServer) return;

            _movementModifiers[sourceId] = new MovementModifier(sourceId, multiplier, duration);
            UpdateMovementModifiers();
        }
        
        // helper to get distance to current target
        protected float GetDistanceToTarget()
        {
            if (currentTarget == null) return float.MaxValue;
            return Vector3.Distance(transform.position, currentTarget.position);
        }
        
        // helper to check if target is in attack range
        protected bool IsTargetInRange(float range)
        {
            return GetDistanceToTarget() <= range;
        }
        
        // helper to get direction to current target (flattened on y)
        protected Vector3 GetDirectionToTarget()
        {
            if (currentTarget == null) return transform.forward;
            
            Vector3 direction = currentTarget.position - transform.position;
            direction.y = 0f;
            return direction.normalized;
        }

        // idamageable implementation
        public void TakeDamage(int damage)
        {
            // Debug.Log($"BossBase.TakeDamage called: damage={damage}, isServer={IsServer}");
            
            if (!IsServer) return;

            CurrentHealth.Value -= damage;
            // Debug.Log($"boss took {damage} damage. health: {CurrentHealth.Value}");
            
            // fire audio event for boss hurt on all clients
            NotifyBossHurtClientRpc(transform.position, damage);

            if (CurrentHealth.Value <= 0)
            {
                Die();
            }
        }

        protected virtual void OnHealthChanged(int oldHealth, int newHealth)
        {
            // update ui or play hit effects
        }

        protected virtual void Die()
        {
            if (_isDead) return; // prevent multiple death calls
            _isDead = true;
            
            // Debug.Log("BossBase: Boss died!");
            
            // fire audio event for boss death on all clients
            NotifyBossDeathClientRpc(transform.position);
            
            // notify game flow manager instead of despawning immediately
            if (Category5.Core.GameFlowManager.Instance != null)
            {
                // hide boss visually during item selection
                HideBossClientRpc();
                
                Category5.Core.GameFlowManager.Instance.OnBossDied();
                // boss will be reset by GameFlowManager when item selection completes
            }
            else
            {
                Debug.LogWarning("BossBase: GameFlowManager not found! Make sure GameFlowManager is in the scene.");
                // fallback if no manager - just despawn
                GetComponent<NetworkObject>().Despawn();
            }
        }
        
        // public method for GameFlowManager to hide boss
        public void HideBoss()
        {
            if (!IsServer) return;
            HideBossClientRpc();
        }

        [ClientRpc]
        private void TriggerBossIntroClientRpc()
        {
            // bossData is a serialized field on the prefab — already present on all clients, no network data needed
            // pass boss world position so the camera can rotate to face it
            Category5.Audio.BossEvents.InvokeIntro(bossData, transform.position);
        }

        [ClientRpc]
        private void HideBossClientRpc()
        {
            // hide boss without deactivating the network object
            SetBossHiddenState(true);
        }
        
        [ClientRpc]
        private void ShowBossClientRpc()
        {
            // show boss again
            SetBossHiddenState(false);
        }

        private void SetBossHiddenState(bool hidden)
        {
            _isHidden = hidden;

            // disable visuals
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = !hidden;
            }

            // disable combat collisions while hidden
            var colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = !hidden;
            }

            // freeze all rigidbody motion while hidden, restore rotation constraints when shown
            if (_rigidbody != null)
            {
                _rigidbody.constraints = hidden
                    ? RigidbodyConstraints.FreezeAll
                    : RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }

            // hide/show minimap icon by toggling the trackable component
            var trackable = GetComponent<MinimapTrackable>();
            if (trackable != null)
                trackable.enabled = !hidden;

            // sync boss health bar visibility with boss visibility
            if (hidden)
                Category5.UI.UIManager.Instance?.HideBossHealthBar();
            else
                _bossVisuals?.PlaySpawnAnimation();
        }
        
        // called by PowerUpManager to reset boss for new round with scaled hp
        public virtual void ResetBoss(int newMaxHealth, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            if (!IsServer) return;
            
            // Debug.Log($"BossBase: Resetting boss with {newMaxHealth} HP at position {spawnPosition}");
            
            // teleport boss to spawn position and clear any accumulated vertical velocity
            if (_rigidbody != null)
            {
                _rigidbody.position = spawnPosition;
                _rigidbody.rotation = spawnRotation;
            }
            else
            {
                transform.position = spawnPosition;
                transform.rotation = spawnRotation;
            }
            _verticalVelocity = 0f;
            
            // update cached spawn position for killbox recovery
            _initialSpawnPosition = spawnPosition;
            _initialSpawnRotation = spawnRotation;

            maxHealth = newMaxHealth;
            CurrentHealth.Value = maxHealth;
            currentState.Value = BossState.Idle;
            stateTimer = idleDuration;
            _isDead = false;
            _isHidden = false;
            
            // show boss again and notify clients about the reset
            ShowBossClientRpc();
            ResetBossClientRpc(newMaxHealth);

            // freeze boss ai and trigger the intro card on all clients
            _introDormancyTimer = bossData != null ? bossData.introDuration : 0f;
            TriggerBossIntroClientRpc();
            
            // fire audio event for boss spawn on all clients (use spawn position)
            NotifyBossSpawnClientRpc(spawnPosition);
            
            // re-register with ui for updated health bar
            TryRegisterWithUI();
        }
        
        [ClientRpc]
        private void ResetBossClientRpc(int newMaxHealth)
        {
            // clients need to update their reference to max health for ui
            maxHealth = newMaxHealth;
            TryRegisterWithUI();
        }
        
        // teleports boss back to its cached spawn position (used by killbox)
        public void TeleportToSpawn()
        {
            if (!IsServer) return;

            if (_rigidbody != null)
            {
                _rigidbody.position = _initialSpawnPosition;
                _rigidbody.rotation = _initialSpawnRotation;
            }
            else
            {
                transform.position = _initialSpawnPosition;
                transform.rotation = _initialSpawnRotation;
            }
            _verticalVelocity = 0f;

            // reset to idle state so boss resumes behavior
            currentState.Value = BossState.Idle;
            stateTimer = idleDuration;
        }

        // =====================================
        // vfx hook clientrpcs
        // =====================================
        [ClientRpc]
        protected void NotifyBossTelegraphClientRpc(BossAttackType attackType, Vector3 position)
        {
            if (HitFeedbackManager.Instance != null)
            {
                HitFeedbackManager.Instance.NotifyBossAttackTelegraph(attackType, position);
            }
        }
        
        [ClientRpc]
        protected void NotifyBossAttackClientRpc(BossAttackType attackType, Vector3 position)
        {
            if (HitFeedbackManager.Instance != null)
            {
                HitFeedbackManager.Instance.NotifyBossAttackExecute(attackType, position);
            }
        }
        
        // helper method for subclasses to trigger feedback when boss hits players
        protected void TriggerBossHitFeedback(Vector3 position, bool isHeavyAttack = false)
        {
            TriggerBossHitFeedbackClientRpc(position, isHeavyAttack);
        }
        
        [ClientRpc]
        private void TriggerBossHitFeedbackClientRpc(Vector3 position, bool isHeavyAttack)
        {
            if (HitFeedbackManager.Instance == null) return;
            
            if (isHeavyAttack)
            {
                HitFeedbackManager.Instance.TriggerBossSlam(position);
            }
            else
            {
                HitFeedbackManager.Instance.TriggerHeavyHit(position);
            }
        }
        
        // =====================================
        // audio event clientrpcs
        // =====================================
        
        [ClientRpc]
        private void NotifyBossDeathClientRpc(Vector3 position)
        {
            BossEvents.InvokeDeath(position);
            _bossVisuals?.TriggerDeathAnimation();
        }
        
        [ClientRpc]
        private void NotifyBossSpawnClientRpc(Vector3 position)
        {
            BossEvents.InvokeSpawn(position);
        }
        
        [ClientRpc]
        private void NotifyBossHurtClientRpc(Vector3 position, int damage)
        {
            BossEvents.InvokeHurt(position, damage);
            _bossVisuals?.TriggerHurtAnimation();
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (!showGroundCheckGizmo) return;

            // draw the ground check in the scene view so we can tune it
            Gizmos.color = _isGrounded ? new Color(0f, 1f, 0f, 0.6f) : new Color(1f, 0.3f, 0f, 0.6f);
            Gizmos.DrawSphere(transform.position + groundCheckOffset, groundCheckRadius);
        }

        // sphere check with confirm/loss hysteresis to avoid flickering ground state
        protected void UpdateGroundCheck()
        {
            Vector3 checkPos = transform.position + groundCheckOffset;
            bool raw = Physics.CheckSphere(checkPos, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);

            if (raw)
            {
                _groundedTrueCounter++;
                _groundedFalseCounter = 0;
                if (_groundedTrueCounter >= groundedConfirmFrames)
                    _isGrounded = true;
            }
            else
            {
                _groundedFalseCounter++;
                _groundedTrueCounter = 0;
                if (_groundedFalseCounter >= groundedLossFrames)
                    _isGrounded = false;
            }
        }

        // accumulate vertical velocity from gravity reset to stick force when grounded
        protected void ApplyGravity()
        {
            if (_isGrounded)
            {
                _verticalVelocity = groundedStickForce;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
                if (_verticalVelocity < terminalVelocity) _verticalVelocity = terminalVelocity;
            }
        }

        // apply vertical displacement after other movement so the boss always falls when off the ground
        protected void ApplyVerticalDisplacement()
        {
            float deltaY = _verticalVelocity * Time.deltaTime;

            // always resolve the collider once upfront so both branches share it
            Collider col = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();

            if (_isGrounded)
            {
                // snap to ground surface / cast from just above collider bottom so scale doesnt matter
                if (col != null)
                {
                    Vector3 rayOrigin = new Vector3(transform.position.x, col.bounds.min.y + 0.15f, transform.position.z);
                    RaycastHit hit;
                    if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 0.4f, groundLayers, QueryTriggerInteraction.Ignore))
                    {
                        float diff = hit.point.y - col.bounds.min.y;
                        if (diff > 0.001f)
                            transform.position += Vector3.up * diff;
                    }
                }
                return;
            }

            if (deltaY < 0f)
            {
                // cast from near the collider bottom so the ray only needs to travel a short
                // distance regardless of how tall/scaled the boss is
                Vector3 rayOrigin = col != null
                    ? new Vector3(transform.position.x, col.bounds.min.y + 0.15f, transform.position.z)
                    : transform.position + Vector3.up * 0.1f;
                float castDistance = Mathf.Abs(deltaY) + 0.15f + groundCheckRadius;
                RaycastHit hit;
                if (Physics.Raycast(rayOrigin, Vector3.down, out hit, castDistance, groundLayers, QueryTriggerInteraction.Ignore))
                {
                    if (col != null)
                    {
                        float shift = hit.point.y - col.bounds.min.y;
                        transform.position += Vector3.up * shift;
                    }
                    else
                    {
                        transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
                    }
                    _verticalVelocity = groundedStickForce;
                    _isGrounded = true;
                }
                else
                {
                    transform.position += Vector3.up * deltaY;
                    _isGrounded = false;
                }
            }
            else if (deltaY > 0f)
            {
                transform.position += Vector3.up * deltaY;
            }
        }
    }
}
