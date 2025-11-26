using UnityEngine;
using Unity.Netcode;
using Category5.Core;

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

        protected NetworkVariable<BossState> currentState = new NetworkVariable<BossState>(BossState.Idle);
        protected float stateTimer;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                CurrentHealth.Value = maxHealth;
                currentState.Value = BossState.Idle;
                stateTimer = idleDuration;
            }

            CurrentHealth.OnValueChanged += OnHealthChanged;
            
            // try to register with ui, it may not be ready yet on scene load
            TryRegisterWithUI();
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
        }

        protected virtual void StartAttack()
        {
            currentState.Value = BossState.Attack;
            // duration depends on the specific attack
            stateTimer = 1f; 
            ExecuteAttack();
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
            Debug.Log("boss died");
            GetComponent<NetworkObject>().Despawn();
        }
    }
}
