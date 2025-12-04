using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Category5.Core;
using Category5.PowerUps;
using Category5.Audio;

namespace Category5.Player
{
    /// <summary>
    /// combat class determines whether player uses melee or ranged attacks
    /// </summary>
    public enum CombatClass
    {
        Melee,
        Ranged
    }
    
    public class PlayerCombat : NetworkBehaviour
    {
        [Header("Combat Class")]
        [Tooltip("switch between melee and ranged combat for testing")]
        [SerializeField] private CombatClass combatClass = CombatClass.Melee;
        
        [Header("Melee Combat Stats")]
        [SerializeField] private int lightDamage = 10;
        [SerializeField] private int heavyDamage = 25;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackOffset = 1f;
        [SerializeField] private LayerMask enemyLayers;

        [Header("Melee Combo Settings")]
        [SerializeField] private float comboResetTime = 1f;
        [SerializeField] private float attack1Duration = 0.3f;
        [SerializeField] private float attack2Duration = 0.4f;
        [SerializeField] private float attack3Duration = 0.6f;
        
        [Header("Ranged Combat Settings")]
        [Tooltip("projectile data defining arrow properties")]
        [SerializeField] private ProjectileData arrowData;
        
        [Tooltip("transform where projectiles spawn from (should be near player's hand or bow)")]
        [SerializeField] private Transform projectileSpawnPoint;
        
        [Tooltip("cooldown between ranged attacks in seconds")]
        [SerializeField] private float rangedAttackCooldown = 0.5f;

        private InputSystem_Actions _inputActions;
        private int _comboCounter = 0;
        private float _lastAttackTime;
        private bool _isAttacking;
        
        // reference to player stats for power-up modifiers
        private PlayerStats _playerStats;
        
        // charging state
        private bool _isCharging;
        private float _chargeStartTime;
        private float _lastChargePercent;
        
        // public accessors for combat class and charging state
        public CombatClass CurrentCombatClass => combatClass;
        public bool IsCharging => _isCharging;
        public float ChargePercent => _isCharging && arrowData != null 
            ? Mathf.Clamp01((Time.time - _chargeStartTime) / arrowData.MaxChargeTime) 
            : 0f;
        
        // public accessor for charge movement multiplier (used by playercontroller)
        public float ChargeMovementMultiplier => arrowData != null 
            ? arrowData.ChargeMovementSpeedMultiplier 
            : 0.5f;
        
        // static events for vfx/sfx to hook into
        public static event Action<Vector3> OnChargeStarted;
        public static event Action<float, Vector3> OnChargeProgress;
        public static event Action<float, Vector3> OnChargeReleased;
        public static event Action<Vector3> OnChargeCanceled;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
            _playerStats = GetComponent<PlayerStats>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }
            
            // cache stats reference if not found in awake
            if (_playerStats == null)
            {
                _playerStats = GetComponent<PlayerStats>();
            }
        }

        private void OnEnable()
        {
            if (_inputActions != null)
            {
                _inputActions.Player.Enable();
                // melee uses performed, ranged uses started/canceled for charging
                _inputActions.Player.Attack.performed += OnAttackPerformed;
                _inputActions.Player.Attack.started += OnAttackStarted;
                _inputActions.Player.Attack.canceled += OnAttackCanceled;
            }
        }

        private void OnDisable()
        {
            if (_inputActions != null)
            {
                _inputActions.Player.Attack.performed -= OnAttackPerformed;
                _inputActions.Player.Attack.started -= OnAttackStarted;
                _inputActions.Player.Attack.canceled -= OnAttackCanceled;
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
            
            // update charge progress event for ui/vfx
            if (_isCharging)
            {
                float currentPercent = ChargePercent;
                // only fire event when percent changes significantly (avoid spam)
                if (Mathf.Abs(currentPercent - _lastChargePercent) > 0.01f)
                {
                    _lastChargePercent = currentPercent;
                    OnChargeProgress?.Invoke(currentPercent, transform.position);
                }
            }
        }
        
        private void OnAttackStarted(InputAction.CallbackContext context)
        {
            // only ranged uses started for charging
            if (combatClass != CombatClass.Ranged) return;
            if (!CanAttack()) return;
            
            StartCharging();
        }
        
        private void OnAttackCanceled(InputAction.CallbackContext context)
        {
            // only ranged uses canceled for releasing charge
            if (combatClass != CombatClass.Ranged) return;
            if (!_isCharging) return;
            
            ReleaseCharge();
        }

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            // ranged uses started/canceled for charging, so skip performed
            if (combatClass == CombatClass.Ranged) return;
            
            if (!CanAttack()) return;

            // melee attack on performed
            PerformMeleeAttack();
        }
        
        /// <summary>
        /// checks if the player can currently attack
        /// </summary>
        private bool CanAttack()
        {
            if (_isAttacking) return false;
            if (Category5.UI.PauseMenu.GameIsPaused) return false;
            
            // prevent attack input during power-up selection
            if (PowerUpManager.Instance != null && 
                PowerUpManager.Instance.CurrentPhase.Value == GamePhase.PowerUpSelection) return false;
            
            // prevent attack input when dead
            var playerController = GetComponent<PlayerController>();
            if (playerController != null && playerController.IsDead.Value) return false;
            
            return true;
        }
        

        // starts charging a ranged attack
        private void StartCharging()
        {
            if (arrowData == null)
            {
                Debug.LogWarning("No arrow data assigned to PlayerCombat!");
                return;
            }
            
            _isCharging = true;
            _chargeStartTime = Time.time;
            _lastChargePercent = 0f;
            
            OnChargeStarted?.Invoke(transform.position);
            Debug.Log("Started charging arrow...");
        }
        
        // releases a charged ranged attack
        private void ReleaseCharge()
        {
            float chargePercent = ChargePercent;
            _isCharging = false;
            
            OnChargeReleased?.Invoke(chargePercent, transform.position);
            Debug.Log($"Released arrow with {chargePercent:P0} charge!");
            
            PerformChargedRangedAttack(chargePercent);
        }
        
        // cancels the current charge (called when taking damage)
        public void CancelCharge()
        {
            if (!_isCharging) return;
            
            _isCharging = false;
            OnChargeCanceled?.Invoke(transform.position);
            Debug.Log("Charge canceled!");
        }

        private void PerformMeleeAttack()
        {
            _isAttacking = true;
            _lastAttackTime = Time.time;
            _comboCounter++;
            
            // fire audio event for attack swing
            PlayerEvents.InvokeAttackSwing(transform.position);

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
            Debug.Log($"Player Melee Attack! Combo: {_comboCounter-1} | Damage: {damage}");

            // networked attack logic
            RequestMeleeAttackServerRpc(damage, transform.position, transform.forward);

            // start cooldown coroutine
            StartCoroutine(AttackCooldown(duration));
        }
        
        /// <summary>
        /// performs a charged ranged attack with the given charge percentage
        /// </summary>
        private void PerformChargedRangedAttack(float chargePercent)
        {
            if (arrowData == null)
            {
                Debug.LogWarning("No arrow data assigned to PlayerCombat!");
                return;
            }
            
            _isAttacking = true;
            _lastAttackTime = Time.time;
            
            // get spawn position and direction
            Vector3 spawnPos = projectileSpawnPoint != null 
                ? projectileSpawnPoint.position 
                : transform.position + transform.forward * 0.5f + Vector3.up * 1.5f;
            
            // use camera direction for aiming (accounts for vertical look angle)
            Vector3 direction = GetAimDirection();
            
            // calculate multipliers based on charge
            float damageMultiplier = Mathf.Lerp(1f, arrowData.MaxDamageMultiplier, chargePercent);
            float speedMultiplier = Mathf.Lerp(1f, arrowData.MaxSpeedMultiplier, chargePercent);
            
            Debug.Log($"Charged Ranged Attack! Charge: {chargePercent:P0}, Damage x{damageMultiplier:F2}, Speed x{speedMultiplier:F2}");
            
            // request server to spawn charged projectile
            RequestChargedRangedAttackServerRpc(spawnPos, direction, damageMultiplier, speedMultiplier);
            
            // start cooldown
            StartCoroutine(AttackCooldown(rangedAttackCooldown));
        }
        
        private Vector3 GetAimDirection()
        {
            // try to use main camera's forward direction for full 3d aiming
            if (Camera.main != null)
            {
                return Camera.main.transform.forward;
            }
            
            // fallback to player forward if no camera found
            return transform.forward;
        }

        private IEnumerator AttackCooldown(float duration)
        {
            yield return new WaitForSeconds(duration);
            _isAttacking = false;
        }

        [ServerRpc]
        private void RequestChargedRangedAttackServerRpc(Vector3 spawnPosition, Vector3 direction, float damageMultiplier, float speedMultiplier)
        {
            if (arrowData == null || arrowData.ProjectilePrefab == null)
            {
                Debug.LogWarning("Cannot spawn projectile - missing arrow data or prefab!");
                return;
            }
            
            // validate multipliers are within expected bounds (anti-cheat)
            damageMultiplier = Mathf.Clamp(damageMultiplier, 1f, arrowData.MaxDamageMultiplier);
            speedMultiplier = Mathf.Clamp(speedMultiplier, 1f, arrowData.MaxSpeedMultiplier);
            
            // get player stats for damage modifiers
            if (_playerStats == null)
            {
                _playerStats = GetComponent<PlayerStats>();
            }
            
            // spawn the projectile on the server
            GameObject projectileObj = Instantiate(
                arrowData.ProjectilePrefab, 
                spawnPosition, 
                Quaternion.LookRotation(direction)
            );
            
            // initialize projectile with charged multipliers
            if (projectileObj.TryGetComponent<NetworkedProjectile>(out var projectile))
            {
                projectile.InitializeCharged(arrowData, OwnerClientId, _playerStats, damageMultiplier, speedMultiplier);
            }
            
            // spawn on network
            var networkObject = projectileObj.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Spawn();
            }
            
            // notify clients to play fire vfx/sound
            PlayRangedAttackVfxClientRpc(spawnPosition, direction);
        }
        
        [ClientRpc]
        private void PlayRangedAttackVfxClientRpc(Vector3 position, Vector3 direction)
        {
            // TODO: play bow shot particle effect or sound here
            if (!IsOwner)
            {
                // play sound/vfx for other players
            }
        }

        [ServerRpc]
        private void RequestMeleeAttackServerRpc(int baseDamage, Vector3 position, Vector3 direction)
        {
            // server performs the hit check to prevent cheating
            // for a simple prototype we use OverlapSphere in front of the player
            Vector3 attackPoint = position + direction * attackOffset;
            Collider[] hitEnemies = Physics.OverlapSphere(attackPoint, attackRange, enemyLayers);

            // get player stats for damage modifiers
            if (_playerStats == null)
            {
                _playerStats = GetComponent<PlayerStats>();
            }
            
            // calculate final damage with power-up modifiers
            int finalDamage = _playerStats != null 
                ? _playerStats.CalculateDamage(baseDamage) 
                : baseDamage;
            
            int lifestealAmount = _playerStats != null ? _playerStats.LifestealAmount : 0;

            // determine if this is a heavy hit (combo finisher)
            bool isHeavyHit = baseDamage >= heavyDamage;
            
            foreach (Collider enemy in hitEnemies)
            {
                if (enemy.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(finalDamage);
                    
                    // apply lifesteal healing
                    if (lifestealAmount > 0)
                    {
                        ApplyLifesteal(lifestealAmount);
                    }
                    
                    // notify the attacking player to show damage number
                    // use the enemy's position for the damage number
                    ShowDamageNumberClientRpc(finalDamage, enemy.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { OwnerClientId }
                        }
                    });
                    
                    // trigger hit feedback for the attacking player
                    TriggerHitFeedbackClientRpc(enemy.transform.position, isHeavyHit, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { OwnerClientId }
                        }
                    });
                    
                    // notify hit feedback manager for vfx hooks (all clients)
                    NotifyPlayerHitClientRpc(enemy.transform.position, finalDamage, isHeavyHit);
                }
            }

            // optional: notify clients to play VFX/Sound
            PlayAttackVfxClientRpc(position, direction);
        }
        
        // applies lifesteal healing to the player (server only)
        private void ApplyLifesteal(int healAmount)
        {
            var playerController = GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.Heal(healAmount);
                
                // show heal feedback on client
                ShowLifestealVfxClientRpc(healAmount, transform.position, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { OwnerClientId }
                    }
                });
            }
        }
        
        [ClientRpc]
        private void ShowLifestealVfxClientRpc(int healAmount, Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            // fire audio event for healing
            PlayerEvents.InvokeHeal(position, healAmount);
            Debug.Log($"Lifesteal healed {healAmount} HP!");
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
        
        // trigger hit feedback effects for the attacking player only
        [ClientRpc]
        private void TriggerHitFeedbackClientRpc(Vector3 position, bool isHeavyHit, ClientRpcParams clientRpcParams = default)
        {
            if (HitFeedbackManager.Instance == null) return;
            
            if (isHeavyHit)
            {
                HitFeedbackManager.Instance.TriggerHeavyHit(position);
            }
            else
            {
                HitFeedbackManager.Instance.TriggerLightHit(position);
            }
        }
        
        // notify all clients for vfx hook events
        [ClientRpc]
        private void NotifyPlayerHitClientRpc(Vector3 position, int damage, bool isHeavyHit)
        {
            if (HitFeedbackManager.Instance != null)
            {
                HitFeedbackManager.Instance.NotifyPlayerHitEnemy(position, damage, isHeavyHit);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (combatClass == CombatClass.Melee)
            {
                // show melee attack range
                Gizmos.color = Color.red;
                Vector3 attackPoint = transform.position + transform.forward * attackOffset;
                Gizmos.DrawWireSphere(attackPoint, attackRange);
            }
            else
            {
                // show projectile spawn point for ranged
                Gizmos.color = Color.cyan;
                Vector3 spawnPos = projectileSpawnPoint != null 
                    ? projectileSpawnPoint.position 
                    : transform.position + transform.forward * 0.5f + Vector3.up * 1.5f;
                Gizmos.DrawWireSphere(spawnPos, 0.15f);
                Gizmos.DrawRay(spawnPos, transform.forward * 3f);
            }
        }
    }
}
