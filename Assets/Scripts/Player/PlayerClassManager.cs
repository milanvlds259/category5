using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Category5.Core;

namespace Category5.Player
{
    // manages the player's selected class and loads the appropriate abilities
    public class PlayerClassManager : NetworkBehaviour
    {
        [Header("Class Selection")]
        public NetworkVariable<int> SelectedClassId = new NetworkVariable<int>(PlayerClass.NoClassId);
        
        private PlayerAbilityManager abilityManager;
        private PlayerCombat playerCombat;
        private PlayerStats playerStats;
        
        private int _offlineClassId = PlayerClass.NoClassId;
        private bool _isOffline = false;

        private void Awake()
{
            abilityManager = GetComponent<PlayerAbilityManager>();
            playerCombat = GetComponent<PlayerCombat>();
            playerStats = GetComponent<PlayerStats>();
        }
        
        private void Start()
        {
            // if we are the local player, subscribe to lobby changes to sync our class
            // this handles both offline hub and networked lobby selection
            LobbyManager.OnLobbyPlayersChanged += SyncWithLobby;
            
            // initial sync
            if (LobbyManager.Instance != null)
            {
                SyncWithLobby();
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            
            // always unsubscribe from static events to prevent memory leaks and MissingReferenceExceptions
            LobbyManager.OnLobbyPlayersChanged -= SyncWithLobby;
        }

        private void SyncWithLobby()
        {
            if (LobbyManager.Instance == null) return;
            
            // get our local ID (0 for offline if no network manager, otherwise LocalClientId)
            ulong localId = (NetworkManager.Singleton != null) ? NetworkManager.Singleton.LocalClientId : 0;
            
            // only sync if we are either offline or the owner of this networked object
            if (!IsSpawned || IsOwner)
            {
                int classId = LobbyManager.Instance.GetPlayerClassId(localId);
                
                if (IsSpawned && IsOwner)
                {
                    // networked mode: update the NetworkVariable via RPC if it differs
                    if (classId != SelectedClassId.Value && classId != PlayerClass.NoClassId)
                    {
                        RequestSetClassIdServerRpc(classId);
                    }
                }
                else if (!IsSpawned)
                {
                    // offline hub mode: load locally
                    if (classId != _offlineClassId)
                    {
                        _offlineClassId = classId;
                        LoadClassLocally(classId);
                    }
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            // Debug.Log($"PlayerClassManager.OnNetworkSpawn: IsServer={IsServer}, IsOwner={IsOwner}, SelectedClass={SelectedClass.Value}, OwnerClientId={OwnerClientId}");

            // subscribe to class changes
            SelectedClassId.OnValueChanged += OnSelectedClassChanged;
            
            // if owner, request the class selection from lobby or persistent selection
            if (IsOwner)
            {
                int classToLoad = SelectedClassId.Value;
                
                // check if LobbyManager has a selected class for this player
                if (LobbyManager.Instance != null)
                {
                    int lobbySelectedClass = LobbyManager.Instance.GetPlayerClassId(OwnerClientId);
                    // Debug.Log($"PlayerClassManager: Found lobby selection {lobbySelectedClass} for player {OwnerClientId}");
                    classToLoad = lobbySelectedClass;
                    
                    // request server to set the class from lobby
                    RequestSetClassIdServerRpc(classToLoad);
                }
                else
                {
                    // LobbyManager is gone (cleaned up during scene load), use persistent ClassSelectionManager
                    int persistentClass = ClassSelectionManager.GetClassId();
                    // Debug.Log($"PlayerClassManager: LobbyManager not found, using persistent ClassSelectionManager: {persistentClass}");
                    classToLoad = persistentClass;
                    
                    // if the class is different, request it via RPC
                    if (classToLoad != SelectedClassId.Value)
                    {
                        // Debug.Log($"PlayerClassManager: Owner {OwnerClientId} requested class {classToLoad}");
                        RequestSetClassIdServerRpc(classToLoad);
                    }
                    else
                    {
                        // class is already correct, load it directly (since OnValueChanged won't fire)
                        // Debug.Log($"PlayerClassManager: Owner {OwnerClientId} class already {classToLoad}, loading directly");
                        LoadClassLocally(classToLoad);
                    }
                }
            }
            else
            {
                // Debug.Log($"PlayerClassManager: Non-owner observing player {OwnerClientId} with class {SelectedClassId.Value}");
                // load class data expeditiously 
                LoadClassLocally(SelectedClassId.Value);
            }
        }
        
        private void OnSelectedClassChanged(int oldClass, int newClass)
        {
            // when class changes, reload abilities on all instances
            // Debug.Log($"PlayerClassManager.OnSelectedClassChanged: {oldClass} -> {newClass}");
            LoadClassLocally(newClass);
        }

        // client requests to set their class
        [Rpc(SendTo.Server)]
        public void RequestSetClassIdServerRpc(int classId)
        {
            if (!IsServer) return;
            
            // server updates the selected class (triggers OnValueChanged on all clients)
            SelectedClassId.Value = classId;
        }

        // load class and spawn its abilities (for the owner of this player)
        private void LoadClassLocally(int classId)
        {
            if (classId == PlayerClass.NoClassId)
            {
                // no class selected yet (e.g. player spawned before selecting in lobby)
                return;
            }

            // tell model manager to load the 3D model for this class
            var modelManager = GetComponent<PlayerModelManager>();
                modelManager.LoadModel(classId);
            
            PlayerClass classData = GetClassData(classId);
            if (classData == null)
            {
                Debug.LogError($"PlayerClassManager: No class data found for classId {classId}!");
                return;
            }
            
            // Debug.Log($"PlayerClassManager: Found class data {classData.className}");
            // Debug.Log($"  - Ability1Prefab: {(classData.ability1Prefab != null ? classData.ability1Prefab.name : "null")}");
            // Debug.Log($"  - Ability2Prefab: {(classData.ability2Prefab != null ? classData.ability2Prefab.name : "null")}");
            // Debug.Log($"  - Ability3Prefab: {(classData.ability3Prefab != null ? classData.ability3Prefab.name : "null")}");
            
            // clear existing abilities and reset ability manager references
            ClearAbilities();
            if (abilityManager != null)
            {
                abilityManager.ClearAbilityReferences();
            }

            // push class data to stats system so all base stats update
            if (playerStats != null)
            {
                playerStats.SetClassData(classData);
            }
            
            // push melee coefficients to combat system
            if (playerCombat != null)
            {
                playerCombat.SetMeleeCoefficients(classData.lightAttackCoefficient, classData.heavyAttackCoefficient);
                playerCombat.SetComboResetTime(classData.meleeComboResetTime);
            }

            // all instances need the correct combat class and coefficients for stat-dependent gameplay,
            // but only the owner should spawn local ability objects.
            if (playerCombat != null)
            {
                playerCombat.SetCombatClass(classData.combatClass);

                if (classData.combatClass == CombatClass.Ranged)
                {
                    playerCombat.SetArrowData(classData.basicAttackProjectile);
                }
            }

            // stop here for remote players - they don't need local ability objects or ability manager updates
            if (!IsOwner && !_isOffline)
            {
                return;
            }

            // spawn new abilities locally
            if (classData.ability1Prefab != null)
            {
                var abilityObj = Instantiate(classData.ability1Prefab, transform);
                abilityObj.name = "Ability1";
            }
            
            if (classData.ability2Prefab != null)
            {
                var abilityObj = Instantiate(classData.ability2Prefab, transform);
                abilityObj.name = "Ability2";
            }
            
            if (classData.ability3Prefab != null)
            {
                var abilityObj = Instantiate(classData.ability3Prefab, transform);
                abilityObj.name = "Ability3";
            }
            
            // notify ability manager that abilities have been loaded
            abilityManager.FindAbilitiesAfterClassLoad();
        }        
        
        private void ClearAbilities()
        {
            // destroy existing ability children immediately so they don't conflict with new instantiation
            // must use DestroyImmediate — Destroy defers to end of frame, leaving old abilities
            // in the hierarchy where FindAbilityBySlotName can find them instead of the new ones
            // collect into a list first to avoid skipping children during forward iteration
            // (destroying shifts indices which causes foreach to skip the middle child)
            var toDestroy = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Ability"))
                {
                    toDestroy.Add(child);
                }
            }
            foreach (var child in toDestroy)
            {
                if (child != null) DestroyImmediate(child.gameObject);
            }
        }
        
        private PlayerClass GetClassData(int classId)
        {
            // get class definition from the registry (single source of truth)
            if (ClassRegistry.Instance == null)
            {
                Debug.LogError("PlayerClassManager.GetClassData: ClassRegistry not found!");
                return null;
            }
            
            PlayerClass classData = ClassRegistry.Instance.GetClass(classId);
            if (classData == null)
            {
                Debug.LogError($"PlayerClassManager.GetClassData: No class data found for classId {classId}!");
                return null;
            }
            
            // Debug.Log($"PlayerClassManager.GetClassData: Found classId {classId} -> {classData.className}");
            return classData;
        }
        
        // public method to set class (call before spawning or during lobby)
        public void SetSelectedClassId(int classId)
        {
            SelectedClassId.Value = classId;
        }
        
        public int GetSelectedClassId()
        {
            return SelectedClassId.Value;
        }
    }
}
