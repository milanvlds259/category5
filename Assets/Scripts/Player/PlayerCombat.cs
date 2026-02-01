using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Category5.Core;
using Category5.PowerUps;
using Category5.Items;
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
        
        [Tooltip("enable predictive aiming for moving enemies")]
        [SerializeField] private bool enableTargetLeading = true;

        private InputSystem_Actions _inputActions;
        private int _comboCounter = 0;
        private float _lastAttackTime;
        private bool _isAttacking;
        
        // reference to player stats for damage modifiers
        private PlayerStats _playerStats;
        private PlayerController _playerController;
        
        // charging state
        private bool _isCharging;
        private float _chargeStartTime;
        private float _lastChargePercent;
        
        // quickbow buff state
        private bool _quickbowActive;
        private float _quickbowAttackSpeedMult = 1f;
        private float _quickbowChargeSpeedMult = 1f;
        private int _quickbowBurstCount = 0;
        private float _quickbowBurstInterval = 0.1f;
        private float _quickbowBurstDamageMult = 1f;
        
        // public accessors for combat class and charging state
        public CombatClass CurrentCombatClass => combatClass;
        public bool IsCharging => _isCharging;
        public float ChargePercent => _isCharging && arrowData != null 
            ? Mathf.Clamp01((Time.time - _chargeStartTime) / (arrowData.MaxChargeTime * _quickbowChargeSpeedMult)) 
            : 0f;
        
        // public accessor for charge movement multiplier (used by playercontroller)
        public float ChargeMovementMultiplier => arrowData != null 
            ? arrowData.ChargeMovementSpeedMultiplier 
            : 0.5f;
        
        // set combat class based on loaded player class
        public void SetCombatClass(CombatClass newCombatClass)
        {
            combatClass = newCombatClass;
            Debug.Log($"PlayerCombat: Combat class set to {combatClass}");
        }
        
        // static events for vfx/sfx to hook into
        public static event Action<Vector3> OnChargeStarted;
        public static event Action<float, Vector3> OnChargeProgress;
        public static event Action<float, Vector3> OnChargeReleased;
        public static event Action<Vector3> OnChargeCanceled;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
            _playerStats = GetComponent<PlayerStats>();
            _playerController = GetComponent<PlayerController>();
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
            if (Category5.Items.ItemManager.Instance != null && 
                Category5.Items.ItemManager.Instance.CurrentPhase.Value == Category5.Core.GamePhase.PowerUpSelection) return false;
            
            // prevent attack input when dead
            if (_playerController != null && _playerController.IsDead.Value) return false;
            
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
            
            // cancel sprint when starting to charge
            if (_playerController != null)
            {
                _playerController.CancelSprint();
            }
            
            _isCharging = true;
            _chargeStartTime = Time.time;
            _lastChargePercent = 0f;
            
            OnChargeStarted?.Invoke(transform.position);
            // Debug.Log("Started charging arrow...");
        }
        
        // releases a charged ranged attack
        private void ReleaseCharge()
        {
            float chargePercent = ChargePercent;
            _isCharging = false;
            
            OnChargeReleased?.Invoke(chargePercent, transform.position);
            // Debug.Log($"Released arrow with {chargePercent:P0} charge!");
            
            PerformChargedRangedAttack(chargePercent);
        }
        
        // cancels the current charge (called when taking damage)
        public void CancelCharge()
        {
            if (!_isCharging) return;
            
            _isCharging = false;
            OnChargeCanceled?.Invoke(transform.position);
            // Debug.Log("Charge canceled!");
        }

        private void PerformMeleeAttack()
        {
            // cancel sprint when attacking
            if (_playerController != null)
            {
                _playerController.CancelSprint();
            }
            
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
            // Debug.Log($"Player Melee Attack! Combo: {_comboCounter-1} | Damage: {damage}");

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
            
            // check if this is a fully charged shot with quickbow active
            if (_quickbowActive && chargePercent >= 0.99f)
            {
                // fire burst of arrows
                StartCoroutine(FireBurstArrows());
            }
            else
            {
                // fire single arrow
                FireSingleArrow(chargePercent);
            }
            
            // start cooldown (modified by quickbow buff)
            float effectiveCooldown = rangedAttackCooldown * _quickbowAttackSpeedMult;
            StartCoroutine(AttackCooldown(effectiveCooldown));
        }
        
        // fires a single arrow with the given charge
        private void FireSingleArrow(float chargePercent)
        {
            // calculate multipliers based on charge (modified by quickbow)
            float damageMultiplier = Mathf.Lerp(1f, arrowData.MaxDamageMultiplier, chargePercent);
            float speedMultiplier = Mathf.Lerp(1f, arrowData.MaxSpeedMultiplier, chargePercent);
            
            // get spawn position
            Vector3 spawnPos = projectileSpawnPoint != null 
                ? projectileSpawnPoint.position 
                : transform.position + transform.forward * 0.5f + Vector3.up * 1.5f;
            
            // apply forward offset to prevent collision with shooter
            spawnPos += (projectileSpawnPoint != null ? projectileSpawnPoint.forward : transform.forward) * arrowData.SpawnForwardOffset;
            
            // use raycast-based aiming (accounts for crosshair position)
            Vector3 direction = GetAimDirection(chargePercent, speedMultiplier);
            
            // Debug.Log($"Charged Ranged Attack! Charge: {chargePercent:P0}, Damage x{damageMultiplier:F2}, Speed x{speedMultiplier:F2}");
            
            // request server to spawn charged projectile
            RequestChargedRangedAttackServerRpc(spawnPos, direction, damageMultiplier, speedMultiplier);
        }
        
        // fires a burst of arrows rapidly (quickbow ability)
        private IEnumerator FireBurstArrows()
        {
            for (int i = 0; i < _quickbowBurstCount; i++)
            {
                // get spawn position for each arrow
                Vector3 spawnPos = projectileSpawnPoint != null 
                    ? projectileSpawnPoint.position 
                    : transform.position + transform.forward * 0.5f + Vector3.up * 1.5f;
                
                spawnPos += (projectileSpawnPoint != null ? projectileSpawnPoint.forward : transform.forward) * arrowData.SpawnForwardOffset;
                
                // use current aim direction (player can move and aim during burst)
                Vector3 direction = GetAimDirection(1f, 1f); // full charge stats
                
                // apply burst damage multiplier
                float damageMultiplier = _quickbowBurstDamageMult;
                float speedMultiplier = 1f;
                
                // spawn arrow
                RequestChargedRangedAttackServerRpc(spawnPos, direction, damageMultiplier, speedMultiplier);
                
                // wait before next arrow
                if (i < _quickbowBurstCount - 1)
                {
                    yield return new WaitForSeconds(_quickbowBurstInterval);
                }
            }
        }
        
        // calculates aim direction using screen-center raycast to ensure projectiles hit where crosshair points
        private Vector3 GetAimDirection(float chargePercent, float speedMultiplier)
        {
            if (Camera.main == null || arrowData == null)
            {
                // fallback to player forward if no camera or arrow data
                return transform.forward;
            }

            // calculate spawn position with offset (computed once and reused)
            Vector3 spawnPos = projectileSpawnPoint != null
                ? projectileSpawnPoint.position
                : transform.position + transform.forward * 0.5f + Vector3.up * 1.5f;
            spawnPos += (projectileSpawnPoint != null ? projectileSpawnPoint.forward : transform.forward) * arrowData.SpawnForwardOffset;

            // calculate effective projectile speed (accounts for charge multiplier)
            float effectiveProjectileSpeed = arrowData.Speed * speedMultiplier;

            // calculate effective aim range (base range + charge bonus if fully charged, clamped to projectile lifetime)
            float effectiveRange = arrowData.BaseAimRange;
            if (chargePercent >= 0.99f)
            {
                effectiveRange += arrowData.ChargedAimRangeBonus;
            }
            effectiveRange = Mathf.Min(effectiveRange, arrowData.Lifetime * effectiveProjectileSpeed);

            // cast ray from screen center (where crosshair is)
            Ray aimRay = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 targetPoint;

            // raycast to find what we're aiming at
            if (Physics.Raycast(aimRay, out RaycastHit hit, effectiveRange, arrowData.AimLayers))
            {
                targetPoint = hit.point;

                // apply target leading if enabled and hit a moving enemy
                if (enableTargetLeading && hit.collider != null)
                {
                    // check if hit object is on enemy layer and has rigidbody
                    if (((1 << hit.collider.gameObject.layer) & LayerMask.GetMask("Enemy")) != 0)
                    {
                        Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
                        if (rb != null && rb.linearVelocity.magnitude > 0.1f)
                        {
                            // get intercept point for moving target
                            targetPoint = GetInterceptPoint(hit.point, rb.linearVelocity, spawnPos, effectiveProjectileSpeed);
                        }
                    }
                }
            }
            else
            {
                // no hit - aim at max range along ray direction
                targetPoint = aimRay.GetPoint(effectiveRange);
            }

            // return direction from spawn position to target point
            return (targetPoint - spawnPos).normalized;
        }
        
        // calculates intercept point for hitting a moving target
        // MATH WARNING THE CODE BELOW USES COMPLEX NERD AHH MATH AVERT YOUR EYES
        private Vector3 GetInterceptPoint(Vector3 targetPos, Vector3 targetVel, Vector3 shooterPos, float projectileSpeed)
        {
            // relative position and velocity
            Vector3 toTarget = targetPos - shooterPos;
            
            // quadratic equation coefficients: a*t^2 + b*t + c = 0
            float a = targetVel.sqrMagnitude - projectileSpeed * projectileSpeed;
            float b = 2f * Vector3.Dot(toTarget, targetVel);
            float c = toTarget.sqrMagnitude;
            
            // discriminant
            float discriminant = b * b - 4f * a * c;
            
            // if discriminant is negative, target is too fast to catch
            if (discriminant < 0f || Mathf.Abs(a) < 0.001f)
            {
                // fallback to current position (no leading)
                return targetPos;
            }
            
            // solve for time (use smallest positive root)
            float t1 = (-b + Mathf.Sqrt(discriminant)) / (2f * a);
            float t2 = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
            
            float t = Mathf.Min(t1, t2);
            if (t < 0f)
            {
                t = Mathf.Max(t1, t2);
            }
            
            // if both solutions are negative, target is moving away too fast
            if (t < 0f)
            {
                return targetPos;
            }
            
            // return predicted future position
            return targetPos + targetVel * t;
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
            
            // calculate final damage with item modifiers
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
            if (_playerController != null)
            {
                _playerController.Heal(healAmount);
                
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
            // Debug.Log($"Lifesteal healed {healAmount} HP!");
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
            else if (combatClass == CombatClass.Ranged && arrowData != null && Camera.main != null)
            {
                // visualize aim raycast system for ranged combat
                
                // calculate spawn position with offset
                Vector3 spawnPos = projectileSpawnPoint != null 
                    ? projectileSpawnPoint.position 
                    : transform.position + transform.forward * 0.5f + Vector3.up * 1.5f;
                Vector3 spawnOffset = (projectileSpawnPoint != null ? projectileSpawnPoint.forward : transform.forward) * arrowData.SpawnForwardOffset;
                Vector3 offsetSpawnPos = spawnPos + spawnOffset;
                
                // calculate effective range (simulating full charge for visualization)
                float effectiveProjectileSpeed = arrowData.Speed * arrowData.MaxSpeedMultiplier;
                float effectiveRange = arrowData.BaseAimRange + arrowData.ChargedAimRangeBonus;
                effectiveRange = Mathf.Min(effectiveRange, arrowData.Lifetime * effectiveProjectileSpeed);
                
                // cast ray from screen center
                Ray aimRay = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                
                // draw cyan ray from camera through screen center
                Gizmos.color = Color.cyan;
                Vector3 targetPoint;
                
                if (Physics.Raycast(aimRay, out RaycastHit hit, effectiveRange, arrowData.AimLayers))
                {
                    // hit something - draw to hit point
                    targetPoint = hit.point;
                    Gizmos.DrawLine(aimRay.origin, hit.point);
                    
                    // draw red sphere at hit point
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(hit.point, 0.3f);
                    
                    // check if we would apply target leading
                    if (enableTargetLeading && hit.collider != null)
                    {
                        if (((1 << hit.collider.gameObject.layer) & LayerMask.GetMask("Enemy")) != 0)
                        {
                            Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
                            if (rb != null && rb.linearVelocity.magnitude > 0.1f)
                            {
                                // calculate intercept point
                                Vector3 interceptPoint = GetInterceptPoint(hit.point, rb.linearVelocity, offsetSpawnPos, effectiveProjectileSpeed);
                                
                                // draw green line to intercept point
                                Gizmos.color = Color.green;
                                Gizmos.DrawLine(offsetSpawnPos, interceptPoint);
                                Gizmos.DrawWireSphere(interceptPoint, 0.25f);
                                
                                targetPoint = interceptPoint;
                            }
                        }
                    }
                }
                else
                {
                    // no hit - draw to max range
                    targetPoint = aimRay.GetPoint(effectiveRange);
                    Gizmos.DrawLine(aimRay.origin, targetPoint);
                }
                
                // draw yellow sphere at offset spawn position
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(offsetSpawnPos, 0.2f);
                
                // draw original spawn position (before offset) for comparison
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // semi-transparent orange
                Gizmos.DrawWireSphere(spawnPos, 0.15f);
                
                // draw final firing direction
                Gizmos.color = Color.white;
                Vector3 fireDirection = (targetPoint - offsetSpawnPos).normalized;
                Gizmos.DrawRay(offsetSpawnPos, fireDirection * 5f);
                
                // draw range text
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(offsetSpawnPos + Vector3.up * 0.5f, $"Range: {effectiveRange:F1}m");
                #endif
            }
        }
        
        // =====================================
        // quickbow ability buff methods
        // =====================================
        
        public void ApplyQuickbowBuff(float attackSpeedMult, float chargeSpeedMult, int burstCount, float burstInterval, float burstDamageMult)
        {
            _quickbowActive = true;
            _quickbowAttackSpeedMult = attackSpeedMult;
            _quickbowChargeSpeedMult = chargeSpeedMult;
            _quickbowBurstCount = burstCount;
            _quickbowBurstInterval = burstInterval;
            _quickbowBurstDamageMult = burstDamageMult;
            
            // Debug.Log("Quickbow buff applied! Attack speed and charge speed increased, burst fire enabled.");
        }
        
        public void RemoveQuickbowBuff()
        {
            _quickbowActive = false;
            _quickbowAttackSpeedMult = 1f;
            _quickbowChargeSpeedMult = 1f;
            _quickbowBurstCount = 0;
            _quickbowBurstInterval = 0.1f;
            _quickbowBurstDamageMult = 1f;
            
            // Debug.Log("Quickbow buff removed.");
        }
    }
}
