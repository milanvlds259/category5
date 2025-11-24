using UnityEngine;
using Unity.Netcode;

namespace Category5.Boss
{
    public class TestBoss : BossBase
    {
        [Header("visuals")]
        [SerializeField] private Renderer meshRenderer;
        [SerializeField] private Color idleColor = Color.gray;
        [SerializeField] private Color telegraphColor = Color.yellow;
        [SerializeField] private Color attackColor = Color.red;
        [SerializeField] private Color cooldownColor = Color.blue;

        [Header("combat")]
        [SerializeField] private float attackRadius = 3f;
        [SerializeField] private Vector3 attackOffset = Vector3.zero;
        [SerializeField] private int attackDamage = 10;

        private void Awake()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<Renderer>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            currentState.OnValueChanged += OnStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            currentState.OnValueChanged -= OnStateChanged;
        }

        protected override void SelectNextAttack()
        {
            // for now we just have one attack
            // later we can pick random attacks here
            Debug.Log("test boss selected attack 1");
        }

        protected override void ExecuteAttack()
        {
            Debug.Log("test boss executing attack");
            // simulate attack duration
            stateTimer = 1.0f;
            
            // here we would spawn hitboxes or projectiles
            // for test purposes lets do a simple overlap sphere around the boss (so kinda like a swipe attack thing)
            if (IsServer)
            {
                // calculate attack center in world space (relative to boss rotation)
                Vector3 attackCenter = transform.position + transform.TransformDirection(attackOffset);
                
                Collider[] hits = Physics.OverlapSphere(attackCenter, attackRadius);
                foreach (var hit in hits)
                {
                    if (hit.TryGetComponent<Core.IDamageable>(out var target) && hit.gameObject != gameObject)
                    {
                        target.TakeDamage(attackDamage);
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 attackCenter = transform.position + transform.TransformDirection(attackOffset);
            Gizmos.DrawWireSphere(attackCenter, attackRadius);
        }

        private void OnStateChanged(BossState oldState, BossState newState)
        {
            UpdateVisuals(newState);
        }

        private void UpdateVisuals(BossState state)
        {
            if (meshRenderer == null) return;

            switch (state)
            {
                case BossState.Idle:
                    meshRenderer.material.color = idleColor;
                    break;
                case BossState.Telegraph:
                    meshRenderer.material.color = telegraphColor;
                    break;
                case BossState.Attack:
                    meshRenderer.material.color = attackColor;
                    break;
                case BossState.Cooldown:
                    meshRenderer.material.color = cooldownColor;
                    break;
            }
        }
    }
}
