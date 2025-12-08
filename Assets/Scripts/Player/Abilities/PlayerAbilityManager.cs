using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Linq;
using Category5.Player;
using Category5.PowerUps;
using Category5.UI;
using Category5.Audio;

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

        // events for ui updates
        public static event Action<AbilitySlot, float, float> OnCooldownChanged; // slot, current, max

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
            
            // try to find them if not already assigned
            if (ability1 == null) ability1 = GetComponentInChildren<QuickbowAbility>();
            if (ability2 == null) ability2 = GetComponentInChildren<SpiralbowAbility>();
            if (ability3 == null) ability3 = GetComponentInChildren<CritshotAbility>();
            
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
            if (PowerUpManager.Instance != null && PowerUpManager.Instance.CurrentPhase.Value == GamePhase.PowerUpSelection)
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
            
            Debug.Log($"  -> Executing ability {slot}");
            
            // get the ability and check cooldown locally
            AbilityBase ability = GetAbility(slot);
            NetworkVariable<float> cooldown = GetCooldown(slot);
            
            if (ability == null)
            {
                Debug.LogWarning($"PlayerAbilityManager: No ability assigned for slot {slot}!");
                return;
            }
            
            if (cooldown.Value > 0f)
            {
                Debug.Log($"  -> Blocked: Ability on cooldown for {cooldown.Value}s more");
                return;
            }
            
            if (!ability.CanUse())
            {
                Debug.Log($"  -> Blocked: Ability CanUse check failed");
                return;
            }
            
            // execute locally first - wrap in try-catch to ensure flag is always reset
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
                IsExecutingAbility = false; // always reset, even if exception occurs
            }
            
            // start cooldown
            cooldown.Value = ability.Data.cooldownDuration;
            
            // notify ui
            OnCooldownChanged?.Invoke(slot, cooldown.Value, ability.Data.cooldownDuration);
            
            // notify server to sync cooldown state
            SyncAbilityCooldownServerRpc(slot, cooldown.Value, ability.Data.cooldownDuration);
        }
        
        // removed ResetExecutingAbilityAfterDelay since cooldown prevents spam anyway

        [Rpc(SendTo.Server)]
        private void SyncAbilityCooldownServerRpc(AbilitySlot slot, float current, float max)
        {
            // server receives cooldown state and syncs to other clients via NetworkVariable
            NetworkVariable<float> cooldown = GetCooldown(slot);
            cooldown.Value = current;
            
            // notify all clients (including us) about the cooldown change
            NotifyCooldownChangedClientRpc(slot, current, max);
        }

        [Rpc(SendTo.Everyone)]
        private void NotifyCooldownChangedClientRpc(AbilitySlot slot, float current, float max)
        {
            // fire event for UI updates (only if not the owner, owner already did this)
            if (!IsOwner)
            {
                OnCooldownChanged?.Invoke(slot, current, max);
            }
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
    }
}
