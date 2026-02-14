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
    public abstract class BossBase : NetworkBehaviour, IDamageable
    {
        [Header("stats")]
        [SerializeField] protected int maxHealth = 500;
        public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>();

        public int MaxHealth => maxHealth;

        [Header("state timings")]
        [SerializeField] protected float idleDuration = 2f;
        [SerializeField] protected float telegraphDuration = 1.5f;
        [SerializeField] protected float cooldownDuration = 1f;
        
        [Header("targeting")]
        [SerializeField] protected float targetUpdateInterval = 0.5f;
        private float _targetUpdateTimer;
        protected Transform currentTarget;
        protected PlayerController currentTargetController;
        
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
        
        // optional character controller for better collision handling
        protected CharacterController characterController;
        
        [Header("vfx/feedback")]
        [Tooltip("Default attack type for vfx hooks, can be overridden by subclass")]
        [SerializeField] protected BossAttackType defaultAttackType = BossAttackType.Slam;

        protected NetworkVariable<BossState> currentState = new NetworkVariable<BossState>(BossState.Idle);
        protected float stateTimer;
        
        // current attack type for vfx hooks
        protected BossAttackType currentAttackType = BossAttackType.None;
        
        // flag to prevent multiple death triggers
        private bool _isDead = false;
        private bool _isHidden = false;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                CurrentHealth.Value = maxHealth;
                currentState.Value = BossState.Idle;
                stateTimer = idleDuration;
                _isDead = false;
                _targetUpdateTimer = 0f;
            }
            
            // cache character controller if present
            characterController = GetComponent<CharacterController>();

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

            UpdateTargetTimer();
            HandleStateMachine();
        }

        private void HandleStateMachine()
        {
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
            
            Vector3 movement = direction * moveSpeed * Time.deltaTime;
            
            if (characterController != null)
            {
                // use character controller for better collision handling
                characterController.Move(movement);
            }
            else
            {
                // simple transform-based movement
                transform.position += movement;
            }
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

            // disable movement controller while hidden
            if (characterController != null)
            {
                characterController.enabled = !hidden;
            }
        }
        
        // called by PowerUpManager to reset boss for new round with scaled hp
        public virtual void ResetBoss(int newMaxHealth, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            if (!IsServer) return;
            
            // Debug.Log($"BossBase: Resetting boss with {newMaxHealth} HP at position {spawnPosition}");
            
            // teleport boss to spawn position
            if (characterController != null)
            {
                characterController.enabled = false;
                transform.position = spawnPosition;
                transform.rotation = spawnRotation;
                characterController.enabled = true;
            }
            else
            {
                transform.position = spawnPosition;
                transform.rotation = spawnRotation;
            }
            
            maxHealth = newMaxHealth;
            CurrentHealth.Value = maxHealth;
            currentState.Value = BossState.Idle;
            stateTimer = idleDuration;
            _isDead = false;
            _isHidden = false;
            
            // show boss again and notify clients about the reset
            ShowBossClientRpc();
            ResetBossClientRpc(newMaxHealth);
            
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
        }
    }
}
