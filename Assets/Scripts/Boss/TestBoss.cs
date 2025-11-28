using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

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
        [SerializeField] private Vector3 attackOffset = new Vector3(0f, 0f, 2f);
        [SerializeField] private int attackDamage = 10;
        
        [Header("movement tuning")]
        [SerializeField] private float lungeSpeed = 8f;
        [SerializeField] private float lungeDistance = 2f;
        private bool _isLunging = false;
        private Vector3 _lungeDirection;
        private float _lungeDistanceTraveled;
        
        // track which targets have been hit this attack to prevent multi-hits
        private HashSet<GameObject> _hitTargetsThisAttack = new HashSet<GameObject>();

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
            
            // set up lunge toward target
            _isLunging = false;
            _lungeDistanceTraveled = 0f;
            _lungeDirection = GetDirectionToTarget();
        }

        protected override void ExecuteAttack()
        {
            Debug.Log("test boss executing attack");
            // simulate attack duration
            stateTimer = 1.0f;
            
            // clear hit targets for new attack
            _hitTargetsThisAttack.Clear();
            
            // start lunging toward target during attack
            _isLunging = true;
            _lungeDistanceTraveled = 0f;
        }
        
        protected override void OnAttackUpdate()
        {
            // lunge forward during attack
            if (_isLunging && _lungeDistanceTraveled < lungeDistance)
            {
                float frameDistance = lungeSpeed * Time.deltaTime;
                ApplyMovement(_lungeDirection * (lungeSpeed / moveSpeed)); // scale to use base move speed
                _lungeDistanceTraveled += frameDistance;
                
                // check for hits during lunge
                CheckAttackHits();
            }
            else if (_isLunging)
            {
                // lunge complete, do final hit check
                _isLunging = false;
                CheckAttackHits();
            }
        }
        
        private void CheckAttackHits()
        {
            if (!IsServer) return;
            
            // calculate attack center in world space (relative to boss rotation)
            Vector3 attackCenter = transform.position + transform.TransformDirection(attackOffset);
            
            Collider[] hits = Physics.OverlapSphere(attackCenter, attackRadius);
            foreach (var hit in hits)
            {
                // skip if already hit this attack
                if (_hitTargetsThisAttack.Contains(hit.gameObject)) continue;
                
                if (hit.TryGetComponent<Core.IDamageable>(out var target) && hit.gameObject != gameObject)
                {
                    _hitTargetsThisAttack.Add(hit.gameObject);
                    target.TakeDamage(attackDamage);
                    TriggerBossHitFeedback(hit.transform.position, false);
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
