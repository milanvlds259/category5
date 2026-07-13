using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Category5.Player;
using Category5.Player.Abilities;
using Category5.Items;
using Category5.UI;
using Category5.Audio;
using Category5.Enemies;
using Category5.Core;
using Category5.Boss;
using Category5.Player.Van;
using Category5.SkillTree;

namespace Category5
{
    public enum AbilitySlot
    {
        Ability1, // Q
        Ability2, // E
        Ability3  // R
    }

    public class PlayerAbilityManager : NetworkBehaviour
    {
        [Header("Ability Components - will be auto-found from children")]
        [SerializeField] private AbilityBase ability1; // Q
        [SerializeField] private AbilityBase ability2; // E
        [SerializeField] private AbilityBase ability3; // R (ultimate)
        
        [Header("Critshot Projectile Data")]
        [SerializeField] private ProjectileData critshotArrowData;

        [Header("Ranger Prefabs")]
        [SerializeField] private GameObject rangerEArrowPrefab;
        [SerializeField] private GameObject rangerEZonePrefab;

        [Header("Cooldown Tracking")]
        public NetworkVariable<float> ability1Cooldown = new NetworkVariable<float>(0f);
        public NetworkVariable<float> ability2Cooldown = new NetworkVariable<float>(0f);
        public NetworkVariable<float> ability3Cooldown = new NetworkVariable<float>(0f);

        [Header("Enchanter Charges")]
        [SerializeField] private int maxEnchanterCharges = 5;
        [SerializeField] private float enchanterChargeDecaySeconds = 15f;
        public NetworkVariable<int> enchanterCharges = new NetworkVariable<int>(0);

        private PlayerController playerController;
        private PlayerStats playerStats;
        private PlayerCombat playerCombat;
        private InputSystem_Actions inputActions;
        private UltimateLockManager _ultimateLockManager;

        // prevents multiple abilities from executing simultaneously
        public bool IsExecutingAbility { get; private set; }

        // events for ui updates - includes reference to source PlayerAbilityManager so UI can filter
        public static event Action<PlayerAbilityManager, AbilitySlot, float, float> OnCooldownChanged; // source, slot, current, max
        public static event Action<PlayerAbilityManager, int, int> OnEnchanterChargesChanged; // source, current, max

        private float _enchanterChargeTimer;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerStats = GetComponent<PlayerStats>();
            playerCombat = GetComponent<PlayerCombat>();
            inputActions = new InputSystem_Actions();
            _ultimateLockManager = GetComponent<UltimateLockManager>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // abilities may not be instantiated yet on network spawn
            // they will be loaded by PlayerClassManager
            // we'll try to find them now, or they'll be found when PlayerAbilityManager.FindAbilitiesAfterClassLoad is called
            AttemptToFindAbilities();
            
            // only subscribe to input if this is our player
            if (IsOwner)
            {
                SubscribeToInputActions();
            }

            enchanterCharges.OnValueChanged += OnEnchanterChargesValueChanged;
        }
        
        // called by PlayerClassManager to clear ability references when switching classes
        public void ClearAbilityReferences()
        {
            ability1 = null;
            ability2 = null;
            ability3 = null;
            // Debug.Log("PlayerAbilityManager: Cleared ability references");
        }
        
        // called by PlayerClassManager after abilities are instantiated
        public void FindAbilitiesAfterClassLoad()
        {
            AttemptToFindAbilities();
            
            // initialize them if we just found them
            if (ability1 != null && ability1.gameObject.activeSelf && ability1.enabled)
            {
                ability1.Initialize(playerController, playerStats, this);
            }
            if (ability2 != null && ability2.gameObject.activeSelf && ability2.enabled)
            {
                ability2.Initialize(playerController, playerStats, this);
            }
            if (ability3 != null && ability3.gameObject.activeSelf && ability3.enabled)
            {
                ability3.Initialize(playerController, playerStats, this);
            }
        }
        
        private void AttemptToFindAbilities()
        {
            // log all children to debug
            // Debug.Log($"PlayerAbilityManager.AttemptToFindAbilities: Player has {transform.childCount} children");
            foreach (Transform child in transform)
            {
                // Debug.Log($"  - Child: {child.name}, Components: {string.Join(", ", child.GetComponents<Component>().Select(c => c.GetType().Name))}");
            }
            
            // find abilities by name (set by PlayerClassManager: "Ability1", "Ability2", "Ability3")
            // this approach is generic and works with any class system
            if (ability1 == null) ability1 = FindAbilityBySlotName("Ability1");
            if (ability2 == null) ability2 = FindAbilityBySlotName("Ability2");
            if (ability3 == null) ability3 = FindAbilityBySlotName("Ability3");
            
            // Debug.Log($"PlayerAbilityManager.AttemptToFindAbilities: Found abilities - Q:{ability1 != null}, E:{ability2 != null}, R:{ability3 != null}");
            
            // initialize abilities only if not already done
            AbilityBase[] abilities = { ability1, ability2, ability3 };
            foreach (var ability in abilities)
            {
                if (ability != null && !ability.IsInitialized)
                {
                    ability.Initialize(playerController, playerStats, this);
                }
            }
        }
        
        // find an ability by its slot name (generic approach, works with any class)
        private AbilityBase FindAbilityBySlotName(string slotName)
        {
            foreach (Transform child in transform)
            {
                if (child.name == slotName)
                {
                    var ability = child.GetComponent<AbilityBase>();
                    if (ability != null)
                    {
                        // Debug.Log($"PlayerAbilityManager.FindAbilityBySlotName: Found {slotName} with component {ability.GetType().Name}");
                        return ability;
                    }
                    else
                    {
                        Debug.LogWarning($"PlayerAbilityManager.FindAbilityBySlotName: Found child '{slotName}' but it has no AbilityBase component!");
                    }
                }
            }
            
            Debug.LogWarning($"PlayerAbilityManager.FindAbilityBySlotName: Could not find ability slot '{slotName}'");
            return null;
        }

        private void SubscribeToInputActions()
        {
            if (inputActions == null)
            {
                Debug.LogError("PlayerAbilityManager: inputActions is null!");
                return;
            }
            
            // Debug.Log("PlayerAbilityManager: Subscribing to input actions");

            inputActions.Enable();
            
            // verify actions exist before subscribing
            if (inputActions.Player.Ability1 == null)
            {
                Debug.LogError("PlayerAbilityManager: Ability1 action not found!");
            }
            else
            {
                inputActions.Player.Ability1.performed += OnAbility1Pressed;
                inputActions.Player.Ability1.canceled += OnAbility1Released;
                // Debug.Log("PlayerAbilityManager: Subscribed to Ability1 (Q)");
            }
            
            if (inputActions.Player.Ability2 == null)
            {
                Debug.LogError("PlayerAbilityManager: Ability2 action not found!");
            }
            else
            {
                inputActions.Player.Ability2.performed += OnAbility2Pressed;
                // Debug.Log("PlayerAbilityManager: Subscribed to Ability2 (E)");
            }
            
            if (inputActions.Player.Ability3 == null)
            {
                Debug.LogError("PlayerAbilityManager: Ability3 action not found!");
            }
            else
            {
                inputActions.Player.Ability3.performed += OnAbility3Pressed;
                // Debug.Log("PlayerAbilityManager: Subscribed to Ability3 (R)");
            }
        }

        private void OnEnable()
        {
            // input subscription now happens in OnNetworkSpawn for better timing
        }

        private void OnDisable()
        {
            if (inputActions == null) return;

            if (inputActions.Player.Ability1 != null)
                inputActions.Player.Ability1.performed -= OnAbility1Pressed;
            if (inputActions.Player.Ability1 != null)
                inputActions.Player.Ability1.canceled -= OnAbility1Released;
            if (inputActions.Player.Ability2 != null)
                inputActions.Player.Ability2.performed -= OnAbility2Pressed;
            if (inputActions.Player.Ability3 != null)
                inputActions.Player.Ability3.performed -= OnAbility3Pressed;
                
            inputActions.Disable();
        }

        public override void OnNetworkDespawn()
        {
            // clean up input subscriptions on despawn
            OnDisable();
            enchanterCharges.OnValueChanged -= OnEnchanterChargesValueChanged;
            base.OnNetworkDespawn();
        }
        
        private void OnAbility1Pressed(InputAction.CallbackContext ctx)
        {
            // Debug.Log("Ability1 (Q) pressed!");
            TryUseAbility(AbilitySlot.Ability1);
        }

        private void OnAbility1Released(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;

            if (ability1 != null)
            {
                ability1.OnReleased();
            }
        }
        
        private void OnAbility2Pressed(InputAction.CallbackContext ctx)
        {
            // Debug.Log("Ability2 (E) pressed!");
            TryUseAbility(AbilitySlot.Ability2);
        }
        
        private void OnAbility3Pressed(InputAction.CallbackContext ctx)
        {
            // Debug.Log("Ability3 (R) pressed!");
            TryUseAbility(AbilitySlot.Ability3);
        }

        private void Update()
        {
            if (!IsServer) return;

            // tick down cooldowns on server
            if (ability1Cooldown.Value > 0f)
            {
                ability1Cooldown.Value = Mathf.Max(0f, ability1Cooldown.Value - Time.deltaTime);
            }
            if (ability2Cooldown.Value > 0f)
            {
                ability2Cooldown.Value = Mathf.Max(0f, ability2Cooldown.Value - Time.deltaTime);
            }
            if (ability3Cooldown.Value > 0f)
            {
                ability3Cooldown.Value = Mathf.Max(0f, ability3Cooldown.Value - Time.deltaTime);
            }

            if (enchanterCharges.Value > 0)
            {
                _enchanterChargeTimer += Time.deltaTime;
                if (_enchanterChargeTimer >= enchanterChargeDecaySeconds)
                {
                    enchanterCharges.Value = 0;
                    _enchanterChargeTimer = 0f;
                }
            }
            else
            {
                _enchanterChargeTimer = 0f;
            }
        }

        public void TryUseAbility(AbilitySlot slot)
        {
            if (!IsOwner) return;
            
            Debug.Log($"[DebugAbility] TryUseAbility called for slot {slot}");

            // check input blocking conditions
            if (PauseMenu.GameIsPaused || Category5.DebugTools.DebugMenuUI.IsMenuOpen)
            {
                Debug.Log($"[DebugAbility] Blocked: Menu open. GamePaused={PauseMenu.GameIsPaused}, MenuOpen={Category5.DebugTools.DebugMenuUI.IsMenuOpen}");
                return;
            }

            // disable ability usage in homebase
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Homebase")
            {
                return; 
            }
            if (Category5.Core.GameFlowManager.Instance != null && Category5.Core.GameFlowManager.Instance.CurrentPhase.Value == Category5.Core.GamePhase.PowerUpSelection)
{
                Debug.Log("[DebugAbility] Blocked: Power-up selection phase");
                return;
            }
            if (Category5.UI.BossIntroUI.IntroIsPlaying)
            {
                Debug.Log("[DebugAbility] Blocked: Boss intro playing");
                return;
            }
            if (Category5.UI.ItemSelectionUI.IsSelectionUIActive)
            {
                Debug.Log("[DebugAbility] Blocked: Item selection active");
                return;
            }
            if (playerController.IsDead.Value)
            {
                Debug.Log("[DebugAbility] Blocked: Player dead");
                return;
            }
            if (playerController.IsWindRiding)
            {
                Debug.Log("[DebugAbility] Blocked: Wind riding");
                return;
            }
            if (playerController.IsRecallChanneling)
            {
                var recallController = playerController.GetComponent<RecallController>();
                if (recallController != null)
                    recallController.InterruptRecall();
                return;
            }
            if (playerCombat.IsCharging)
            {
                Debug.Log("[DebugAbility] Blocked: Currently charging");
                return;
            }
            if (IsExecutingAbility)
            {
                Debug.Log("[DebugAbility] Blocked: Already executing ability");
                return;
            }

            // skill tree ultimate lock: block R ability if not unlocked
            if (slot == AbilitySlot.Ability3 && _ultimateLockManager != null && !_ultimateLockManager.IsUnlocked)
            {
                Debug.Log("[DebugAbility] Blocked: Ultimate not unlocked in skill tree");
                return;
            }
            
            // Debug.Log($"  -> Requesting ability {slot} from server");
            
            AbilityBase ability = GetAbility(slot);

            if (ability == null)
            {
                Debug.LogWarning($"[DebugAbility] No ability assigned for slot {slot}!");
                return;
            }

            // validate cooldown locally
            NetworkVariable<float> cooldown = GetCooldown(slot);
            if (ability.UsesManagerCooldownGate && cooldown.Value > 0f)
            {
                Debug.Log($"[DebugAbility] Blocked: Ability on cooldown for {cooldown.Value}s more");
                return;
            }
            
            if (!ability.CanUse())
            {
                Debug.Log($"[DebugAbility] Blocked: Ability CanUse check failed. Mana={playerController.CurrentMana.Value}/{playerController.MaxMana}");
                return;
            }
            
            Debug.Log($"[DebugAbility] Executing ability {slot}!");

            // cancel sprint before executing ability
            if (playerController != null)
            {
                playerController.CancelSprint();
            }
            
            // execute locally on the owner (client has the abilities instantiated)
            IsExecutingAbility = true;
            try
            {
                ability.Execute();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Exception while executing ability {slot}: {ex}");
            }
            finally
            {
                IsExecutingAbility = false;
            }
            
            // consume mana if ability has a cost
            if (ability.ConsumeCostOnExecute)
            {
                if (ability.Data.consumesAllMana)
                    playerController.RequestConsumeAllManaServerRpc();
                else if (ability.Data.manaCost > 0)
                    playerController.RequestConsumeManaServerRpc(ability.Data.manaCost);
            }
            
            // send request to server to set cooldown on NetworkVariable
            if (ability.StartCooldownOnExecute)
            {
                RequestSetAbilityCooldownServerRpc(slot, ability.Data.cooldownDuration);
            }
        }

        [Rpc(SendTo.Server)]
        public void RequestSetAbilityCooldownServerRpc(AbilitySlot slot, float cooldownDuration)
        {
            // server finds the correct player's ability manager by OwnerClientId
            // and sets the cooldown on the NetworkVariable
            NetworkVariable<float> cooldown = GetCooldown(slot);
            cooldown.Value = cooldownDuration;
            
            // notify all clients about the cooldown change
            NotifyCooldownChangedClientRpc(slot, cooldownDuration, cooldownDuration);
        }

        [Rpc(SendTo.Everyone)]
        private void NotifyCooldownChangedClientRpc(AbilitySlot slot, float current, float max)
        {
            // fire event for all clients for UI updates
            OnCooldownChanged?.Invoke(this, slot, current, max);
        }

        private void OnEnchanterChargesValueChanged(int previous, int current)
        {
            OnEnchanterChargesChanged?.Invoke(this, current, maxEnchanterCharges);
        }

        private AbilityBase GetAbility(AbilitySlot slot)
        {
            return slot switch
            {
                AbilitySlot.Ability1 => ability1,
                AbilitySlot.Ability2 => ability2,
                AbilitySlot.Ability3 => ability3,
                _ => null
            };
        }

        // RYLAN CODE - First attempt at resetting cooldown
        public void ResetAbilityCooldown(AbilitySlot slot)
        {
            if (!IsServer)
            {
                ResetAbilityCooldownServerRpc(slot);
                return;
            }

            var cooldown = GetCooldown(slot);
            AbilityBase ability = GetAbility(slot);

            float max = ability.Data.cooldownDuration;
            cooldown.Value = 0f;

            NotifyCooldownChangedClientRpc(slot, 0f, max);

        }
        //RYLAN CODE - Server RPC cooldown reset


        [Rpc(SendTo.Server)]
        public void ResetAbilityCooldownServerRpc(AbilitySlot slot)
        {
            NetworkVariable<float> cooldown = GetCooldown(slot);
            float cdValue = cooldown.Value;
            if (cooldown != null)
            {
                cooldown.Value = 0f;
                AbilityBase ability = GetAbility(slot);

                float max = ability.Data.cooldownDuration;

                // Debug.Log($"Cooldown for {slot} reset on server for client {OwnerClientId}");
                
                // notify clients about cooldown reset
                NotifyCooldownChangedClientRpc(slot, 0f, max);
            }
            else
            {
                Debug.LogError($"PlayerAbilityManager: Invalid slot {slot} for cooldown reset");
            }
        }

        private NetworkVariable<float> GetCooldown(AbilitySlot slot)
        {
            return slot switch
            {
                AbilitySlot.Ability1 => ability1Cooldown,
                AbilitySlot.Ability2 => ability2Cooldown,
                AbilitySlot.Ability3 => ability3Cooldown,
                _ => null
            };
        }

        public void ApplyAbilityCostAndCooldown(AbilitySlot slot, AbilityBase ability)
        {
            if (!IsOwner) return;
            if (ability == null || ability.Data == null) return;

            if (ability.Data.consumesAllMana)
            {
                playerController.RequestConsumeAllManaServerRpc();
            }
            else if (ability.Data.manaCost > 0)
            {
                playerController.RequestConsumeManaServerRpc(ability.Data.manaCost);
            }

            RequestSetAbilityCooldownServerRpc(slot, ability.Data.cooldownDuration);
        }

        public void SetAbilityCooldownDisplay(AbilitySlot slot, float cooldownDuration)
        {
            if (!IsOwner) return;

            RequestSetAbilityCooldownServerRpc(slot, cooldownDuration);
        }

        public int GetEnchanterCharges()
        {
            return enchanterCharges.Value;
        }

        public int GetMaxEnchanterCharges()
        {
            return maxEnchanterCharges;
        }

        public void AddEnchanterCharges(int amount)
        {
            if (!IsServer) return;
            if (amount <= 0) return;

            int newValue = Mathf.Clamp(enchanterCharges.Value + amount, 0, maxEnchanterCharges);
            if (newValue != enchanterCharges.Value)
            {
                enchanterCharges.Value = newValue;
            }

            _enchanterChargeTimer = 0f;
        }

        public int ConsumeAllEnchanterCharges()
        {
            if (!IsServer) return 0;

            int consumed = enchanterCharges.Value;
            enchanterCharges.Value = 0;
            _enchanterChargeTimer = 0f;
            return consumed;
        }

        // public getters for ui
        public AbilityBase GetAbility1() => ability1;
        public AbilityBase GetAbility2() => ability2;
        public AbilityBase GetAbility3() => ability3;

        [Rpc(SendTo.Server)]
        public void RequestSpawnNetworkProjectileServerRpc(Vector3 position, Vector3 direction, float damageMultiplier)
        {
            // server spawns a piercing projectile for critshot ability
            // reads ownerClientId from OwnerClientId (this RPC executes in the context of the calling player's ability manager)
            
            if (critshotArrowData == null)
            {
                Debug.LogError("PlayerAbilityManager: critshotArrowData is not assigned!");
                return;
            }
            
            if (critshotArrowData.ProjectilePrefab == null)
            {
                Debug.LogError("PlayerAbilityManager: ProjectilePrefab is null!");
                return;
            }
            
            if (playerStats == null)
            {
                Debug.LogError("PlayerAbilityManager: playerStats is null!");
                return;
            }
            
            // instantiate projectile
            GameObject projectileObj = Instantiate(critshotArrowData.ProjectilePrefab, position, Quaternion.LookRotation(direction));
            NetworkObject netObj = projectileObj.GetComponent<NetworkObject>();
            NetworkedProjectile projectile = projectileObj.GetComponent<NetworkedProjectile>();
            
            if (netObj == null || projectile == null)
            {
                Debug.LogError("PlayerAbilityManager: Arrow prefab missing NetworkObject or NetworkedProjectile component!");
                Destroy(projectileObj);
                return;
            }
            
            // initialize with piercing behavior using OwnerClientId from this manager's context
            projectile.InitializePiercing(
                critshotArrowData,
                OwnerClientId,
                playerStats,
                damageMultiplier,
                ignoreEnemies: true,
                ignoreEnvironment: true
            );
            
            // spawn on network
            netObj.Spawn();
            
            // Debug.Log($"Critshot fired for client {OwnerClientId}! Piercing arrow with {damageMultiplier}x damage!");
        }

        [Rpc(SendTo.Server)]
        public void SpawnRangerEArrowServerRpc(Vector3 position, Vector3 direction, float damageCoefficient,
            float arrowSpeed, float arrowLifetime, float zoneRadius, float zoneDuration,
            float tickInterval, float slowMultiplier)
        {
            if (rangerEArrowPrefab == null)
            {
                Debug.LogError("PlayerAbilityManager: rangerEArrowPrefab is not assigned!");
                return;
            }

            if (rangerEZonePrefab == null)
            {
                Debug.LogError("PlayerAbilityManager: rangerEZonePrefab is not assigned!");
                return;
            }

            if (playerStats == null)
            {
                Debug.LogError("PlayerAbilityManager: playerStats is null!");
                return;
            }

            GameObject obj = Instantiate(rangerEArrowPrefab, position, Quaternion.LookRotation(direction));
            NetworkObject netObj = obj.GetComponent<NetworkObject>();
            RangerEArrow arrow = obj.GetComponent<RangerEArrow>();

            if (netObj == null || arrow == null)
            {
                Debug.LogError("PlayerAbilityManager: ranger e arrow prefab missing NetworkObject or RangerEArrow component!");
                Destroy(obj);
                return;
            }

            arrow.Initialize(OwnerClientId, playerStats, rangerEZonePrefab, damageCoefficient, arrowSpeed,
                arrowLifetime, zoneRadius, zoneDuration, tickInterval, slowMultiplier);

            netObj.Spawn();
        }

        [Rpc(SendTo.Server)]
        public void ExecuteFighterQSlamGroundedServerRpc(Vector3 playerPos, Vector3 forward, float damageCoefficient,
            float boxWidth, float boxHeight, float boxDepth, float boxForwardOffset,
            float launchForceUp, float launchForceForward, int enemyLayerMask)
        {
            if (!IsServer) return;

            int adjustedDamage = playerStats != null ? playerStats.CalculateDamage(damageCoefficient).damage : Mathf.RoundToInt(damageCoefficient * 100f);

            Vector3 boxCenter = playerPos + forward * boxForwardOffset + Vector3.up * (boxHeight * 0.5f);
            Quaternion rotation = forward != Vector3.zero ? Quaternion.LookRotation(forward) : Quaternion.identity;
            Vector3 halfExtents = new Vector3(boxWidth * 0.5f, boxHeight * 0.5f, boxDepth * 0.5f);

            Collider[] hits = Physics.OverlapBox(boxCenter, halfExtents, rotation, enemyLayerMask, QueryTriggerInteraction.Ignore);
            HashSet<int> processed = new HashSet<int>();

            foreach (Collider col in hits)
            {
                EnemyBase enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    if (!processed.Add(enemy.GetInstanceID())) continue;
                    enemy.TakeDamage(adjustedDamage);
                    enemy.ApplyLaunch(forward * launchForceForward + Vector3.up * launchForceUp);
                    ShowFighterDamageNumberClientRpc(adjustedDamage, enemy.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    continue;
                }

                BossBase boss = col.GetComponentInParent<BossBase>();
                if (boss != null && processed.Add(boss.GetInstanceID()))
                {
                    boss.TakeDamage(adjustedDamage);
                    ShowFighterDamageNumberClientRpc(adjustedDamage, boss.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                }
            }

            TriggerFighterQSlamGroundedClientRpc(playerPos);
        }

        [Rpc(SendTo.Server)]
        public void ExecuteFighterQSlamAirborneServerRpc(Vector3 playerPos, Vector3 forward, float damageCoefficient,
            float sphereRadius, Vector3 sphereOffset, float launchForceUp,
            float selfLaunchUp, float selfLaunchForward, int enemyLayerMask)
        {
            if (!IsServer) return;

            int adjustedDamage = playerStats != null ? playerStats.CalculateDamage(damageCoefficient).damage : Mathf.RoundToInt(damageCoefficient * 100f);

            Vector3 sphereCenter = playerPos + sphereOffset;
            Collider[] hits = Physics.OverlapSphere(sphereCenter, sphereRadius, enemyLayerMask, QueryTriggerInteraction.Ignore);
            HashSet<int> processed = new HashSet<int>();

            foreach (Collider col in hits)
            {
                EnemyBase enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    if (!processed.Add(enemy.GetInstanceID())) continue;
                    enemy.TakeDamage(adjustedDamage);
                    enemy.ApplyLaunch(Vector3.up * launchForceUp);
                    ShowFighterDamageNumberClientRpc(adjustedDamage, enemy.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    continue;
                }

                BossBase boss = col.GetComponentInParent<BossBase>();
                if (boss != null && processed.Add(boss.GetInstanceID()))
                {
                    boss.TakeDamage(adjustedDamage);
                    ShowFighterDamageNumberClientRpc(adjustedDamage, boss.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                }
            }

            // launch the player upward + forward, sent exclusively to the owning client
            TriggerFighterQAirborneSelfLaunchClientRpc(forward, selfLaunchUp, selfLaunchForward, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
            });

            TriggerFighterQSlamAirborneClientRpc(playerPos);
        }

        [ClientRpc]
        private void TriggerFighterQSlamGroundedClientRpc(Vector3 position)
        {
            FighterQ.InvokeSlamGrounded(position);
            if (IsOwner && HitFeedbackManager.Instance != null)
                HitFeedbackManager.Instance.TriggerHeavyHit(position);
        }

        [ClientRpc]
        private void TriggerFighterQSlamAirborneClientRpc(Vector3 position)
        {
            FighterQ.InvokeSlamAirborne(position);
            if (IsOwner && HitFeedbackManager.Instance != null)
                HitFeedbackManager.Instance.TriggerHeavyHit(position);
        }

        [ClientRpc]
        private void TriggerFighterQAirborneSelfLaunchClientRpc(Vector3 forward, float launchUp, float launchForward, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return;
            playerController.SetExternalVelocity(Vector3.up * launchUp + forward * launchForward);
        }

        [ClientRpc]
        private void ShowFighterDamageNumberClientRpc(int damage, Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            if (Category5.UI.UIManager.Instance != null)
                Category5.UI.UIManager.Instance.ShowDamageNumber(damage, position);
        }

        // =====================================
        // fighter e - magnetic grapple
        // =====================================

        [Rpc(SendTo.Server)]
        public void FireMagneticGrappleServerRpc(Vector3 spawnPosition, Vector3 aimDirection, float hookSpeed, float hookLifetime, float pullForce, string hookPrefabName)
        {
            if (!IsServer) return;

            // find the registered network prefab by name - the owner passes this from FighterE's inspector field
            // so all tunable data stays in FighterE with no ability-specific fields here
            GameObject hookPrefab = null;
            foreach (var entry in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
            {
                if (entry.Prefab != null && entry.Prefab.name == hookPrefabName)
                {
                    hookPrefab = entry.Prefab;
                    break;
                }
            }

            if (hookPrefab == null)
            {
                Debug.LogError($"PlayerAbilityManager: could not find registered network prefab named '{hookPrefabName}' - ensure the hook prefab is added to the NetworkManager prefab list");
                return;
            }

            var hookObj = Instantiate(hookPrefab, spawnPosition, Quaternion.LookRotation(aimDirection));
            var networkObj = hookObj.GetComponent<NetworkObject>();
            var hookProjectile = hookObj.GetComponent<HookProjectile>();

            if (networkObj == null || hookProjectile == null)
            {
                Debug.LogError("PlayerAbilityManager: hook prefab missing NetworkObject or HookProjectile");
                Destroy(hookObj);
                return;
            }

            networkObj.Spawn();
            hookProjectile.Initialize(playerController.NetworkObjectId, aimDirection, hookSpeed, hookLifetime, pullForce);

            TriggerHookFiredClientRpc(spawnPosition, aimDirection);
        }

        // called by HookProjectile when it hits a target (server only, not an rpc)
        public void OnHookHitTarget(Vector3 hitPosition, ulong targetNetworkObjectId, bool isBoss, float pullForce)
        {
            if (!IsServer) return;

            HandleFighterEHookHitServerSide(hitPosition, targetNetworkObjectId, isBoss, playerController.transform, pullForce);
        }

        // server-side pull logic called by FighterE.OnHookHitTargetFromProjectile
        public void HandleFighterEHookHitServerSide(Vector3 hitPosition, ulong targetNetworkObjectId, bool isBoss,
            Transform playerTransform, float pullForce)
        {
            if (!IsServer) return;

            TriggerHookHitClientRpc(hitPosition);

            if (isBoss)
            {
                if (playerController.IsDead.Value) return;

                if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.ContainsKey(targetNetworkObjectId))
                {
                    Debug.LogError("PlayerAbilityManager: HandleFighterEHookHitServerSide - boss NetworkObject not found");
                    return;
                }

                NotifyFighterEBossPullClientRpc(targetNetworkObjectId, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                });
            }
            else
            {
                if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out var netObj)) return;
                EnemyBase enemy = netObj.GetComponent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                    enemy.StartGrapple(playerTransform, pullForce);
            }
        }

        [ClientRpc]
        private void TriggerHookFiredClientRpc(Vector3 position, Vector3 direction)
        {
            FighterE.OnHookFireInvoke(position);
            if (IsOwner && HitFeedbackManager.Instance != null)
                HitFeedbackManager.Instance.TriggerLightHit(position);
        }

        [ClientRpc]
        private void TriggerHookHitClientRpc(Vector3 hitPosition)
        {
            FighterE.OnHookHitInvoke(hitPosition);
        }

        [ClientRpc]
        private void NotifyFighterEBossPullClientRpc(ulong bossNetworkObjectId, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return;
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(bossNetworkObjectId, out var netObj)) return;
            var fighterE = ability2 as FighterE;
            fighterE?.StartGrapplePull(netObj.transform);
        }

        // =====================================
        // fighter r - tempest engine
        // =====================================

        [Rpc(SendTo.Server)]
        public void ActivateTempestEngineServerRpc()
        {
            if (!IsServer) return;

            // reset q and e cooldowns so they are immediately usable again
            ability1Cooldown.Value = 0f;
            ability2Cooldown.Value = 0f;
            NotifyCooldownChangedClientRpc(AbilitySlot.Ability1, 0f, ability1?.Data?.cooldownDuration ?? 0f);
            NotifyCooldownChangedClientRpc(AbilitySlot.Ability2, 0f, ability2?.Data?.cooldownDuration ?? 0f);

            // notify owner to activate local ult state
            NotifyTempestActivatedClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
            });
        }

        [ClientRpc]
        private void NotifyTempestActivatedClientRpc(ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return;
            var fighterR = ability3 as FighterR;
            fighterR?.OnTempestActivated();
        }

        [Rpc(SendTo.Server)]
        public void ExecuteTempestBigMoveServerRpc(Vector3 playerPos, Vector3 forward, float damageCoefficient,
            float boxWidth, float boxHeight, float boxDepth, float boxForwardOffset, int enemyLayerMask, float cooldownDuration)
        {
            if (!IsServer) return;

            int adjustedDamage = playerStats != null ? playerStats.CalculateDamage(damageCoefficient).damage : Mathf.RoundToInt(damageCoefficient * 100f);

            Vector3 boxCenter = playerPos + forward * boxForwardOffset + Vector3.up * (boxHeight * 0.5f);
            Quaternion rotation = forward != Vector3.zero ? Quaternion.LookRotation(forward) : Quaternion.identity;
            Vector3 halfExtents = new Vector3(boxWidth * 0.5f, boxHeight * 0.5f, boxDepth * 0.5f);

            Collider[] hits = Physics.OverlapBox(boxCenter, halfExtents, rotation, enemyLayerMask, QueryTriggerInteraction.Ignore);
            HashSet<int> processed = new HashSet<int>();

            foreach (Collider col in hits)
            {
                EnemyBase enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    if (!processed.Add(enemy.GetInstanceID())) continue;
                    enemy.TakeDamage(adjustedDamage);
                    ShowFighterDamageNumberClientRpc(adjustedDamage, enemy.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    continue;
                }

                BossBase boss = col.GetComponentInParent<BossBase>();
                if (boss != null && processed.Add(boss.GetInstanceID()))
                {
                    boss.TakeDamage(adjustedDamage);
                    ShowFighterDamageNumberClientRpc(adjustedDamage, boss.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                }
            }

            // set r cooldown after big move executes
            ability3Cooldown.Value = cooldownDuration;
            NotifyCooldownChangedClientRpc(AbilitySlot.Ability3, cooldownDuration, cooldownDuration);

            TriggerTempestBigMoveClientRpc(playerPos, forward);
        }

        [Rpc(SendTo.Server)]
        public void EndTempestEngineServerRpc(float cooldownDuration)
        {
            if (!IsServer) return;

            // set r cooldown when ult expires without the second press
            ability3Cooldown.Value = cooldownDuration;
            NotifyCooldownChangedClientRpc(AbilitySlot.Ability3, cooldownDuration, cooldownDuration);

            TriggerTempestDeactivatedClientRpc(playerController.transform.position);
        }

        [ClientRpc]
        private void TriggerTempestBigMoveClientRpc(Vector3 position, Vector3 forward)
        {
            FighterR.OnTempestBigMoveInvoke(position, forward);
            if (IsOwner && HitFeedbackManager.Instance != null)
                HitFeedbackManager.Instance.TriggerHeavyHit(position);
        }

        [ClientRpc]
        private void TriggerTempestDeactivatedClientRpc(Vector3 position)
        {
            FighterR.OnTempestDeactivatedInvoke(position, false);
        }

        // =====================================
        // enchanter ability rpcs
        // =====================================

        [Rpc(SendTo.Server)]
        public void ExecuteEnchanterQDashServerRpc(Vector3 startPosition, Vector3 direction, float dashDistance,
            float damageCoefficient, float hitRadius, int enemyLayerMask)
        {
            if (!IsServer) return;

            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }

            direction.y = 0f;
            direction.Normalize();

            Vector3 endPosition = startPosition + direction * dashDistance;

            int adjustedDamage = playerStats != null ? playerStats.CalculateDamage(damageCoefficient).damage : Mathf.RoundToInt(damageCoefficient * 100f);

            TriggerEnchanterQDashClientRpc(startPosition, direction, dashDistance);

            Collider[] hitColliders = enemyLayerMask == 0
                ? Physics.OverlapCapsule(startPosition, endPosition, hitRadius)
                : Physics.OverlapCapsule(startPosition, endPosition, hitRadius, enemyLayerMask);
            var hitTargets = new HashSet<int>();
            int hits = 0;

            foreach (Collider collider in hitColliders)
            {
                EnemyBase enemy = collider.GetComponentInParent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    int id = enemy.GetInstanceID();
                    if (!hitTargets.Add(id)) continue;

                    enemy.TakeDamage(adjustedDamage);
                    ShowEnchanterQDamageNumberClientRpc(adjustedDamage, enemy.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    hits++;
                    continue;
                }

                BossBase boss = collider.GetComponentInParent<BossBase>();
                if (boss != null)
                {
                    int id = boss.GetInstanceID();
                    if (!hitTargets.Add(id)) continue;

                    boss.TakeDamage(adjustedDamage);
                    ShowEnchanterQDamageNumberClientRpc(adjustedDamage, boss.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    hits++;
                }
            }

            if (hits > 0)
            {
                AddEnchanterCharges(hits);
                TriggerEnchanterQHitClientRpc(endPosition, hits);
            }
        }

        [ClientRpc]
        private void TriggerEnchanterQDashClientRpc(Vector3 startPosition, Vector3 direction, float dashDistance)
        {
            EnchanterQ.InvokeDashStarted(startPosition, direction, dashDistance);
        }

        [ClientRpc]
        private void TriggerEnchanterQHitClientRpc(Vector3 position, int hitCount)
        {
            EnchanterQ.InvokeDashHit(position, hitCount);

            if (IsOwner && HitFeedbackManager.Instance != null)
            {
                HitFeedbackManager.Instance.TriggerLightHit(position);
            }
        }

        [ClientRpc]
        private void ShowEnchanterQDamageNumberClientRpc(int damage, Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            if (Category5.UI.UIManager.Instance != null)
            {
                Category5.UI.UIManager.Instance.ShowDamageNumber(damage, position);
            }
        }

        [Rpc(SendTo.Server)]
        public void ExecuteAssassinQDashServerRpc(Vector3 startPosition, Vector3 direction, float dashDistance,
            float damageCoefficient, float hitRadius, int enemyLayerMask)
        {
            if (!IsServer) return;

            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }

            direction.y = 0f;
            direction.Normalize();

            Vector3 endPosition = startPosition + direction * dashDistance;
            int adjustedDamage = playerStats != null ? playerStats.CalculateDamage(damageCoefficient).damage : Mathf.RoundToInt(damageCoefficient * 100f);

            TriggerAssassinQDashClientRpc(startPosition, direction, dashDistance);

            Collider[] hitColliders = enemyLayerMask == 0
                ? Physics.OverlapCapsule(startPosition, endPosition, hitRadius)
                : Physics.OverlapCapsule(startPosition, endPosition, hitRadius, enemyLayerMask);

            var hitTargets = new HashSet<int>();
            int hits = 0;

            foreach (Collider collider in hitColliders)
            {
                EnemyBase enemy = collider.GetComponentInParent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    int id = enemy.GetInstanceID();
                    if (!hitTargets.Add(id)) continue;

                    enemy.TakeDamage(adjustedDamage);
                    ShowAssassinQDamageNumberClientRpc(adjustedDamage, enemy.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    hits++;
                    continue;
                }

                BossBase boss = collider.GetComponentInParent<BossBase>();
                if (boss != null)
                {
                    int id = boss.GetInstanceID();
                    if (!hitTargets.Add(id)) continue;

                    boss.TakeDamage(adjustedDamage);
                    ShowAssassinQDamageNumberClientRpc(adjustedDamage, boss.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    hits++;
                }
            }

            if (hits > 0)
            {
                TriggerAssassinQHitClientRpc(endPosition, hits);
            }
        }

        [ClientRpc]
        private void TriggerAssassinQDashClientRpc(Vector3 startPosition, Vector3 direction, float dashDistance)
        {
            AssassinQ.InvokeDashStarted(startPosition, direction, dashDistance);
        }

        [ClientRpc]
        private void TriggerAssassinQHitClientRpc(Vector3 position, int hitCount)
        {
            AssassinQ.InvokeDashHit(position, hitCount);

            if (IsOwner)
            {
                if (ability1 is AssassinQ assassinQ)
                {
                    assassinQ.OnDashHitResolved(hitCount);
                }

                if (HitFeedbackManager.Instance != null)
                {
                    HitFeedbackManager.Instance.TriggerLightHit(position);
                }
            }
        }

        [ClientRpc]
        private void ShowAssassinQDamageNumberClientRpc(int damage, Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            if (Category5.UI.UIManager.Instance != null)
            {
                Category5.UI.UIManager.Instance.ShowDamageNumber(damage, position);
            }
        }

        // =====================================
        // assassin ability rpcs
        // =====================================
        [Rpc(SendTo.Server)]
        public void TriggerAssassinEWhirlwindStartServerRpc(Vector3 startPosition, Vector3 direction, float hitRadius)
        {
            // server triggers the start of the whirlwind dash for all clients
            if (!IsServer) return;

            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }

            direction.y = 0f;
            direction.Normalize();

            TriggerAssassinEWhirlwindStartClientRpc(startPosition, direction, hitRadius);
        }

        [ClientRpc]
        private void TriggerAssassinEWhirlwindStartClientRpc(Vector3 startPosition, Vector3 direction, float hitRadius)
        {
            AssassinE.InvokeWhirlwindStarted(startPosition, direction, hitRadius);
        }

        [Rpc(SendTo.Server)]
        public void ExecuteAssassinEWhirlwindServerRpc(Vector3 startPosition, Vector3 direction, float dashDistance,
            float damageCoefficient, float hitRadius, int enemyLayerMask)
        {
            if (!IsServer) return;

            int adjustedDamage = playerStats != null ? playerStats.CalculateDamage(damageCoefficient).damage : Mathf.RoundToInt(damageCoefficient * 100f);

            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }

            direction.y = 0f;
            direction.Normalize();

            Vector3 hitPosition = startPosition + direction * dashDistance;
            Collider[] hitColliders = enemyLayerMask == 0
                ? Physics.OverlapCapsule(startPosition, hitPosition, hitRadius)
                : Physics.OverlapCapsule(startPosition, hitPosition, hitRadius, enemyLayerMask);

            var hitTargets = new HashSet<int>();
            int hits = 0;

            foreach (Collider collider in hitColliders)
            {
                EnemyBase enemy = collider.GetComponentInParent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    int id = enemy.GetInstanceID();
                    if (!hitTargets.Add(id)) continue;

                    enemy.TakeDamage(adjustedDamage);
                    ShowAssassinEDamageNumberClientRpc(adjustedDamage, enemy.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    hits++;
                    continue;
                }

                BossBase boss = collider.GetComponentInParent<BossBase>();
                if (boss != null)
                {
                    int id = boss.GetInstanceID();
                    if (!hitTargets.Add(id)) continue;

                    boss.TakeDamage(adjustedDamage);
                    ShowAssassinEDamageNumberClientRpc(adjustedDamage, boss.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    hits++;
                }
            }

            TriggerAssassinEWhirlwindClientRpc(hitPosition, hits);
        }

        [ClientRpc]
        private void TriggerAssassinEWhirlwindClientRpc(Vector3 position, int hitCount)
        {
            AssassinE.InvokeWhirlwindHit(position, hitCount);

            if (IsOwner && hitCount > 0 && HitFeedbackManager.Instance != null)
            {
                HitFeedbackManager.Instance.TriggerLightHit(position);
            }
        }

        [ClientRpc]
        private void ShowAssassinEDamageNumberClientRpc(int damage, Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            if (Category5.UI.UIManager.Instance != null)
            {
                Category5.UI.UIManager.Instance.ShowDamageNumber(damage, position);
            }
        }

        [Rpc(SendTo.Server)]
        public void ExecuteAssassinRConvergenceServerRpc(Vector3 startPosition, Vector3 direction, float dashDistance,
            float damageCoefficient, float hitRadius, int enemyLayerMask)
        {
            if (!IsServer) return;

            int adjustedDamage = playerStats != null ? playerStats.CalculateDamage(damageCoefficient).damage : Mathf.RoundToInt(damageCoefficient * 100f);

            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }

            direction.y = 0f;
            direction.Normalize();

            Vector3 hitPosition = startPosition + direction * dashDistance;
            Collider[] dashColliders = enemyLayerMask == 0
                ? Physics.OverlapCapsule(startPosition, hitPosition, hitRadius)
                : Physics.OverlapCapsule(startPosition, hitPosition, hitRadius, enemyLayerMask);

            Collider[] explosionColliders = enemyLayerMask == 0
                ? Physics.OverlapSphere(hitPosition, hitRadius)
                : Physics.OverlapSphere(hitPosition, hitRadius, enemyLayerMask);

            var hitTargets = new HashSet<int>();
            int hits = 0;

            foreach (Collider collider in dashColliders)
            {
                EnemyBase enemy = collider.GetComponentInParent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    int id = enemy.GetInstanceID();
                    if (!hitTargets.Add(id)) continue;

                    enemy.TakeDamage(adjustedDamage);
                    ShowAssassinRDamageNumberClientRpc(adjustedDamage, enemy.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    hits++;
                    continue;
                }

                BossBase boss = collider.GetComponentInParent<BossBase>();
                if (boss != null)
                {
                    int id = boss.GetInstanceID();
                    if (!hitTargets.Add(id)) continue;

                    boss.TakeDamage(adjustedDamage);
                    ShowAssassinRDamageNumberClientRpc(adjustedDamage, boss.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    hits++;
                }
            }

            foreach (Collider collider in explosionColliders)
            {
                EnemyBase enemy = collider.GetComponentInParent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    int id = enemy.GetInstanceID();
                    if (!hitTargets.Add(id)) continue;

                    enemy.TakeDamage(adjustedDamage);
                    ShowAssassinRDamageNumberClientRpc(adjustedDamage, enemy.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    hits++;
                    continue;
                }

                BossBase boss = collider.GetComponentInParent<BossBase>();
                if (boss != null)
                {
                    int id = boss.GetInstanceID();
                    if (!hitTargets.Add(id)) continue;

                    boss.TakeDamage(adjustedDamage);
                    ShowAssassinRDamageNumberClientRpc(adjustedDamage, boss.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    hits++;
                }
            }

            TriggerAssassinRExplosionClientRpc(hitPosition, hits, hits >= 3);
        }

        [ClientRpc]
        private void TriggerAssassinRExplosionClientRpc(Vector3 position, int hitCount, bool resetCharges)
        {
            AssassinR.InvokeConvergenceExplosion(position, hitCount, resetCharges);

            if (IsOwner)
            {
                if (resetCharges && ability1 is AssassinQ assassinQ)
                {
                    assassinQ.ResetAllCharges();
                }

                if (hitCount > 0 && HitFeedbackManager.Instance != null)
                {
                    HitFeedbackManager.Instance.TriggerHeavyHit(position);
                }
            }
        }

        [ClientRpc]
        private void ShowAssassinRDamageNumberClientRpc(int damage, Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            if (Category5.UI.UIManager.Instance != null)
            {
                Category5.UI.UIManager.Instance.ShowDamageNumber(damage, position);
            }
        }

        [Rpc(SendTo.Server)]
        public void SpawnEnchanterHealBeaconServerRpc(Vector3 spawnPosition, Vector3 targetPosition,
            float healPerTick, float tickInterval, float baseDuration, float durationPerCharge, float radius)
        {
            if (!IsServer) return;

            if (healBeaconProjectilePrefab == null)
            {
                Debug.LogError("PlayerAbilityManager: healBeaconProjectilePrefab is not assigned!");
                return;
            }

            if (healBeaconZonePrefab == null)
            {
                Debug.LogError("PlayerAbilityManager: healBeaconZonePrefab is not assigned!");
                return;
            }

            int consumedCharges = ConsumeAllEnchanterCharges();
            float duration = baseDuration + (consumedCharges * durationPerCharge);

            GameObject obj = Instantiate(healBeaconProjectilePrefab, spawnPosition, Quaternion.identity);
            NetworkObject netObj = obj.GetComponent<NetworkObject>();
            HealBeaconProjectile projectile = obj.GetComponent<HealBeaconProjectile>();

            if (netObj == null || projectile == null)
            {
                Debug.LogError("PlayerAbilityManager: heal beacon projectile prefab missing NetworkObject or HealBeaconProjectile!");
                Destroy(obj);
                return;
            }

            projectile.Initialize(OwnerClientId, healBeaconZonePrefab, targetPosition, healPerTick, tickInterval, duration, radius);

            netObj.Spawn();

            Vector3 direction = (targetPosition - spawnPosition).normalized;
            TriggerEnchanterEThrownClientRpc(spawnPosition, direction);
        }

        [ClientRpc]
        private void TriggerEnchanterEThrownClientRpc(Vector3 spawnPosition, Vector3 direction)
        {
            EnchanterE.InvokeBeaconThrown(spawnPosition, direction);
        }

        [Rpc(SendTo.Server)]
        public void ExecuteEnchanterRBuffServerRpc(Vector3 position)
        {
            if (!IsServer) return;

            int consumedCharges = ConsumeAllEnchanterCharges();
            float radius = 5f + (consumedCharges * 1f);

            Collider[] hitColliders = enchanterAllyLayers.value != 0
                ? Physics.OverlapSphere(position, radius, enchanterAllyLayers)
                : Physics.OverlapSphere(position, radius);
            int alliesBuffed = 0;
            var buffedTargets = new HashSet<int>();

            foreach (Collider collider in hitColliders)
            {
                PlayerController target = collider.GetComponentInParent<PlayerController>();
                if (target == null) continue;
                if (target.IsDead.Value) continue;

                PlayerStats targetStats = target.GetComponent<PlayerStats>();
                if (targetStats == null) continue;

                int targetId = target.GetInstanceID();
                if (!buffedTargets.Add(targetId)) continue;

                targetStats.ApplyTemporaryMultiplier("speed", 0.3f, 6f);
                targetStats.ApplyTemporaryMultiplier("attackSpeed", 0.3f, 6f);
                alliesBuffed++;
            }

            TriggerEnchanterRBuffClientRpc(position, radius, alliesBuffed);
        }

        [ClientRpc]
        private void TriggerEnchanterRBuffClientRpc(Vector3 position, float radius, int alliesBuffed)
        {
            EnchanterR.InvokeLightningStrike(position, radius, alliesBuffed);

            if (ability3 is EnchanterR enchanterR)
            {
                enchanterR.ShowDebugSphere(position, radius);
            }
        }

        // =====================================
        // elementalist ability rpcs
        // =====================================

        [Header("Enchanter Prefabs")]
        [SerializeField] private GameObject healBeaconProjectilePrefab;
        [SerializeField] private GameObject healBeaconZonePrefab;
        [SerializeField] private LayerMask enchanterAllyLayers;

        [Header("Elementalist Prefabs")]
        [SerializeField] private GameObject fireballPrefab;
        [SerializeField] private GameObject iceProjectilePrefab;
        [SerializeField] private GameObject blackHoleProjectilePrefab;
        [SerializeField] private GameObject blackHoleZonePrefab;

        [Rpc(SendTo.Server)]
        public void SpawnFireballServerRpc(Vector3 position, Vector3 direction, float damageCoefficient,
            float projectileSpeed, float projectileLifetime, float explosionRadius,
            float burnDmgPerTick, float burnTickInterval, float burnDuration)
        {
            if (fireballPrefab == null)
            {
                Debug.LogError("PlayerAbilityManager: fireballPrefab is not assigned!");
                return;
            }

            GameObject obj = Instantiate(fireballPrefab, position, Quaternion.LookRotation(direction));
            NetworkObject netObj = obj.GetComponent<NetworkObject>();
            FireballProjectile fireball = obj.GetComponent<FireballProjectile>();

            if (netObj == null || fireball == null)
            {
                Debug.LogError("PlayerAbilityManager: fireball prefab missing NetworkObject or FireballProjectile!");
                Destroy(obj);
                return;
            }

            fireball.Initialize(OwnerClientId, playerStats, damageCoefficient, projectileSpeed,
                projectileLifetime, explosionRadius, burnDmgPerTick, burnTickInterval, burnDuration);

            netObj.Spawn();
            // Debug.Log($"[Elementalist] fireball spawned for client {OwnerClientId}");
        }

        [Rpc(SendTo.Server)]
        public void SpawnIceProjectileServerRpc(Vector3 position, Vector3 direction, float damageCoefficient,
            float projectileSpeed, float projectileLifetime, float slowMultiplier, float slowDuration)
        {
            if (iceProjectilePrefab == null)
            {
                Debug.LogError("PlayerAbilityManager: iceProjectilePrefab is not assigned!");
                return;
            }

            GameObject obj = Instantiate(iceProjectilePrefab, position, Quaternion.LookRotation(direction));
            NetworkObject netObj = obj.GetComponent<NetworkObject>();
            IceProjectile ice = obj.GetComponent<IceProjectile>();

            if (netObj == null || ice == null)
            {
                Debug.LogError("PlayerAbilityManager: ice prefab missing NetworkObject or IceProjectile!");
                Destroy(obj);
                return;
            }

            ice.Initialize(OwnerClientId, playerStats, damageCoefficient, projectileSpeed,
                projectileLifetime, slowMultiplier, slowDuration);

            netObj.Spawn();
            // Debug.Log($"[Elementalist] ice projectile spawned for client {OwnerClientId}");
        }

        [Rpc(SendTo.Server)]
        public void ExecuteThunderArcServerRpc(Vector3 position, Vector3 forward, float damageCoefficient,
            float arcAngle, float arcRange, float knockbackForce, float stunDuration, float stunDelay, int enemyLayerMask)
        {
            if (!IsServer) return;

            int adjustedDamage = playerStats != null ? playerStats.CalculateDamage(damageCoefficient).damage : Mathf.RoundToInt(damageCoefficient * 100f);

            // Debug.Log($"[ElementalistE_Thunder Server] executing arc at {position}, damage={adjustedDamage}, range={arcRange}, angle={arcAngle}");

            // trigger vfx for all clients
            TriggerThunderArcVfxClientRpc(position, forward, arcRange, arcAngle);

            Collider[] hitColliders = Physics.OverlapSphere(position, arcRange, enemyLayerMask);
            int enemiesHit = 0;
            float halfAngle = arcAngle * 0.5f;

            foreach (Collider collider in hitColliders)
            {
                // check if target is within the cone angle
                Vector3 dirToTarget = (collider.transform.position - position).normalized;
                dirToTarget.y = 0; // ignore vertical difference
                Vector3 flatForward = forward;
                flatForward.y = 0;
                flatForward.Normalize();

                float angle = Vector3.Angle(flatForward, dirToTarget);
                if (angle > halfAngle) continue;

                // try enemy
                if (collider.TryGetComponent<EnemyBase>(out var enemy) && !enemy.IsDead)
                {
                    enemy.TakeDamage(adjustedDamage);

                    // knockback away from player
                    Vector3 knockback = dirToTarget * knockbackForce;
                    CharacterController enemyCC = enemy.GetComponent<CharacterController>();
                    if (enemyCC != null)
                    {
                        enemyCC.Move(knockback * Time.fixedDeltaTime);
                    }
                    else
                    {
                        enemy.transform.position += knockback * Time.fixedDeltaTime;
                    }

                    if (stunDuration > 0f)
                    {
                        StartCoroutine(ApplyStunAfterDelay(enemy, stunDuration, stunDelay));
                    }

                    ShowThunderDamageNumberClientRpc(adjustedDamage, enemy.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    enemiesHit++;
                }
                // try boss
                else if (collider.TryGetComponent<BossBase>(out var boss))
                {
                    boss.TakeDamage(adjustedDamage);
                    // bosses don't get stunned or knocked back by thunder arc

                    ShowThunderDamageNumberClientRpc(adjustedDamage, boss.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                    });
                    enemiesHit++;
                }
            }

            // Debug.Log($"[ElementalistE_Thunder Server] hit {enemiesHit} enemies");

            if (enemiesHit > 0)
            {
                TriggerThunderHitFeedbackClientRpc(position, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                });
            }
        }

        private IEnumerator ApplyStunAfterDelay(EnemyBase enemy, float duration, float delay)
        {
            if (enemy == null) yield break;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (enemy == null || enemy.IsDead) yield break;
            enemy.ApplyStun(duration);
        }

        [Rpc(SendTo.Everyone)]
        private void TriggerThunderArcVfxClientRpc(Vector3 position, Vector3 forward, float range, float angle)
        {
            ElementalistE_Thunder.InvokeThunderArcExecute(position, forward, range, angle);
        }

        [ClientRpc]
        private void ShowThunderDamageNumberClientRpc(int damage, Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            if (Category5.UI.UIManager.Instance != null)
            {
                Category5.UI.UIManager.Instance.ShowDamageNumber(damage, position);
            }
        }

        [ClientRpc]
        private void TriggerThunderHitFeedbackClientRpc(Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            if (HitFeedbackManager.Instance != null)
            {
                HitFeedbackManager.Instance.TriggerHeavyHit(position);
            }
        }

        [Rpc(SendTo.Server)]
        public void SpawnBlackHoleProjectileServerRpc(Vector3 position, Vector3 direction, float damageCoefficient,
            float projectileSpeed, float projectileLifetime, float pullRadius, float pullForce,
            float pullDuration, float pullStrengthRampUp, float explosionRadius)
        {
            if (blackHoleProjectilePrefab == null)
            {
                Debug.LogError("PlayerAbilityManager: blackHoleProjectilePrefab is not assigned!");
                return;
            }

            if (blackHoleZonePrefab == null)
            {
                Debug.LogError("PlayerAbilityManager: blackHoleZonePrefab is not assigned!");
                return;
            }

            GameObject obj = Instantiate(blackHoleProjectilePrefab, position, Quaternion.LookRotation(direction));
            NetworkObject netObj = obj.GetComponent<NetworkObject>();
            BlackHoleProjectile projectile = obj.GetComponent<BlackHoleProjectile>();

            if (netObj == null || projectile == null)
            {
                Debug.LogError("PlayerAbilityManager: black hole projectile prefab missing NetworkObject or BlackHoleProjectile!");
                Destroy(obj);
                return;
            }

            projectile.Initialize(OwnerClientId, playerStats, blackHoleZonePrefab, damageCoefficient,
                projectileSpeed, projectileLifetime, pullRadius, pullForce,
                pullDuration, pullStrengthRampUp, explosionRadius);

            netObj.Spawn();
            // Debug.Log($"[Elementalist] black hole projectile spawned for client {OwnerClientId} at {position}");
        }

    }
}