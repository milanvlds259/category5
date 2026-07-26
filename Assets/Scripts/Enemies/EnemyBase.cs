using System;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using Category5.Core;
using Category5.Audio;
using Category5.Player;
using Category5.UI;
using Category5.WeakPoints;
using System.Collections.Generic;

namespace Category5.Enemies
{
    // movement modifier for abilities like spiralbow slow
    [System.Serializable]
    public class MovementModifier
    {
        public string sourceId; // identifier for the source (e.g., "Spiralbow_Player1")
        public float multiplier; // speed multiplier (0.6 = 60% speed)
        public float remainingDuration; // time left on this modifier
        
        public MovementModifier(string id, float mult, float duration)
        {
            sourceId = id;
            multiplier = mult;
            remainingDuration = duration;
        }
    }
    
    // enemy states for the state machine
    public enum EnemyState
    {
        Idle,       // waiting, no target
        Chase,      // moving toward target
        Attack,     // executing an attack
        Stagger,    // briefly stunned after taking damage
        Dead        // defeated
    }

    // abstract base class for all enemies
    // handles health, targeting, state machine, movement, and networking
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public abstract class EnemyBase : NetworkBehaviour, IDamageable, IWeakPointHost
    {
        [Header("enemy data")]
        [SerializeField] protected EnemyData enemyData;
        
        [Header("components")]
        [SerializeField] protected EnemyHealthBar healthBar;
        
        // visuals component handles animator + hit flash on all clients
        protected EnemyVisuals _visuals;
        
        // networked state
        public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>();
        public NetworkVariable<EnemyState> CurrentState = new NetworkVariable<EnemyState>(EnemyState.Idle);
        
        // runtime stats (populated from the EnemyData asset at spawn)
        protected int maxHealth = 50;
        protected float moveSpeed = 4f;
        protected float rotationSpeed = 360f;
        protected int damage = 10;
        protected float attackRange = 2f;
        protected float attackCooldown = 1.5f;
        protected float staggerDuration = 0.3f;
        protected float detectionRange = 15f;
        protected float leashRange = 25f;
        protected ElementType elementType = ElementType.None;

        // damage reduction applied by items like Storm Suppressor (0 = no reduction, 0.15 = 15% less damage)
        public float DamageOutputMultiplier { get; set; } = 1f;
        
        // movement modifiers (for abilities like spiralbow slow)
        private Dictionary<string, MovementModifier> _movementModifiers = new Dictionary<string, MovementModifier>();
        // pre-allocated list so UpdateMovementModifiers doesn't allocate every frame
        private readonly List<string> _keysToRemove = new List<string>(4);
        private float _effectiveMoveSpeed;

        [Header("timing & targeting")]
        [SerializeField] protected float targetUpdateInterval = 0.5f;
        
        // ground check - values come from enemyData.physics on spawn
        protected float groundCheckRadius = 0.2f;
        protected Vector3 groundCheckOffset = new Vector3(0f, -0.64f, 0f);
        public LayerMask groundLayers = 1;
        public bool showGroundCheckGizmo = true;
        protected bool _isGrounded = false;

        // ground check stability counters
        private int _groundedConfirmFrames = 2;
        private int _groundedLossFrames = 2;
        private int _groundedTrueCounter = 0;
        private int _groundedFalseCounter = 0;

        // gravity - values come from enemyData.physics on spawn
        protected float gravity = -20f;
        protected float groundedStickForce = -2f;
        protected float terminalVelocity = -50f;
        protected float _verticalVelocity = 0f;
        
        // current selected attack (chosen each time we enter attack state)
        protected EnemyAttackData _currentAttack;
        // tracks whether base damage was already dealt this attack cycle
        private bool _hasDealtDamageThisAttack;
        private float _damageDelayTimer;
        
        // targeting
        protected Transform currentTarget;
        protected PlayerController currentTargetController;
        private float _targetUpdateTimer;
        
        // state timers
        protected float stateTimer;
        protected float attackCooldownTimer;
        
        // movement
        protected Vector3 spawnPosition;
        protected float _groundY;

        // navmesh agent - server only
        // clients have it disabled since NetworkTransform handles position sync
        private NavMeshAgent _agent;
        // true when the agent is enabled and successfully placed on the navmesh
        private bool _agentOnMesh;
        // set when ApplyLaunch boots the enemy off the navmesh so we know to re-enable it
        private bool _agentDisabledByLaunch;
        
        // rigidbody for physics interactions (kinematic since we control movement)
        protected Rigidbody _rigidbody;
        
        // reference to spawner for death notification
        private EnemySpawner _spawner;
        
        // flag to prevent multiple death triggers
        private bool _isDead = false;

        // =====================================
        // lifecycle
        // =====================================
        
        protected virtual void Awake()
        {
            // cache visuals - handles animator and hit flash on all clients
            _visuals = GetComponent<EnemyVisuals>();

            // cache nav mesh agent - will be configured and enabled after spawn (server only)
            _agent = GetComponent<NavMeshAgent>();
            if (_agent != null)
                _agent.enabled = false;
            
            // cache and configure rigidbody for trigger collision detection
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true; // we control movement manually
                _rigidbody.useGravity = false; // we handle gravity ourselves
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }
        
        public override void OnNetworkSpawn()
        {
            if (enemyData != null)
            {
                InitializeFromData();
            }
            
            if (IsServer)
            {
                CurrentHealth.Value = maxHealth;
                CurrentState.Value = EnemyState.Idle;
                spawnPosition = transform.position;
                _isDead = false;
                
                // fire spawn event
                EnemyEvents.InvokeSpawn(transform.position, elementType);
            }
            
            // initialize health bar
            if (healthBar != null)
            {
                healthBar.Initialize(this);
            }

            // let EnemyVisuals know what data to use for initial color + vfx
            if (_visuals != null)
            {
                _visuals.Initialize(enemyData);
            }

            // fire the spawn vfx
            if (_visuals != null)
            {
                _visuals.PlaySpawnVfx(enemyData);
            }

            // initialize minimap trackable for radar display
            InitializeMinimapTrackable();

            // ensure we have a non-trigger collider so projectiles (which use trigger colliders) can detect hits
            Collider anyCollider = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
            if (anyCollider == null)
            {
                // add a capsule collider sized to renderer bounds if possible
                var renderer = GetComponentInChildren<Renderer>();
                CapsuleCollider added = gameObject.AddComponent<CapsuleCollider>();
                added.isTrigger = false;
                if (renderer != null)
                {
                    Bounds b = renderer.bounds;
                    added.center = transform.InverseTransformPoint(b.center);
                    added.height = Mathf.Max(0.5f, b.size.y);
                    added.radius = Mathf.Max(0.25f, Mathf.Max(b.size.x, b.size.z) * 0.5f);
                }
                else
                {
                    added.height = 1.8f;
                    added.radius = 0.5f;
                }
            }

            _groundY = transform.position.y;
            spawnPosition = transform.position;

            // configure and enable the agent on the server after everything else is set up
            if (IsServer)
                ConfigureAgent();
        }
        
        public override void OnNetworkDespawn()
        {
            // stop and disable the agent cleanly
            if (_agent != null)
            {
                _agent.isStopped = true;
                _agent.enabled = false;
            }
        }
        
        protected virtual void InitializeFromData()
        {
            if (enemyData == null) return;
            // always populate runtime stats from the EnemyData asset
            maxHealth = enemyData.maxHealth;
            moveSpeed = enemyData.moveSpeed;
            _effectiveMoveSpeed = moveSpeed;
            rotationSpeed = enemyData.rotationSpeed;
            damage = enemyData.damage;
            attackRange = enemyData.attackRange;
            attackCooldown = enemyData.attackCooldown;
            staggerDuration = enemyData.staggerDuration;
            detectionRange = enemyData.detectionRange;
            leashRange = enemyData.leashRange;
            elementType = enemyData.elementType;
            
            // apply scale if needed
            if (enemyData.scaleMultiplier != 1f)
            {
                transform.localScale = Vector3.one * enemyData.scaleMultiplier;
            }

            // pull physics parameters from data so designers can tune them per enemy type
            if (enemyData.physics != null)
            {
                gravity = enemyData.physics.gravity;
                groundedStickForce = enemyData.physics.groundedStickForce;
                terminalVelocity = enemyData.physics.terminalVelocity;
                groundCheckRadius = enemyData.physics.groundCheckRadius;
                groundCheckOffset = enemyData.physics.groundCheckOffset;
                _groundedConfirmFrames = enemyData.physics.groundedConfirmFrames;
                _groundedLossFrames = enemyData.physics.groundedLossFrames;
                _launchDecayRate = enemyData.physics.launchDecayRate;
            }
        }

        // configures and enables the NavMeshAgent using values from enemyData
        // called after InitializeFromData so all stats are ready
        private void ConfigureAgent()
        {
            if (_agent == null) return;

            // read collider radius so the agent footprint matches the physics shape
            float agentRadius = 0.5f;
            float agentHeight = 2f;
            var cap = GetComponent<CapsuleCollider>();
            if (cap != null)
            {
                agentRadius = cap.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
                agentHeight = cap.height * transform.localScale.y;
            }

            _agent.enabled = true;
            _agent.speed = _effectiveMoveSpeed;
            _agent.angularSpeed = rotationSpeed;
            _agent.radius = agentRadius;
            _agent.height = agentHeight;
            _agent.acceleration = 50f;
            _agent.autoBraking = false;
            _agent.updateUpAxis = false;
            _agent.stoppingDistance = (enemyData != null) ? attackRange * enemyData.stoppingDistanceFactor : attackRange * 0.9f;
            if (enemyData != null)
            {
                _agent.obstacleAvoidanceType = enemyData.obstacleAvoidanceType;
                _agent.avoidancePriority = enemyData.avoidancePriority;
            }

            // check whether we landed on a valid navmesh position after enabling
            _agentOnMesh = _agent.isOnNavMesh;
            if (!_agentOnMesh)
            {
                // try to warp to the nearest navmesh position within 2 units
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
                {
                    _agent.Warp(hit.position);
                    _agentOnMesh = true;
                }
                else
                {
                    // no navmesh nearby - fall back to direct transform movement
                    _agent.enabled = false;
                    _agentOnMesh = false;
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
            trackable.Configure(TrackableType.Enemy, new Color(1f, 0.2f, 0.2f), 1f);
        }
        
        // called by spawner to register itself for death callbacks
        public void SetSpawner(EnemySpawner spawner)
        {
            _spawner = spawner;
        }
        
        // =====================================
        // update loop
        // =====================================
        
        protected virtual void Update()
        {
            if (!IsServer) return;
            if (_isDead) return;

            // manual physics only runs when the agent is off the navmesh (launched / no navmesh baked)
            bool useManualPhysics = !_agentOnMesh;
            if (useManualPhysics)
            {
                UpdateGroundCheck();
                ApplyGravity();
            }

            UpdateTargetTimer();
            UpdateCooldowns();
            HandleStateMachine();

            HandleGrapplePull();
            HandleLaunchVelocity();

            if (useManualPhysics)
            {
                ApplyVerticalDisplacement();

                // once the enemy has landed and their launch velocity has decayed, re-enable the agent
                if (_agentDisabledByLaunch && _isGrounded && _launchHorizontalVelocity.sqrMagnitude < 0.1f)
                    TryReEnableAgent();
            }
        }
        
        private void UpdateTargetTimer()
        {
            _targetUpdateTimer -= Time.deltaTime;
            if (_targetUpdateTimer <= 0f)
            {
                _targetUpdateTimer = targetUpdateInterval;
                UpdateTarget();
            }
        }
        
        private void UpdateCooldowns()
        {
            if (attackCooldownTimer > 0f)
            {
                attackCooldownTimer -= Time.deltaTime;
            }
            
            // update movement modifiers and keep agent speed in sync
            UpdateMovementModifiers();
            SyncAgentSpeed();
        }
        
        private void UpdateMovementModifiers()
        {
            // tick down modifier durations and remove expired ones
            _keysToRemove.Clear();
            foreach (var kvp in _movementModifiers)
            {
                kvp.Value.remainingDuration -= Time.deltaTime;
                if (kvp.Value.remainingDuration <= 0f)
                {
                    _keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in _keysToRemove)
            {
                _movementModifiers.Remove(key);
            }
            
            // recalculate effective move speed (use lowest multiplier if multiple modifiers)
            _effectiveMoveSpeed = moveSpeed;
            float lowestMultiplier = 1f;
            foreach (var modifier in _movementModifiers.Values)
            {
                if (modifier.multiplier < lowestMultiplier)
                {
                    lowestMultiplier = modifier.multiplier;
                }
            }
            _effectiveMoveSpeed = moveSpeed * lowestMultiplier;
        }
        
        // public method for abilities to apply movement speed modifiers
        public void ApplyMovementModifier(float multiplier, float duration, string sourceId)
        {
            if (!IsServer) return;
            
            // add or update the modifier
            if (_movementModifiers.ContainsKey(sourceId))
            {
                // refresh duration and update multiplier
                _movementModifiers[sourceId].multiplier = multiplier;
                _movementModifiers[sourceId].remainingDuration = duration;
            }
            else
            {
                _movementModifiers[sourceId] = new MovementModifier(sourceId, multiplier, duration);
            }
            
            // immediately recalculate effective speed
            UpdateMovementModifiers();
        }

        // keep agent speed in sync with movement modifiers every frame
        private void SyncAgentSpeed()
        {
            if (_agent != null && _agent.enabled && _agentOnMesh)
                _agent.speed = _effectiveMoveSpeed;
        }
        
        // =====================================
        // state machine
        // =====================================
        
        protected virtual void HandleStateMachine()
        {
            // decrement state timer
            if (stateTimer > 0f)
            {
                stateTimer -= Time.deltaTime;
            }
            
            switch (CurrentState.Value)
            {
                case EnemyState.Idle:
                    OnIdleUpdate();
                    break;
                case EnemyState.Chase:
                    OnChaseUpdate();
                    break;
                case EnemyState.Attack:
                    OnAttackUpdate();
                    break;
                case EnemyState.Stagger:
                    OnStaggerUpdate();
                    break;
                case EnemyState.Dead:
                    // do nothing
                    break;
            }
        }
        
        // wander state
        private float _wanderTimer;
        private Vector3 _wanderDestination;
        private bool _hasWanderDestination;

        protected virtual void OnIdleUpdate()
        {
            // check for target using effective target (respects taunt)
            if (GetEffectiveTarget() != null && GetDistanceToTarget() <= detectionRange)
            {
                _hasWanderDestination = false;
                TransitionToChase();
                return;
            }

            // wander if configured to do so
            if (enemyData != null && enemyData.idleBehavior == IdleBehavior.Wander)
            {
                _wanderTimer -= Time.deltaTime;

                if (!_hasWanderDestination || _wanderTimer <= 0f)
                {
                    // pick a new random destination within wanderRadius of spawn
                    Vector2 offset = UnityEngine.Random.insideUnitCircle * enemyData.wanderRadius;
                    _wanderDestination = spawnPosition + new Vector3(offset.x, 0f, offset.y);
                    _hasWanderDestination = true;
                    _wanderTimer = enemyData.wanderInterval;
                }

                MoveTowardPosition(_wanderDestination);
            }
        }
        
        protected virtual void OnChaseUpdate()
        {
            Transform effectiveTarget = GetEffectiveTarget();
            
            if (effectiveTarget == null)
            {
                TransitionToIdle();
                return;
            }
            
            // update currentTarget to effective target for distance calculations
            currentTarget = effectiveTarget;
            float distance = GetDistanceToTarget();
            
            // check leash range
            if (distance > leashRange)
            {
                currentTarget = null;
                currentTargetController = null;
                TransitionToIdle();
                return;
            }
            
            // check attack range
            if (distance <= attackRange && attackCooldownTimer <= 0f)
            {
                TransitionToAttack();
                return;
            }
            
            // move toward target
            // when on the navmesh the agent handles rotation via angularSpeed - no need to call RotateTowardTarget
            if (!_agentOnMesh)
                RotateTowardTarget();
            MoveTowardTarget();
        }
        
        protected virtual void OnAttackUpdate()
        {
            // deal damage after the configured delay
            if (!_hasDealtDamageThisAttack)
            {
                _damageDelayTimer -= Time.deltaTime;
                if (_damageDelayTimer <= 0f)
                {
                    DealDamageToTarget();
                    _hasDealtDamageThisAttack = true;
                }
            }

            if (stateTimer <= 0f)
            {
                // attack finished, go back to chase or idle
                if (currentTarget != null)
                {
                    TransitionToChase();
                }
                else
                {
                    TransitionToIdle();
                }
            }
        }

        // deals damage to the current target - uses _currentAttack multiplier if set
        protected virtual void DealDamageToTarget()
        {
if (currentTargetController == null) return;

            float range = _currentAttack != null && _currentAttack.attackRangeOverride > 0f
                ? _currentAttack.attackRangeOverride
                : attackRange;

            if (!IsTargetInRange(range * 1.2f)) return;

            float multiplier = _currentAttack != null ? _currentAttack.damageMultiplier : 1f;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * multiplier * DamageOutputMultiplier));
            currentTargetController.TakeDamage(finalDamage);

            OnAttackHit(currentTargetController, finalDamage);
        }
        
        protected virtual void OnStaggerUpdate()
        {
            if (stateTimer <= 0f)
            {
                // stagger finished
                if (currentTarget != null)
                {
                    TransitionToChase();
                }
                else
                {
                    TransitionToIdle();
                }
            }
        }
        
        // =====================================
        // state transitions
        // =====================================
        
        protected virtual void TransitionToIdle()
        {
            CurrentState.Value = EnemyState.Idle;
            stateTimer = 0f;
            StopAgent();
        }
        
        protected virtual void TransitionToChase()
        {
            CurrentState.Value = EnemyState.Chase;
            stateTimer = 0f;
            if (_agent != null && _agent.enabled && _agentOnMesh)
                _agent.isStopped = false;
        }
        
        protected virtual void TransitionToAttack()
        {
            // pick an attack from the data asset using weighted random selection
            _currentAttack = SelectAttack();

            CurrentState.Value = EnemyState.Attack;
            attackCooldownTimer = attackCooldown;

            // set up base damage timer
            _hasDealtDamageThisAttack = false;
            _damageDelayTimer = _currentAttack != null ? _currentAttack.damageDelay : 0.25f;

            // set the state timer to the attack's configured duration
            stateTimer = _currentAttack != null ? _currentAttack.attackDuration : 0.5f;

            StopAgent();
            ExecuteAttack();
        }

        // weighted random selection from EnemyData.attacks
        // falls back to null if no attacks are configured
        private EnemyAttackData SelectAttack()
        {
            if (enemyData == null || enemyData.attacks == null || enemyData.attacks.Length == 0)
                return null;

            int totalWeight = 0;
            foreach (var a in enemyData.attacks)
            {
                if (a != null) totalWeight += Mathf.Max(1, a.selectionWeight);
            }

            int roll = UnityEngine.Random.Range(0, totalWeight);
            int accumulated = 0;
            foreach (var a in enemyData.attacks)
            {
                if (a == null) continue;
                accumulated += Mathf.Max(1, a.selectionWeight);
                if (roll < accumulated) return a;
            }

            return enemyData.attacks[0];
        }
        
        protected virtual void TransitionToStagger()
        {
            CurrentState.Value = EnemyState.Stagger;
            stateTimer = staggerDuration;
            StopAgent();
        }

        // stop the agent in place without disabling it
        // stays on the navmesh so it still participates in avoidance, just doesn't move
        private void StopAgent()
        {
            if (_agent != null && _agent.enabled && _agentOnMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
        }
        
        // =====================================
        // abstract methods for subclasses
        // =====================================
        
        // called when an attack is triggered - subclasses fire events and vfx here
        // stateTimer is already set to attackDuration before this is called
        protected virtual void ExecuteAttack() { }

        // called when base damage is dealt - virtual so subclasses can react (e.g. vfx)
        protected virtual void OnAttackHit(PlayerController target, int finalDamage) { }
        
        // =====================================
        // targeting
        // =====================================
        
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
        // movement
        // =====================================
        
        protected virtual void RotateTowardTarget()
        {
            if (currentTarget == null) return;
            
            Vector3 direction = GetDirectionToTarget();
            if (direction == Vector3.zero) return;
            
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime / 360f
            );
        }
        
        protected virtual void MoveTowardTarget()
        {
            if (_isBeingGrappled) return;
            if (currentTarget == null) return;

            MoveTowardPosition(currentTarget.position);
        }

        // move toward any world-space position
        // uses the NavMeshAgent when on the mesh, falls back to direct transform move when airborne
        protected void MoveTowardPosition(Vector3 worldPos)
        {
            if (_isBeingGrappled) return;

            if (_agentOnMesh && _agent != null && _agent.enabled)
            {
                _agent.isStopped = false;
                _agent.SetDestination(worldPos);
                return;
            }

            // fallback: direct transform movement when off the navmesh
            Vector3 direction = worldPos - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            direction.Normalize();
            transform.position += direction * _effectiveMoveSpeed * Time.deltaTime;
        }
        
        // =====================================
        // helpers
        // =====================================
        
        protected float GetDistanceToTarget()
        {
            if (currentTarget == null) return float.MaxValue;
            return Vector3.Distance(transform.position, currentTarget.position);
        }
        
        protected Vector3 GetDirectionToTarget()
        {
            if (currentTarget == null) return Vector3.zero;
            
            Vector3 direction = currentTarget.position - transform.position;
            direction.y = 0f;
            return direction.normalized;
        }
        
        // get effective target (respects taunt from abilities)
        // this method can be overridden by concrete enemies or used by the state machine
        protected virtual Transform GetEffectiveTarget()
        {
            // by default return currentTarget
            // BasicEnemy (ICanBeTaunted) will override to check taunt state
            return currentTarget;
        }

        // ground check helper (mirrors PlayerController's ground check)
        protected void UpdateGroundCheck()
        {
            Vector3 checkPos = transform.position + groundCheckOffset;
            bool raw = Physics.CheckSphere(checkPos, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);

            if (raw)
            {
                _groundedTrueCounter++;
                _groundedFalseCounter = 0;
                if (_groundedTrueCounter >= _groundedConfirmFrames)
                {
                    _isGrounded = true;
                }
            }
            else
            {
                _groundedFalseCounter++;
                _groundedTrueCounter = 0;
                if (_groundedFalseCounter >= _groundedLossFrames)
                {
                    _isGrounded = false;
                }
            }
        }

        // update vertical velocity based on grounded state and gravity
        protected void ApplyGravity()
        {
            if (_isGrounded)
            {
                // don't stomp an upward velocity - a launch may have just set it positive
                if (_verticalVelocity <= 0f)
                    _verticalVelocity = groundedStickForce;
                return;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
                if (_verticalVelocity < terminalVelocity) _verticalVelocity = terminalVelocity;
            }
        }

        // apply vertical displacement after other movement so gravity always affects the transform
        protected void ApplyVerticalDisplacement()
        {
            float deltaY = _verticalVelocity * Time.deltaTime;

            // if currently considered grounded, avoid large downward raycasts which can cause toggling
            if (_isGrounded)
            {
                // upward launch velocity - apply it even when grounded so the enemy actually lifts off
                if (_verticalVelocity > 0f)
                {
                    transform.position += Vector3.up * deltaY;
                    return;
                }

                // tiny correction to avoid being slightly below ground: do a short raycast
                Collider colCheck = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
                if (colCheck != null)
                {
                    Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
                    RaycastHit hit;
                    if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 0.5f, groundLayers, QueryTriggerInteraction.Ignore))
                    {
                        float colliderBottomY = colCheck.bounds.min.y;
                        float diff = hit.point.y - colliderBottomY;
                        if (diff > 0.001f)
                        {
                            transform.position = transform.position + Vector3.up * diff;
                        }
                    }
                }

                // do not apply downward movement while grounded
                return;
            }

            // if moving downward, raycast to prevent tunneling through ground
            if (deltaY < 0f)
            {
                Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
                float castDistance = Mathf.Abs(deltaY) + 0.1f + groundCheckRadius;
                RaycastHit hit;
                if (Physics.Raycast(rayOrigin, Vector3.down, out hit, castDistance, groundLayers, QueryTriggerInteraction.Ignore))
                {
                    // land on ground - align collider bottom with hit point to avoid half-embedded visuals
                    Collider col = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
                    if (col != null)
                    {
                        // world-space bottom Y of collider
                        float colliderBottomY = col.bounds.min.y;
                        float shift = hit.point.y - colliderBottomY;
                        transform.position = transform.position + Vector3.up * shift;
                    }
                    else
                    {
                        // no collider found, fall back to placing root at hit.y
                        transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
                    }

                    _verticalVelocity = groundedStickForce;
                    _isGrounded = true;
                    return;
                }
                else
                {
                    // no ground detected within cast distance, apply full movement
                    transform.position += Vector3.up * deltaY;
                    _isGrounded = false;
                }
            }
            else if (deltaY > 0f)
            {
                // moving up
                transform.position += Vector3.up * deltaY;
            }
        }
        
        protected bool IsTargetInRange(float range)
        {
            return GetDistanceToTarget() <= range;
        }
        
        // =====================================
        // damage
        // =====================================

        // tracks who last damaged this enemy (for kill attribution)
        public ulong LastDamagerClientId { get; set; }

        // fired on server when this enemy dies, reports who killed it
        public static event Action<ulong, Vector3, GameObject> OnEnemyKilledBy;
        
        public virtual void TakeDamage(int damageAmount)
        {
            if (!IsServer) return;
            if (_isDead) return;
            
            CurrentHealth.Value -= damageAmount;
            
            // fire hurt event
            NotifyHurtClientRpc(transform.position, damageAmount);
            
            // trigger stagger if not attacking
            if (CurrentState.Value != EnemyState.Attack)
            {
                TransitionToStagger();
            }
            
            if (CurrentHealth.Value <= 0)
            {
                Die();
            }
        }
        
        // apply a stun from an ability (overrides current state)
        public virtual void ApplyStun(float stunDuration)
        {
            if (!IsServer) return;
            if (_isDead) return;
            
            CurrentState.Value = EnemyState.Stagger;
            stateTimer = stunDuration;
        }
        
        // check if this enemy is dead
        public bool IsDead => _isDead;
        
        // apply knockback to the enemy (used by grappling hook ability)
        public virtual void ApplyKnockback(Vector3 knockbackForce)
        {
            if (!IsServer) return;
            
            if (TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearVelocity += knockbackForce;
            }
        }
        
        // =====================================
        // iweakpointhost
        // =====================================

        // called by weak points after applying their damage multiplier
        public void TakeDamageFromWeakPoint(int damage, ulong attackerClientId)
        {
            if (!IsServer) return;
            if (_isDead) return;

            LastDamagerClientId = attackerClientId;
            TakeDamage(damage);
        }

        // called when one of this enemy's weak points breaks
        public void OnWeakPointBroken(WeakPoint weakPoint, ulong attackerClientId)
        {
            // base implementation does nothing — subclasses or break effects handle stun etc
        }

        // resets all child weak points to full health (called on round transitions)
        public void ResetAllWeakPoints()
        {
            var weakPoints = GetComponentsInChildren<WeakPoint>(true);
            for (int i = 0; i < weakPoints.Length; i++)
            {
                weakPoints[i].ResetWeakPoint();
            }
        }
        
        // horizontal launch velocity applied by abilities (e.g. fighter q slam)
        private Vector3 _launchHorizontalVelocity = Vector3.zero;
        private float _launchDecayRate = 12f;
        
        // launch the enemy with a velocity impulse (server-only)
        // horizontal component decays over time; vertical overrides gravity
        public void ApplyLaunch(Vector3 velocity)
        {
            if (!IsServer) return;
            if (_isDead) return;

            // disable the agent so manual gravity and horizontal velocity take over
            if (_agent != null && _agent.enabled)
            {
                _agent.isStopped = true;
                _agent.enabled = false;
                _agentOnMesh = false;
                _agentDisabledByLaunch = true;
            }
            
            _launchHorizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            
            if (velocity.y > 0f)
            {
                _verticalVelocity = velocity.y;
                _isGrounded = false;
                _groundedTrueCounter = 0;
                _groundedFalseCounter = 0;
            }
            
            // interrupt chasing/attacking
            TransitionToStagger();
        }

        // called after landing from a launch - warps the agent back onto the navmesh and re-enables it
        private void TryReEnableAgent()
        {
            if (_agent == null) return;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
            {
                _agent.enabled = true;
                _agent.Warp(hit.position);
                _agentOnMesh = true;
                _agentDisabledByLaunch = false;

                if (CurrentState.Value == EnemyState.Chase)
                    _agent.isStopped = false;
            }
            // no nearby navmesh: stays in manual physics mode and tries again next frame
        }
        
        private void HandleLaunchVelocity()
        {
            if (_launchHorizontalVelocity.sqrMagnitude < 0.01f) return;
            
            transform.position += _launchHorizontalVelocity * Time.deltaTime;
            _launchHorizontalVelocity = Vector3.MoveTowards(_launchHorizontalVelocity, Vector3.zero, _launchDecayRate * Time.deltaTime);
        }
        
        // grapple state for continuous pulling (used by fighter e grappling hook)
        private bool _isBeingGrappled;
        private Transform _grappleTargetTransform; // track the player transform instead of fixed position
        private float _grapplePullSpeed;
        
        // start continuous grapple pull toward a target transform
        public void StartGrapple(Transform targetTransform, float pullSpeed)
        {
            if (!IsServer) return;

            // stop the agent so grapple movement has full control
            // we keep it enabled (not disabled) so it stays on the navmesh during the pull
            StopAgent();
            
            _isBeingGrappled = true;
            _grappleTargetTransform = targetTransform;
            _grapplePullSpeed = pullSpeed;
            
            // Debug.Log($"EnemyBase: {gameObject.name} starting grapple to {targetTransform.gameObject.name}");
        }
        
        // stop grapple pull
        public void StopGrapple()
        {
            if (!IsServer) return;
            
            if (_isBeingGrappled)
            {
                // Debug.Log($"EnemyBase: {gameObject.name} stopping grapple");
            }
            
            _isBeingGrappled = false;
            _grappleTargetTransform = null;

            // warp the agent to our new position and resume movement if still chasing
            if (_agent != null && _agent.enabled && _agentOnMesh)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
                    _agent.Warp(hit.position);

                if (CurrentState.Value == EnemyState.Chase)
                    _agent.isStopped = false;
            }
        }
        
        // check if currently being grappled
        public bool IsBeingGrappled => _isBeingGrappled;
        
        // handle continuous grapple pull movement
        private void HandleGrapplePull()
        {
            if (!_isBeingGrappled) return;
            
            // check if target is still valid
            if (_grappleTargetTransform == null)
            {
                // Debug.Log($"EnemyBase: {gameObject.name} grapple target destroyed, stopping grapple");
                StopGrapple();
                return;
            }
            
            Vector3 currentPos = transform.position;
            Vector3 targetPos = _grappleTargetTransform.position; // get current player position each frame
            float distanceToTarget = Vector3.Distance(currentPos, targetPos);
            Vector3 pullDirection = (targetPos - currentPos).normalized;
            float pullAmount = _grapplePullSpeed * Time.deltaTime;
            
            // Debug.Log($"EnemyBase: {gameObject.name} grapple pull - distance: {distanceToTarget:F2}, pullAmount: {pullAmount:F2}, target: {_grappleTargetTransform.gameObject.name}");
            
            // check if we've reached the target (within 1.5 units)
            if (distanceToTarget <= 1.5f)
            {
                // Debug.Log($"EnemyBase: {gameObject.name} reached grapple target (distance: {distanceToTarget:F2}), stopping grapple");
                StopGrapple();
                return;
            }
            
            // for grapple, use direct transform movement for maximum speed and reliability
            // physics-based movement is too slow and gets interrupted
            Vector3 newPosition = transform.position + pullDirection * pullAmount;
            transform.position = newPosition;
            
            // Debug.Log($"EnemyBase: Moved from {currentPos} to {newPosition}");
        }
        
        // detect collisions during grapple
        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer) return;
            if (!_isBeingGrappled) return;
            
            // Debug.Log($"EnemyBase: {gameObject.name} OnCollisionEnter with {collision.gameObject.name} while grappling");
            
            // only stop grapple if we hit the player (the grapple target)
            var player = collision.gameObject.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                // Debug.Log($"EnemyBase: {gameObject.name} collided with player, stopping grapple");
                StopGrapple();
                return;
            }
            
            // ignore all other collisions (terrain, other enemies, etc.) - let the grapple continue
            // Debug.Log($"EnemyBase: {gameObject.name} hit {collision.gameObject.name} but it's not the player, continuing grapple");
        }
        
        [ClientRpc]
        private void NotifyHurtClientRpc(Vector3 position, int damageAmount)
        {
            EnemyEvents.InvokeHurt(position, damageAmount, elementType);
        }

        // no longer needed - EnemyVisuals subscribes to CurrentHealth directly
        protected virtual void OnHealthChanged(int oldHealth, int newHealth) { }
        
        // =====================================
        // death
        // =====================================
        
        protected virtual void Die()
        {
            if (_isDead) return;
            _isDead = true;
            
            CurrentState.Value = EnemyState.Dead;

            // shut down the agent so it stops affecting avoidance for surviving enemies
            if (_agent != null)
            {
                _agent.isStopped = true;
                _agent.enabled = false;
                _agentOnMesh = false;
            }
            
            // fire kill attribution event for item behaviours (server only)
            OnEnemyKilledBy?.Invoke(LastDamagerClientId, transform.position, gameObject);
            
            // fire death event on all clients
            NotifyDeathClientRpc(transform.position);
            
            // notify spawner
            if (_spawner != null)
            {
                _spawner.OnEnemyDied(this);
            }
            
            // stub for power-up manager integration
            // PowerUpManager.Instance?.OnEnemyDied(elementType, transform.position);
            
            // fire death vfx before despawning
            NotifyDeathVfxClientRpc();

            // wait for death animation to finish before despawning
            float linger = enemyData != null ? enemyData.deathLingerDuration : 1.5f;
            Invoke(nameof(DespawnEnemy), linger);
        }
        
        [ClientRpc]
        private void NotifyDeathClientRpc(Vector3 position)
        {
            EnemyEvents.InvokeDeath(position, elementType);
        }

        [ClientRpc]
        private void NotifyDeathVfxClientRpc()
        {
            if (_visuals != null)
                _visuals.PlayDeathVfx(enemyData);
        }
        
        private void DespawnEnemy()
        {
            if (!IsServer) return;
            
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }
        
        // =====================================
        // public accessors
        // =====================================
        
        public int MaxHealth => maxHealth;
        public ElementType Element => elementType;
        public EnemyData Data => enemyData;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (enemyData == null)
            {
                Debug.LogWarning($"{name}: EnemyData not assigned. assign an EnemyData asset to this prefab or scene object.", this);
            }
        }
#endif
        
        // =====================================
        // gizmos
        // =====================================
        
        protected virtual void OnDrawGizmosSelected()
        {
            Color gizmoColor = enemyData != null ? enemyData.gizmoColor : Color.red;
            
            // detection range
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.2f);
            Gizmos.DrawWireSphere(transform.position, enemyData != null ? enemyData.detectionRange : detectionRange);
            
            // attack range
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, enemyData != null ? enemyData.attackRange : attackRange);
            
            // leash range
            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, enemyData != null ? enemyData.leashRange : leashRange);

            // ground check gizmo (can be toggled on derived components)
            if (showGroundCheckGizmo)
            {
                Vector3 checkPos = transform.position + groundCheckOffset;
                bool grounded = false;
                try
                {
                    grounded = Physics.CheckSphere(checkPos, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);
                }
                catch { }

                Gizmos.color = grounded ? new Color(0f, 1f, 0f, 0.4f) : new Color(1f, 0f, 0f, 0.4f);
                Gizmos.DrawWireSphere(checkPos, groundCheckRadius);
            }
        }
    }
}
