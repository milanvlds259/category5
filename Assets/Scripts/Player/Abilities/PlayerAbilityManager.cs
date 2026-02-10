using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Linq;
using Category5.Player;
using Category5.PowerUps;
using Category5.Items;
using Category5.UI;
using Category5.Audio;
using Category5.Enemies;
using Category5.Core;
using Category5.Boss;

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

        [Header("Cooldown Tracking")]
        public NetworkVariable<float> ability1Cooldown = new NetworkVariable<float>(0f);
        public NetworkVariable<float> ability2Cooldown = new NetworkVariable<float>(0f);
        public NetworkVariable<float> ability3Cooldown = new NetworkVariable<float>(0f);

        private PlayerController playerController;
        private PlayerStats playerStats;
        private PlayerCombat playerCombat;
        private InputSystem_Actions inputActions;

        // prevents multiple abilities from executing simultaneously
        public bool IsExecutingAbility { get; private set; }

        // events for ui updates - includes reference to source PlayerAbilityManager so UI can filter
        public static event Action<PlayerAbilityManager, AbilitySlot, float, float> OnCooldownChanged; // source, slot, current, max

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerStats = GetComponent<PlayerStats>();
            playerCombat = GetComponent<PlayerCombat>();
            inputActions = new InputSystem_Actions();
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
        }
        
        // called by PlayerClassManager to clear ability references when switching classes
        public void ClearAbilityReferences()
        {
            ability1 = null;
            ability2 = null;
            ability3 = null;
            Debug.Log("PlayerAbilityManager: Cleared ability references");
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
            Debug.Log($"PlayerAbilityManager.AttemptToFindAbilities: Player has {transform.childCount} children");
            foreach (Transform child in transform)
            {
                Debug.Log($"  - Child: {child.name}, Components: {string.Join(", ", child.GetComponents<Component>().Select(c => c.GetType().Name))}");
            }
            
            // find abilities by name (set by PlayerClassManager: "Ability1", "Ability2", "Ability3")
            // this approach is generic and works with any class system
            if (ability1 == null) ability1 = FindAbilityBySlotName("Ability1");
            if (ability2 == null) ability2 = FindAbilityBySlotName("Ability2");
            if (ability3 == null) ability3 = FindAbilityBySlotName("Ability3");
            
            Debug.Log($"PlayerAbilityManager.AttemptToFindAbilities: Found abilities - Q:{ability1 != null}, E:{ability2 != null}, R:{ability3 != null}");
            
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
                        Debug.Log($"PlayerAbilityManager.FindAbilityBySlotName: Found {slotName} with component {ability.GetType().Name}");
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
            
            Debug.Log("PlayerAbilityManager: Subscribing to input actions");

            inputActions.Enable();
            
            // verify actions exist before subscribing
            if (inputActions.Player.Ability1 == null)
            {
                Debug.LogError("PlayerAbilityManager: Ability1 action not found!");
            }
            else
            {
                inputActions.Player.Ability1.performed += OnAbility1Pressed;
                Debug.Log("PlayerAbilityManager: Subscribed to Ability1 (Q)");
            }
            
            if (inputActions.Player.Ability2 == null)
            {
                Debug.LogError("PlayerAbilityManager: Ability2 action not found!");
            }
            else
            {
                inputActions.Player.Ability2.performed += OnAbility2Pressed;
                Debug.Log("PlayerAbilityManager: Subscribed to Ability2 (E)");
            }
            
            if (inputActions.Player.Ability3 == null)
            {
                Debug.LogError("PlayerAbilityManager: Ability3 action not found!");
            }
            else
            {
                inputActions.Player.Ability3.performed += OnAbility3Pressed;
                Debug.Log("PlayerAbilityManager: Subscribed to Ability3 (R)");
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
            base.OnNetworkDespawn();
        }
        
        private void OnAbility1Pressed(InputAction.CallbackContext ctx)
        {
            Debug.Log("Ability1 (Q) pressed!");
            TryUseAbility(AbilitySlot.Ability1);
        }
        
        private void OnAbility2Pressed(InputAction.CallbackContext ctx)
        {
            Debug.Log("Ability2 (E) pressed!");
            TryUseAbility(AbilitySlot.Ability2);
        }
        
        private void OnAbility3Pressed(InputAction.CallbackContext ctx)
        {
            Debug.Log("Ability3 (R) pressed!");
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
        }

        public void TryUseAbility(AbilitySlot slot)
        {
            if (!IsOwner) return;
            
            Debug.Log($"TryUseAbility called for slot {slot}");

            // check input blocking conditions
            if (PauseMenu.GameIsPaused)
            {
                Debug.Log("  -> Blocked: Game paused");
                return;
            }
            if (Category5.Items.ItemManager.Instance != null && Category5.Items.ItemManager.Instance.CurrentPhase.Value == Category5.Core.GamePhase.PowerUpSelection)
            {
                Debug.Log("  -> Blocked: Power-up selection phase");
                return;
            }
            if (playerController.IsDead.Value)
            {
                Debug.Log("  -> Blocked: Player dead");
                return;
            }
            if (playerCombat.IsCharging)
            {
                Debug.Log("  -> Blocked: Currently charging");
                return;
            }
            if (IsExecutingAbility)
            {
                Debug.Log("  -> Blocked: Already executing ability");
                return;
            }
            
            Debug.Log($"  -> Requesting ability {slot} from server");
            
            // validate cooldown locally
            NetworkVariable<float> cooldown = GetCooldown(slot);
            AbilityBase ability = GetAbility(slot);
            
            if (cooldown.Value > 0f)
            {
                Debug.Log($"  -> Blocked: Ability on cooldown for {cooldown.Value}s more");
                return;
            }
            
            if (ability == null)
            {
                Debug.LogWarning($"PlayerAbilityManager: No ability assigned for slot {slot}!");
                return;
            }
            
            if (!ability.CanUse())
            {
                Debug.Log($"  -> Blocked: Ability CanUse check failed");
                return;
            }
            
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
            
            // send request to server to set cooldown on NetworkVariable
            RequestSetAbilityCooldownServerRpc(slot, ability.Data.cooldownDuration);
        }

        [Rpc(SendTo.Server)]
        private void RequestSetAbilityCooldownServerRpc(AbilitySlot slot, float cooldownDuration)
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

                Debug.Log($"Cooldown for {slot} reset on server for client {OwnerClientId}");
                
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
            
            Debug.Log($"Critshot fired for client {OwnerClientId}! Piercing arrow with {damageMultiplier}x damage!");
        }

        [Rpc(SendTo.Server)]
        public void ExecuteFighterQSmashServerRpc(Vector3 executePosition, int adjustedDamage, float aoeRadius, float stunDuration, int enemyLayerMask)
        {
            if (!IsServer) return;
            
            Debug.Log($"[FighterQ Server] Executing smash at {executePosition}, damage={adjustedDamage}, radius={aoeRadius}, layerMask={enemyLayerMask}");
            
            // always trigger execute telegraph/vfx (even with 0 hits)
            TriggerFighterQExecuteClientRpc(executePosition);
            
            // find enemies in aoe using layermask
            Collider[] hitColliders = Physics.OverlapSphere(executePosition, aoeRadius, enemyLayerMask);
            Debug.Log($"[FighterQ Server] Found {hitColliders.Length} colliders with layerMask");
            
            // also try without layermask to see all colliders
            Collider[] allColliders = Physics.OverlapSphere(executePosition, aoeRadius);
            Debug.Log($"[FighterQ Server] Found {allColliders.Length} total colliders (no mask)");
            
            foreach (Collider col in allColliders)
            {
                Debug.Log($"  - Collider: {col.gameObject.name}, Layer: {col.gameObject.layer} ({LayerMask.LayerToName(col.gameObject.layer)}), Has EnemyBase: {col.GetComponent<EnemyBase>() != null}, Has BossBase: {col.GetComponent<BossBase>() != null}");
            }
            
            int enemiesHit = 0;
            foreach (Collider collider in hitColliders)
            {
                // try enemy base first
                if (collider.TryGetComponent<EnemyBase>(out var enemy) && !enemy.IsDead)
                {
                    Debug.Log($"[FighterQ Server] Hitting enemy: {collider.gameObject.name}");
                    enemy.ApplyStun(stunDuration);
                    enemy.TakeDamage(adjustedDamage);
                    
                    // show damage number to the attacking player
                    ShowFighterQDamageNumberClientRpc(adjustedDamage, enemy.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { OwnerClientId }
                        }
                    });
                    
                    enemiesHit++;
                }
                // also check for boss base (bosses inherit from BossBase which implements IDamageable)
                else if (collider.TryGetComponent<BossBase>(out var boss))
                {
                    Debug.Log($"[FighterQ Server] Hitting boss: {collider.gameObject.name}");
                    boss.TakeDamage(adjustedDamage);
                    
                    // show damage number to the attacking player
                    ShowFighterQDamageNumberClientRpc(adjustedDamage, boss.transform.position, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { OwnerClientId }
                        }
                    });
                    
                    enemiesHit++;
                }
                
            }
            
            Debug.Log($"[FighterQ Server] Total enemies hit: {enemiesHit}");
            
            // notify clients for hit effects only if we hit something
            if (enemiesHit > 0)
            {
                TriggerFighterQHitClientRpc(executePosition, enemiesHit);


                //RYLAN CODE - reset cooldown for grapple on smash hit
                
                Debug.Log("cooldown reset!");
                ResetAbilityCooldown(AbilitySlot.Ability2);
                
            }
        }
        
        [Rpc(SendTo.Everyone)]
        private void TriggerFighterQExecuteClientRpc(Vector3 position)
        {
            // fire execute event for vfx/sfx (telegraph/impact)
            FighterQ.InvokeSmashExecute(position, 0);
            
            // temporary debug visualization - create a red sphere at impact location
            GameObject debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            debugSphere.transform.position = position;
            debugSphere.transform.localScale = Vector3.one * 10f; // 5m radius = 10m diameter
            
            // make it semi-transparent red
            Renderer renderer = debugSphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(1f, 0f, 0f, 0.3f); // red with alpha
                mat.SetFloat("_Mode", 3); // transparent rendering mode
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                renderer.material = mat;
            }
            
            // remove collider so it doesn't interfere with gameplay
            Collider col = debugSphere.GetComponent<Collider>();
            if (col != null) Destroy(col);
            
            // destroy after 1 second
            Destroy(debugSphere, 1f);
        }
        
        [Rpc(SendTo.Everyone)]
        private void TriggerFighterQHitClientRpc(Vector3 position, int enemiesHit)
        {
            // fire hit event for vfx/sfx
            FighterQ.InvokeSmashHit(position);
            
            // trigger hit feedback for the owner only
            if (IsOwner && HitFeedbackManager.Instance != null)
            {
                HitFeedbackManager.Instance.TriggerHeavyHit(position);
            }
        }
        
        [ClientRpc]
        private void ShowFighterQDamageNumberClientRpc(int damage, Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            // only the attacking player sees their damage numbers
            if (Category5.UI.UIManager.Instance != null)
            {
                Category5.UI.UIManager.Instance.ShowDamageNumber(damage, position);
            }
        }
        
        // =====================================
        // hook projectile callback for fighter e
        // =====================================
        
        // called by HookProjectile when it hits an enemy or boss
        // routes the callback to the FighterE ability (ability2)
        public void OnHookHitTarget(Vector3 hitPosition, ulong targetNetworkObjectId, bool isBoss)
        {
            Debug.Log($"PlayerAbilityManager: OnHookHitTarget called. IsServer: {IsServer}, ability2: {ability2 != null}");
            
            if (!IsServer) return;
            
            if (ability2 == null)
            {
                Debug.LogError("PlayerAbilityManager: ability2 is null, cannot route hook hit callback");
                return;
            }
            
            // check if ability2 is FighterE
            var fighterE = ability2 as FighterE;
            if (fighterE != null)
            {
                Debug.Log("PlayerAbilityManager: Routing to FighterE");
                fighterE.OnHookHitTargetFromProjectile(hitPosition, targetNetworkObjectId, isBoss);
            }
            else
            {
                Debug.LogWarning($"PlayerAbilityManager: ability2 is not FighterE, it's {ability2.GetType().Name}");
            }
        }

    }
}