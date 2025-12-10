using Unity.Netcode;
using UnityEngine;
using Category5.PowerUps;

namespace Category5.Player
{
    // manages the player's selected class and loads the appropriate abilities
    public class PlayerClassManager : NetworkBehaviour
    {
        [Header("Class Selection")]
        public NetworkVariable<PlayerClassType> SelectedClass = new NetworkVariable<PlayerClassType>(PlayerClassType.Ranger);
        
        [SerializeField] private PlayerClass[] availableClasses = new PlayerClass[5]; // one for each class type
        
        private PlayerAbilityManager abilityManager;
        private PlayerCombat playerCombat;
        
        private void Awake()
        {
            abilityManager = GetComponent<PlayerAbilityManager>();
            playerCombat = GetComponent<PlayerCombat>();
        }
        
        public override void OnNetworkSpawn()
        {
            Debug.Log($"PlayerClassManager.OnNetworkSpawn: IsServer={IsServer}, IsOwner={IsOwner}, SelectedClass={SelectedClass.Value}, OwnerClientId={OwnerClientId}");
            
            // subscribe to class changes
            SelectedClass.OnValueChanged += OnSelectedClassChanged;
            
            // each client/owner loads their own player's abilities when they spawn
            if (IsOwner)
            {
                Debug.Log($"PlayerClassManager: Owner {OwnerClientId} loading class {SelectedClass.Value}");
                LoadClassLocally(SelectedClass.Value);
            }
            else
            {
                Debug.Log($"PlayerClassManager: Non-owner observing player {OwnerClientId} with class {SelectedClass.Value}");
            }
        }
        
        private void OnSelectedClassChanged(PlayerClassType oldClass, PlayerClassType newClass)
        {
            // when class changes, reload abilities on all instances
            Debug.Log($"PlayerClassManager.OnSelectedClassChanged: {oldClass} -> {newClass}");
            LoadClassLocally(newClass);
        }

        // client requests to set their class
        [Rpc(SendTo.Server)]
        public void RequestSetClassServerRpc(PlayerClassType classType)
        {
            if (!IsServer) return;
            
            // server updates the selected class (triggers OnValueChanged on all clients)
            SelectedClass.Value = classType;
        }

        // load class and spawn its abilities (for the owner of this player)
        private void LoadClassLocally(PlayerClassType classType)
        {
            // only instantiate for this player's owner (server can own, client can own their own player)
            if (!IsOwner) return;
            
            Debug.Log($"PlayerClassManager.LoadClassLocally: Owner loading class {classType} for player {OwnerClientId}");
            
            PlayerClass classData = GetClassData(classType);
            if (classData == null)
            {
                Debug.LogError($"PlayerClassManager: No class data found for {classType}!");
                return;
            }
            
            Debug.Log($"PlayerClassManager: Found class data {classData.className}");
            Debug.Log($"  - Ability1Prefab: {(classData.ability1Prefab != null ? classData.ability1Prefab.name : "null")}");
            Debug.Log($"  - Ability2Prefab: {(classData.ability2Prefab != null ? classData.ability2Prefab.name : "null")}");
            Debug.Log($"  - Ability3Prefab: {(classData.ability3Prefab != null ? classData.ability3Prefab.name : "null")}");
            
            // clear existing abilities
            ClearAbilities();
            
            // spawn new abilities locally
            if (classData.ability1Prefab != null)
            {
                var abilityObj = Instantiate(classData.ability1Prefab, transform);
                abilityObj.name = "Ability1";
                Debug.Log($"PlayerClassManager: Instantiated Ability1");
            }
            
            if (classData.ability2Prefab != null)
            {
                var abilityObj = Instantiate(classData.ability2Prefab, transform);
                abilityObj.name = "Ability2";
                Debug.Log($"PlayerClassManager: Instantiated Ability2");
            }
            
            if (classData.ability3Prefab != null)
            {
                var abilityObj = Instantiate(classData.ability3Prefab, transform);
                abilityObj.name = "Ability3";
                Debug.Log($"PlayerClassManager: Instantiated Ability3");
            }
            
            // notify ability manager that abilities have been loaded
            Debug.Log($"PlayerClassManager: Calling FindAbilitiesAfterClassLoad");
            abilityManager.FindAbilitiesAfterClassLoad();
            
            Debug.Log($"Loaded class: {classData.className}");
        }        private void ClearAbilities()
        {
            // destroy existing ability children
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Ability"))
                {
                    Destroy(child.gameObject);
                }
            }
        }
        
        private PlayerClass GetClassData(PlayerClassType classType)
        {
            // search for the class by matching classType enum, not by array index
            // this is safer and doesn't require array ordering to match enum order
            foreach (var classData in availableClasses)
            {
                if (classData != null && classData.classType == classType)
                {
                    Debug.Log($"PlayerClassManager.GetClassData: Found {classType} -> {classData.className}");
                    return classData;
                }
            }
            
            Debug.LogError($"PlayerClassManager.GetClassData: No class data found for {classType}!");
            return null;
        }
        
        // public method to set class (call before spawning or during lobby)
        public void SetSelectedClass(PlayerClassType classType)
        {
            SelectedClass.Value = classType;
        }
        
        public PlayerClassType GetSelectedClass()
        {
            return SelectedClass.Value;
        }
    }
}
