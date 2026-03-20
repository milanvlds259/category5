using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Category5.Core;
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
        [Tooltip("damage coefficient for light attacks (fraction of class attack damage, set by class data)")]
        [SerializeField] private float lightAttackCoefficient = 0.8f;
        [Tooltip("damage coefficient for heavy combo finisher (fraction of class attack damage, set by class data)")]
        [SerializeField] private float heavyAttackCoefficient = 1.5f;
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
        
        [Tooltip("transform where projectiles spawn from (aka avatar joint in the hand or bow tip)")]
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
        private OwnerPlayerNetworkAnimator _ownerNetworkAnimator;
        
        // charging state
        private bool _isCharging;
        private float _chargeStartTime;
        private float _lastChargePercent;
        
        // ranger q buff state
        private bool _rangerQActive;
        private float _rangerQAttackSpeedMult = 1f;
        private float _rangerQChargeSpeedMult = 1f;
        private int _rangerQBurstCount = 0;
        private float _rangerQBurstInterval = 0.1f;
        private float _rangerQBurstDamageMult = 1f;

        // pending attack data for animation event timing
        private bool _hasPendingMeleeHit;
        private float _pendingMeleeCoefficient;

        private bool _hasPendingRangedRelease;
        private float _pendingRangedChargePercent;

        // animator params for basic attack animation
        private static readonly int _animAttackTriggerHash = Animator.StringToHash("Attack");
        private static readonly int _animAttackAnimSpeedHash = Animator.StringToHash("AttackAnimSpeed");
        private RuntimeAnimatorController _cachedAnimatorController;
        private bool _animParamsCached;
        private bool _hasAnimAttackTrigger;
        private bool _hasAnimAttackAnimSpeed;

        // buffered melee chain input
        private bool _hasBufferedMeleeAttackInput;
        private float _bufferedMeleeInputExpireTime;
        private bool _meleeChainWindowOpen;

        [Header("Attack Buffer")]
        [SerializeField] private float meleeInputBufferTime = 0.25f;

        [Header("Attack Animation Speed")]
        [SerializeField] private float minAttackAnimationSpeed = 0.85f;
        [SerializeField] private float maxAttackAnimationSpeed = 1.35f;
        
        // public accessors for combat class and charging state
        public CombatClass CurrentCombatClass => combatClass;
        public bool IsCharging => _isCharging;
        public float ChargePercent => _isCharging && arrowData != null 
            ? Mathf.Clamp01((Time.time - _chargeStartTime) / (arrowData.MaxChargeTime * _rangerQChargeSpeedMult)) 
            : 0f;
        
        // public accessor for charge movement multiplier (used by playercontroller)
        public float ChargeMovementMultiplier => arrowData != null && arrowData.AllowCharge
            ? arrowData.ChargeMovementSpeedMultiplier
            : 1f;
        
        // set combat class based on loaded player class
        public void SetCombatClass(CombatClass newCombatClass)
        {
            combatClass = newCombatClass;
            Debug.Log($"PlayerCombat: Combat class set to {combatClass}");
        }
        
        // set arrow/projectile data based on loaded player class
        public void SetArrowData(ProjectileData data)
        {
            arrowData = data;
            Debug.Log($"PlayerCombat: Arrow data set to {(data != null ? data.name : "null")}");
        }
        
        // set melee coefficients from class data
        public void SetMeleeCoefficients(float light, float heavy)
        {
            lightAttackCoefficient = light;
            heavyAttackCoefficient = heavy;
            Debug.Log($"PlayerCombat: melee coefficients set to light={light:F2}, heavy={heavy:F2}");
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
            _ownerNetworkAnimator = GetComponent<OwnerPlayerNetworkAnimator>();
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
            
            // subscribe to model changes to update projectile spawn point
            PlayerModelManager.OnModelLoaded += OnModelLoaded;
            
            // check if model already loaded (in case it loaded before us)
            var modelManager = GetComponent<PlayerModelManager>();
            if (modelManager != null && modelManager.ProjectileSpawnPoint != null)
            {
                projectileSpawnPoint = modelManager.ProjectileSpawnPoint;
            }
        }
        
        public override void OnNetworkDespawn()
        {
            PlayerModelManager.OnModelLoaded -= OnModelLoaded;
        }
        
        // called when any player's model finishes loading
        private void OnModelLoaded(PlayerController player, Animator animator)
        {
            // only care about our own player's model
            if (player != _playerController) return;
            
            var modelManager = player.GetComponent<PlayerModelManager>();
            if (modelManager != null && modelManager.ProjectileSpawnPoint != null)
            {
                projectileSpawnPoint = modelManager.ProjectileSpawnPoint;
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
            float attackSpeedMult = _playerStats != null ? _playerStats.GetEffectiveAttackSpeedMultiplier() : 1f;
            float effectiveComboReset = comboResetTime / Mathf.Max(0.01f, attackSpeedMult);
            if (Time.time > _lastAttackTime + effectiveComboReset && _comboCounter > 0)
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
            
            if (CanAttack())
            {
                // melee attack on performed
                PerformMeleeAttack();
                return;
            }

            // if cooldown is still running while animation is in chain window, buffer input
            if (CanBufferMeleeAttackInput())
            {
                _hasBufferedMeleeAttackInput = true;
                _bufferedMeleeInputExpireTime = Time.time + meleeInputBufferTime;
            }
        }
        
        // checks if the player can currently attack
        private bool CanAttack()
        {
            if (_isAttacking) return false;
            if (Category5.UI.PauseMenu.GameIsPaused) return false;
            
            // prevent attack input during power-up selection
            if (Category5.Core.GameFlowManager.Instance != null && 
                Category5.Core.GameFlowManager.Instance.CurrentPhase.Value == Category5.Core.GamePhase.PowerUpSelection) return false;
            
            // prevent attack input when dead
            if (_playerController != null && _playerController.IsDead.Value) return false;
            
            // prevent attack input during wind riding
            if (_playerController != null && _playerController.IsWindRiding) return false;
            
            return true;
        }

        // strict checks for whether input buffering is allowed
        private bool CanBufferMeleeAttackInput()
        {
            if (combatClass != CombatClass.Melee) return false;
            if (!_meleeChainWindowOpen) return false;
            if (Category5.UI.PauseMenu.GameIsPaused) return false;

            if (Category5.Core.GameFlowManager.Instance != null &&
                Category5.Core.GameFlowManager.Instance.CurrentPhase.Value == Category5.Core.GamePhase.PowerUpSelection)
            {
                return false;
            }

            if (_playerController != null && _playerController.IsDead.Value) return false;

            return true;
        }

        // starts charging a ranged attack
        private void StartCharging()
        {
            if (arrowData == null)
            {
                Debug.LogWarning("PlayerCombat: Cannot attack - no projectile data assigned for this class!");
                return;
            }

            if (!arrowData.AllowCharge)
            {
                PerformChargedRangedAttack(0f);
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
            _meleeChainWindowOpen = false;
            
            // fire audio event for attack swing
            PlayerEvents.InvokeAttackSwing(transform.position);

            // determine damage coefficient and duration based on combo step
            float coefficient = lightAttackCoefficient;
            float duration = attack1Duration;

            if (_comboCounter == 2) duration = attack2Duration;
            if (_comboCounter >= 3)
            {
                coefficient = heavyAttackCoefficient;
                duration = attack3Duration;
                // Reset combo after 3rd hit
                _comboCounter = 0; 
            }

            float attackSpeedMultiplier = _playerStats != null ? _playerStats.GetEffectiveAttackSpeedMultiplier() : 1f;
            duration /= Mathf.Max(0.01f, attackSpeedMultiplier);

            // play a single basic attack animation for this attack
            PlayBasicAttackAnimation(duration);

            // visuals (Placeholder)
            // Debug.Log($"Player Melee Attack! Combo: {_comboCounter-1} | Damage: {damage}");

            _hasPendingMeleeHit = true;
            _pendingMeleeCoefficient = coefficient;
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
            
            // start cooldown (modified by ranger q buff)
            float attackSpeedMultiplier = _playerStats != null ? _playerStats.GetEffectiveAttackSpeedMultiplier() : 1f;
            float effectiveCooldown = (rangedAttackCooldown * _rangerQAttackSpeedMult) / Mathf.Max(0.01f, attackSpeedMultiplier);

            // play a single basic attack animation for this shot
            PlayBasicAttackAnimation(effectiveCooldown);

            _hasPendingRangedRelease = true;
            _pendingRangedChargePercent = chargePercent;

            StartCoroutine(AttackCooldown(effectiveCooldown));
        }

        // executes ranged attack logic once release timing is reached
        private void ExecuteRangedAttack(float chargePercent)
        {
            // check if this is a fully charged shot with ranger q active
            if (_rangerQActive && chargePercent >= 0.99f)
            {
                // fire burst of arrows
                StartCoroutine(FireBurstArrows());
            }
            else
            {
                // fire single arrow
                FireSingleArrow(chargePercent);
            }
        }

        // called from animation event relay on the model animator
        public void OnAttackImpactAnimationEvent()
        {
            if (!IsOwner) return;

            if (_hasPendingMeleeHit)
            {
                RequestMeleeAttackServerRpc(_pendingMeleeCoefficient, transform.position, transform.forward);
                _hasPendingMeleeHit = false;
                return;
            }

            if (_hasPendingRangedRelease)
            {
                ExecuteRangedAttack(_pendingRangedChargePercent);
                _hasPendingRangedRelease = false;
                return;
            }
        }

        // gets the active model animator from PlayerModelManager
        private Animator GetModelAnimator()
        {
            var modelManager = GetComponent<PlayerModelManager>();
            return modelManager != null ? modelManager.ModelAnimator : null;
        }

        // cache available animator params for the current controller
        private void EnsureAttackAnimParamCache(Animator anim)
        {
            var controller = anim.runtimeAnimatorController;
            if (_animParamsCached && _cachedAnimatorController == controller)
            {
                return;
            }

            _cachedAnimatorController = controller;
            _animParamsCached = true;
            _hasAnimAttackTrigger = false;
            _hasAnimAttackAnimSpeed = false;

            if (controller == null)
            {
                Debug.LogError("PlayerCombat: Model animator has no runtime animator controller. Cannot trigger attack animation.");
                return;
            }

            var parameters = anim.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.nameHash == _animAttackTriggerHash)
                {
                    _hasAnimAttackTrigger = true;
                }

                if (parameter.nameHash == _animAttackAnimSpeedHash)
                {
                    _hasAnimAttackAnimSpeed = true;
                }
            }
        }

        // plays one basic attack animation per attack input
        private void PlayBasicAttackAnimation(float attackDuration)
        {
            var anim = GetModelAnimator();
            if (anim == null)
            {
                Debug.LogError("PlayerCombat: No model animator available on PlayerModelManager. Cannot play attack animation.");
                return;
            }

            EnsureAttackAnimParamCache(anim);

            if (!_hasAnimAttackAnimSpeed)
            {
                Debug.LogError("PlayerCombat: Animator parameter 'AttackAnimSpeed' (Float) is missing. Add it to the active runtime animator controller.");
                return;
            }

            float attackAnimSpeed = GetAttackAnimationSpeedMultiplier();
            anim.SetFloat(_animAttackAnimSpeedHash, attackAnimSpeed);

            if (_hasAnimAttackTrigger)
            {
                if (_ownerNetworkAnimator == null)
                {
                    Debug.LogError("PlayerCombat: Missing OwnerPlayerNetworkAnimator. Cannot sync attack trigger.");
                    return;
                }

                _ownerNetworkAnimator.SetTrigger(_animAttackTriggerHash);
                return;
            }

            Debug.LogError("PlayerCombat: Animator parameter 'Attack' (Trigger) is missing. Add it to the active runtime animator controller.");
        }

        // maps attack speed stat to animator attack speed with safe clamps
        private float GetAttackAnimationSpeedMultiplier()
        {
            float attackSpeedMultiplier = _playerStats != null ? _playerStats.GetEffectiveAttackSpeedMultiplier() : 1f;
            return Mathf.Clamp(attackSpeedMultiplier, minAttackAnimationSpeed, maxAttackAnimationSpeed);
        }

        // animation event hook opens a chain window for buffered attack input
        public void OnAttackChainWindowOpenAnimationEvent()
        {
            if (!IsOwner) return;
            _meleeChainWindowOpen = true;
        }

        // animation event hook closes the chain window
        public void OnAttackChainWindowCloseAnimationEvent()
        {
            if (!IsOwner) return;
            _meleeChainWindowOpen = false;

            // attack recovery is animation-driven for melee so buffered followups only start once
            // the current attack clip reaches its close event
            _isAttacking = false;

            if (_hasPendingMeleeHit)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("PlayerCombat: Melee attack finished without receiving AttackImpact. clearing pending hit without dealing damage.");
#endif
                _hasPendingMeleeHit = false;
            }

            if (_hasBufferedMeleeAttackInput)
            {
                if (Time.time <= _bufferedMeleeInputExpireTime && CanAttack())
                {
                    _hasBufferedMeleeAttackInput = false;
                    PerformMeleeAttack();
                    return;
                }

                _hasBufferedMeleeAttackInput = false;
            }
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
        
        // fires a burst of arrows rapidly (ranger q ability)
        private IEnumerator FireBurstArrows()
        {
            for (int i = 0; i < _rangerQBurstCount; i++)
            {
                // get spawn position for each arrow
                Vector3 spawnPos = projectileSpawnPoint != null 
                    ? projectileSpawnPoint.position 
                    : transform.position + transform.forward * 0.5f + Vector3.up * 1.5f;
                
                spawnPos += (projectileSpawnPoint != null ? projectileSpawnPoint.forward : transform.forward) * arrowData.SpawnForwardOffset;
                
                // use current aim direction (player can move and aim during burst)
                Vector3 direction = GetAimDirection(1f, 1f); // full charge stats
                
                // apply burst damage multiplier
                float damageMultiplier = _rangerQBurstDamageMult;
                float speedMultiplier = 1f;
                
                // spawn arrow
                RequestChargedRangedAttackServerRpc(spawnPos, direction, damageMultiplier, speedMultiplier);
                
                // wait before next arrow
                if (i < _rangerQBurstCount - 1)
                {
                    yield return new WaitForSeconds(_rangerQBurstInterval);
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
        private void RequestMeleeAttackServerRpc(float damageCoefficient, Vector3 position, Vector3 direction)
        {
            // server performs the hit check to prevent cheating
            // for a simple prototype we use OverlapSphere in front of the player
            Vector3 attackPoint = position + direction * attackOffset;
            Collider[] hitEnemies = Physics.OverlapSphere(attackPoint, attackRange, enemyLayers, QueryTriggerInteraction.Collide);

            // get player stats for damage modifiers
            if (_playerStats == null)
            {
                _playerStats = GetComponent<PlayerStats>();
            }
            
            // calculate final damage using coefficient-based formula
            DamageResult result = _playerStats != null 
                ? _playerStats.CalculateDamage(damageCoefficient) 
                : new DamageResult { damage = Mathf.RoundToInt(damageCoefficient * 100f), wasCrit = false };
            int finalDamage = result.damage;
            
            int lifestealAmount = _playerStats != null ? _playerStats.LifestealAmount : 0;

            // determine if this is a heavy hit (combo finisher)
            bool isHeavyHit = damageCoefficient >= heavyAttackCoefficient;
            var hitTargetIds = new HashSet<int>();
            int validTargetCount = 0;
            
            foreach (Collider enemy in hitEnemies)
            {
                IDamageable damageable = null;
                if (!enemy.TryGetComponent<IDamageable>(out damageable))
                {
                    damageable = enemy.GetComponentInParent<IDamageable>();
                }

                if (damageable == null)
                {
                    continue;
                }

                var damageableComponent = damageable as Component;
                int targetId = damageableComponent != null
                    ? damageableComponent.gameObject.GetInstanceID()
                    : enemy.transform.root.gameObject.GetInstanceID();

                if (!hitTargetIds.Add(targetId))
                {
                    continue;
                }

                validTargetCount++;
                damageable.TakeDamage(finalDamage);
                
                // apply lifesteal healing
                if (lifestealAmount > 0)
                {
                    ApplyLifesteal(lifestealAmount);
                }

                Vector3 hitPosition = damageableComponent != null ? damageableComponent.transform.position : enemy.transform.position;
                
                // notify the attacking player to show damage number
                ShowDamageNumberClientRpc(finalDamage, hitPosition, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { OwnerClientId }
                    }
                });
                
                // trigger hit feedback for the attacking player
                TriggerHitFeedbackClientRpc(hitPosition, isHeavyHit, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { OwnerClientId }
                    }
                });
                
                // notify hit feedback manager for vfx hooks (all clients)
                NotifyPlayerHitClientRpc(hitPosition, finalDamage, isHeavyHit);
            }

            if (validTargetCount == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"PlayerCombat: melee attack found no valid targets at {attackPoint} range {attackRange} mask {enemyLayers.value}. raw collider hits: {hitEnemies.Length}");
#endif
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
        // ranger q ability buff methods
        // =====================================
        
        public void ApplyRangerQBuff(float attackSpeedMult, float chargeSpeedMult, int burstCount, float burstInterval, float burstDamageMult)
        {
            _rangerQActive = true;
            _rangerQAttackSpeedMult = attackSpeedMult;
            _rangerQChargeSpeedMult = chargeSpeedMult;
            _rangerQBurstCount = burstCount;
            _rangerQBurstInterval = burstInterval;
            _rangerQBurstDamageMult = burstDamageMult;
            
            // Debug.Log("RangerQ buff applied! Attack speed and charge speed increased, burst fire enabled.");
        }
        
        public void RemoveRangerQBuff()
        {
            _rangerQActive = false;
            _rangerQAttackSpeedMult = 1f;
            _rangerQChargeSpeedMult = 1f;
            _rangerQBurstCount = 0;
            _rangerQBurstInterval = 0.1f;
            _rangerQBurstDamageMult = 1f;
            
            // Debug.Log("RangerQ buff removed.");
        }
    }
}
