using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Audio;
using Category5.Player;

namespace Category5.Enemies
{
    // basic melee enemy that chases and attacks players
    // uses EnemyData for all stats - designers can create variants via ScriptableObjects
    public class BasicEnemy : EnemyBase
    {
        [Header("attack settings")]
        [SerializeField] private float attackDuration = 0.5f;
        [SerializeField] private float damageDelay = 0.25f;
        
        [Header("visual feedback")]
        [SerializeField] private Renderer meshRenderer;
        
        private Color _originalColor;
        private bool _hasDealtDamageThisAttack;
        private float _damageDelayTimer;
        
        protected override void Awake()
        {
            base.Awake();
            
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<Renderer>();
            }
        }
        
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            // cache original color for hit flash
            if (meshRenderer != null)
            {
                _originalColor = meshRenderer.material.color;
                
                // apply enemy color tint if specified
                if (enemyData != null && enemyData.enemyColor != Color.white)
                {
                    meshRenderer.material.color = enemyData.enemyColor;
                    _originalColor = enemyData.enemyColor;
                }
            }
        }
        
        // =====================================
        // attack implementation
        // =====================================
        
        protected override void ExecuteAttack()
        {
            stateTimer = attackDuration;
            _hasDealtDamageThisAttack = false;
            _damageDelayTimer = damageDelay;
            
            // fire attack event
            NotifyAttackClientRpc(transform.position);
        }
        
        [ClientRpc]
        private void NotifyAttackClientRpc(Vector3 position)
        {
            EnemyEvents.InvokeAttack(position, elementType);
        }
        
        protected override void OnAttackUpdate()
        {
            // rotate toward target during attack
            RotateTowardTarget();
            
            // deal damage after delay
            if (!_hasDealtDamageThisAttack)
            {
                _damageDelayTimer -= Time.deltaTime;
                if (_damageDelayTimer <= 0f)
                {
                    DealDamage();
                    _hasDealtDamageThisAttack = true;
                }
            }
            
            base.OnAttackUpdate();
        }
        
        private void DealDamage()
        {
            if (currentTargetController == null) return;
            
            // check if still in range
            if (!IsTargetInRange(attackRange * 1.2f)) return;
            
            // deal damage to target
            currentTargetController.TakeDamage(damage);
        }
        
        // =====================================
        // visual feedback
        // =====================================
        
        protected override void OnHealthChanged(int oldHealth, int newHealth)
        {
            base.OnHealthChanged(oldHealth, newHealth);
            
            // flash red on damage
            if (newHealth < oldHealth)
            {
                PlayHitFlashClientRpc();
            }
        }
        
        [ClientRpc]
        private void PlayHitFlashClientRpc()
        {
            if (meshRenderer != null)
            {
                StartCoroutine(HitFlashCoroutine());
            }
        }
        
        private System.Collections.IEnumerator HitFlashCoroutine()
        {
            if (meshRenderer != null)
            {
                meshRenderer.material.color = Color.red;
                yield return new WaitForSeconds(0.1f);
                meshRenderer.material.color = _originalColor;
            }
        }
        
        // =====================================
        // gizmos
        // =====================================
        
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            
            // draw attack direction
            if (currentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, transform.position + transform.forward * attackRange);
            }
        }
    }
}
