using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.PowerUps;

namespace Category5.Boss
{
    public enum BossState
    {
        Idle,
        Telegraph,
        Attack,
        Cooldown
    }

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
        
        [Header("vfx/feedback")]
        [Tooltip("Default attack type for vfx hooks, can be overridden by subclass")]
        [SerializeField] protected BossAttackType defaultAttackType = BossAttackType.Slam;

        protected NetworkVariable<BossState> currentState = new NetworkVariable<BossState>(BossState.Idle);
        protected float stateTimer;
        
        // current attack type for vfx hooks
        protected BossAttackType currentAttackType = BossAttackType.None;
        
        // flag to prevent multiple death triggers
        private bool _isDead = false;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                CurrentHealth.Value = maxHealth;
                currentState.Value = BossState.Idle;
                stateTimer = idleDuration;
                _isDead = false;
            }

            CurrentHealth.OnValueChanged += OnHealthChanged;
            
            // try to register with ui, it may not be ready yet on scene load
            TryRegisterWithUI();
            
            // register with power-up manager
            if (IsServer && PowerUpManager.Instance != null)
            {
                PowerUpManager.Instance.RegisterBoss(this);
            }
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
                Debug.Log("BossBase: UIManager not ready, waiting for it to register us");
            }
        }

        public override void OnNetworkDespawn()
        {
            CurrentHealth.OnValueChanged -= OnHealthChanged;
        }

        protected virtual void Update()
        {
            if (!IsServer) return;

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
                    OnIdleUpdate();
                    break;
                case BossState.Telegraph:
                    OnTelegraphUpdate();
                    break;
                case BossState.Attack:
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

        // idamageable implementation
        public void TakeDamage(int damage)
        {
            if (!IsServer) return;

            CurrentHealth.Value -= damage;
            Debug.Log($"boss took {damage} damage. health: {CurrentHealth.Value}");

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
            
            Debug.Log("BossBase: Boss died!");
            
            // notify power-up manager instead of despawning immediately
            if (PowerUpManager.Instance != null)
            {
                Debug.Log("BossBase: Notifying PowerUpManager of boss death");
                
                // hide boss visually during power-up selection
                HideBossClientRpc();
                
                PowerUpManager.Instance.OnBossDied();
                // boss will be reset by PowerUpManager.RespawnBoss()
            }
            else
            {
                Debug.LogWarning("BossBase: PowerUpManager.Instance is null! Make sure PowerUpManager is in the scene.");
                // fallback if no power-up manager - just despawn
                GetComponent<NetworkObject>().Despawn();
            }
        }
        
        [ClientRpc]
        private void HideBossClientRpc()
        {
            // hide the boss visually during power-up selection
            gameObject.SetActive(false);
        }
        
        [ClientRpc]
        private void ShowBossClientRpc()
        {
            // show the boss when respawning
            gameObject.SetActive(true);
        }
        
        // called by PowerUpManager to reset boss for new round with scaled hp
        public virtual void ResetBoss(int newMaxHealth)
        {
            if (!IsServer) return;
            
            Debug.Log($"BossBase: Resetting boss with {newMaxHealth} HP");
            
            maxHealth = newMaxHealth;
            CurrentHealth.Value = maxHealth;
            currentState.Value = BossState.Idle;
            stateTimer = idleDuration;
            _isDead = false;
            
            // show boss again and notify clients about the reset
            ShowBossClientRpc();
            ResetBossClientRpc(newMaxHealth);
            
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
    }
}
