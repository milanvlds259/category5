using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Audio;
using Category5.Player;
using Category5.UI;

namespace Category5.Enemies
{
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
    public abstract class EnemyBase : NetworkBehaviour, IDamageable
    {
        [Header("enemy data")]
        [SerializeField] protected EnemyData enemyData;
        
        [Header("components")]
        [SerializeField] protected EnemyHealthBar healthBar;
        
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

        [Header("timing & targeting")]
        [SerializeField] protected float targetUpdateInterval = 0.5f;
        
        [Header("ground check")]
        // make these public so derived concrete enemy components can edit them in the inspector
        public float groundCheckRadius = 0.2f;
        public Vector3 groundCheckOffset = new Vector3(0f, 0.1f, 0f);
        public LayerMask groundLayers = 1; // default to Default layer
        public bool showGroundCheckGizmo = true;
        protected bool _isGrounded = false;

        [Header("ground check stability")]
        [SerializeField] protected int groundedConfirmFrames = 3;
        [SerializeField] protected int groundedLossFrames = 3;
        private int _groundedTrueCounter = 0;
        private int _groundedFalseCounter = 0;

        [Header("gravity")]
        // gravity parameters (public so concrete enemy components can tune them)
        public float gravity = -20f;
        public float groundedStickForce = -2f; // small downward force to keep grounded
        public float terminalVelocity = -50f;
        protected float _verticalVelocity = 0f;
        
        // targeting
        protected Transform currentTarget;
        protected PlayerController currentTargetController;
        private float _targetUpdateTimer;
        
        // state timers
        protected float stateTimer;
        protected float attackCooldownTimer;
        
        // movement
        protected CharacterController characterController;
        protected Vector3 spawnPosition;
        protected float _groundY;
        
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
            // try to cache a character controller if present
            characterController = GetComponent<CharacterController>();
            
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
            
            CurrentHealth.OnValueChanged += OnHealthChanged;
            
            // initialize health bar
            if (healthBar != null)
            {
                healthBar.Initialize(this);
            }

            // remove character controller at runtime so movement and physics rely on transform and regular colliders
            // this avoids conflicts between CharacterController movement and NetworkTransform interpolation
            if (characterController != null)
            {
                Destroy(characterController);
                characterController = null;
            }

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

            // do not perform a raycast ground snap here; rely on the ground check sphere for grounding
            _groundY = transform.position.y;
            spawnPosition = transform.position;
        }
        
        public override void OnNetworkDespawn()
        {
            CurrentHealth.OnValueChanged -= OnHealthChanged;
        }
        
        protected virtual void InitializeFromData()
        {
            if (enemyData == null) return;
            // always populate runtime stats from the EnemyData asset
            maxHealth = enemyData.maxHealth;
            moveSpeed = enemyData.moveSpeed;
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
            // update ground check so movement and gizmos can use current grounded state
            UpdateGroundCheck();

            // update vertical velocity from gravity
            ApplyGravity();

            UpdateTargetTimer();
            UpdateCooldowns();
            HandleStateMachine();

            // after state updates apply vertical displacement so enemies fall when not grounded
            ApplyVerticalDisplacement();
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
        
        protected virtual void OnIdleUpdate()
        {
            // check for target
            if (currentTarget != null && GetDistanceToTarget() <= detectionRange)
            {
                TransitionToChase();
            }
        }
        
        protected virtual void OnChaseUpdate()
        {
            if (currentTarget == null)
            {
                TransitionToIdle();
                return;
            }
            
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
            RotateTowardTarget();
            MoveTowardTarget();
        }
        
        protected virtual void OnAttackUpdate()
        {
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
        }
        
        protected virtual void TransitionToChase()
        {
            CurrentState.Value = EnemyState.Chase;
            stateTimer = 0f;
        }
        
        protected virtual void TransitionToAttack()
        {
            CurrentState.Value = EnemyState.Attack;
            attackCooldownTimer = attackCooldown;
            ExecuteAttack();
        }
        
        protected virtual void TransitionToStagger()
        {
            CurrentState.Value = EnemyState.Stagger;
            stateTimer = staggerDuration;
        }
        
        // =====================================
        // abstract methods for subclasses
        // =====================================
        
        // executes the enemy's attack, sets stateTimer for attack duration
        protected abstract void ExecuteAttack();
        
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
            if (currentTarget == null) return;
            
            Vector3 direction = GetDirectionToTarget();
            if (direction == Vector3.zero) return;
            
            Vector3 movement = direction * moveSpeed * Time.deltaTime;
            
            if (characterController != null)
            {
                // add gravity
                movement.y = -9.81f * Time.deltaTime;
                characterController.Move(movement);
            }
            else
            {
                transform.position += movement;
            }
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

        // ground check helper (mirrors PlayerController's ground check)
        protected void UpdateGroundCheck()
        {
            Vector3 checkPos = transform.position + groundCheckOffset;
            bool raw = Physics.CheckSphere(checkPos, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);

            if (raw)
            {
                _groundedTrueCounter++;
                _groundedFalseCounter = 0;
                if (_groundedTrueCounter >= groundedConfirmFrames)
                {
                    _isGrounded = true;
                }
            }
            else
            {
                _groundedFalseCounter++;
                _groundedTrueCounter = 0;
                if (_groundedFalseCounter >= groundedLossFrames)
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
                // when grounded, prevent accumulation of downward velocity
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

            if (characterController != null)
            {
                // CharacterController handles collisions itself
                characterController.Move(Vector3.up * deltaY);
                return;
            }

            // if currently considered grounded, avoid large downward raycasts which can cause toggling
            if (_isGrounded)
            {
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
        
        [ClientRpc]
        private void NotifyHurtClientRpc(Vector3 position, int damageAmount)
        {
            EnemyEvents.InvokeHurt(position, damageAmount, elementType);
        }
        
        protected virtual void OnHealthChanged(int oldHealth, int newHealth)
        {
            // subclasses can override for visual feedback
        }
        
        // =====================================
        // death
        // =====================================
        
        protected virtual void Die()
        {
            if (_isDead) return;
            _isDead = true;
            
            CurrentState.Value = EnemyState.Dead;
            
            // fire death event on all clients
            NotifyDeathClientRpc(transform.position);
            
            // notify spawner
            if (_spawner != null)
            {
                _spawner.OnEnemyDied(this);
            }
            
            // stub for power-up manager integration
            // PowerUpManager.Instance?.OnEnemyDied(elementType, transform.position);
            
            // despawn after a short delay for death effects
            Invoke(nameof(DespawnEnemy), 0.1f);
        }
        
        [ClientRpc]
        private void NotifyDeathClientRpc(Vector3 position)
        {
            EnemyEvents.InvokeDeath(position, elementType);
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
