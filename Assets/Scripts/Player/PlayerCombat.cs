using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Category5.Core;

namespace Category5.Player
{
    public class PlayerCombat : NetworkBehaviour
    {
        [Header("Combat Stats")]
        [SerializeField] private int lightDamage = 10;
        [SerializeField] private int heavyDamage = 25;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackOffset = 1f;
        [SerializeField] private LayerMask enemyLayers;

        [Header("Combo Settings")]
        [SerializeField] private float comboResetTime = 1f;
        [SerializeField] private float attack1Duration = 0.3f;
        [SerializeField] private float attack2Duration = 0.4f;
        [SerializeField] private float attack3Duration = 0.6f;

        private InputSystem_Actions _inputActions;
        private int _comboCounter = 0;
        private float _lastAttackTime;
        private bool _isAttacking;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            if (_inputActions != null)
            {
                _inputActions.Player.Enable();
                _inputActions.Player.Attack.performed += OnAttack;
            }
        }

        private void OnDisable()
        {
            if (_inputActions != null)
            {
                _inputActions.Player.Attack.performed -= OnAttack;
                _inputActions.Player.Disable();
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            // reset combo if too much time has passed
            if (Time.time > _lastAttackTime + comboResetTime && _comboCounter > 0)
            {
                _comboCounter = 0;
            }
        }

        private void OnAttack(InputAction.CallbackContext context)
        {
            if (_isAttacking) return;
            if (Category5.UI.PauseMenu.GameIsPaused) return;

            PerformAttack();
        }

        private void PerformAttack()
        {
            _isAttacking = true;
            _lastAttackTime = Time.time;
            _comboCounter++;

            // determine damage and duration based on combo step
            int damage = lightDamage;
            float duration = attack1Duration;

            if (_comboCounter == 2) duration = attack2Duration;
            if (_comboCounter >= 3)
            {
                damage = heavyDamage;
                duration = attack3Duration;
                // Reset combo after 3rd hit
                _comboCounter = 0; 
            }

            // visuals (Placeholder)
            Debug.Log($"Player Attacking! Combo: {_comboCounter-1} | Damage: {damage}");

            // networked attack logic
            RequestAttackServerRpc(damage, transform.position, transform.forward);

            // start cooldown coroutine
            StartCoroutine(AttackCooldown(duration));
        }

        private IEnumerator AttackCooldown(float duration)
        {
            yield return new WaitForSeconds(duration);
            _isAttacking = false;
        }

        [ServerRpc]
        private void RequestAttackServerRpc(int damage, Vector3 position, Vector3 direction)
        {
            // server performs the hit check to prevent cheating
            // for a simple prototype we use OverlapSphere in front of the player
            Vector3 attackPoint = position + direction * attackOffset;
            Collider[] hitEnemies = Physics.OverlapSphere(attackPoint, attackRange, enemyLayers);

            foreach (Collider enemy in hitEnemies)
            {
                if (enemy.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(damage);
                    
                    // notify the attacking player to show damage number
                    // use the enemy's position for the damage number
                    ShowDamageNumberClientRpc(damage, enemy.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { OwnerClientId }
                        }
                    });
                }
            }

            // optional: notify clients to play VFX/Sound
            PlayAttackVfxClientRpc(position, direction);
        }
        
        [ClientRpc]
        private void ShowDamageNumberClientRpc(int damage, Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            // only the attacking player sees their damage numbers
            if (Category5.UI.UIManager.Instance != null)
            {
                Category5.UI.UIManager.Instance.ShowDamageNumber(damage, position);
            }
        }

        [ClientRpc]
        private void PlayAttackVfxClientRpc(Vector3 position, Vector3 direction)
        {
            // TODO: play particle effect or sound here
            // if we aree owner, we might have already played it immediately for responsiveness
            if (!IsOwner)
            {
                // play sound/vfx for other players
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 attackPoint = transform.position + transform.forward * attackOffset;
            Gizmos.DrawWireSphere(attackPoint, attackRange);
        }
    }
}
