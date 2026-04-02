using UnityEngine;
using TMPro;
using Unity.Netcode;
using Category5.Core;
using Category5.Player;

namespace Category5.UI
{
    // manages class selection dropdown in the lobby
    public class LobbyClassSelectionUI : MonoBehaviour
    {
        [SerializeField] private GameObject classSelectionPanel;
        [SerializeField] private TMP_Dropdown classDropdown;
        
        private bool _isInitialized = false;
        
        private void OnEnable()
        {
            LobbyManager.OnLobbyPlayersChanged += OnLobbyPlayersChanged;
        }
        
        private void OnDisable()
        {
            LobbyManager.OnLobbyPlayersChanged -= OnLobbyPlayersChanged;
        }
        
        // call this from NetworkMenu when entering the lobby
        public void Initialize()
        {
            if (_isInitialized) return;
            
            // hide panel initially
            if (classSelectionPanel != null)
            {
                classSelectionPanel.SetActive(false);
            }
            
            // populate dropdown with class names
            PopulateDropdown();
            
            // subscribe to dropdown changes
            if (classDropdown != null)
            {
                classDropdown.onValueChanged.AddListener(OnClassSelectionChanged);
            }
            
            _isInitialized = true;
        }
        
        // call this from NetworkMenu when showing the lobby
        public void ShowSelection()
        {
            if (!_isInitialized) Initialize();
            
            if (classSelectionPanel != null)
            {
                classSelectionPanel.SetActive(true);
            }
            
            // set dropdown to current selection
            UpdateDropdownToCurrentSelection();
        }
        
        // call this when hiding the lobby
        public void HideSelection()
        {
            if (classSelectionPanel != null)
            {
                classSelectionPanel.SetActive(false);
            }
        }
        
        private void PopulateDropdown()
        {
            if (classDropdown == null) return;
            
            if (ClassRegistry.Instance == null)
            {
                Debug.LogError("LobbyClassSelectionUI: ClassRegistry not found! Make sure it's in the scene.");
                return;
            }
            
            classDropdown.ClearOptions();
            
            // get all available classes from ClassRegistry (single source of truth)
            var classes = ClassRegistry.Instance.GetAllClasses();
            
            if (classes == null || classes.Length == 0)
            {
                Debug.LogError("LobbyClassSelectionUI: No available classes found in ClassRegistry!");
                return;
            }
            
            foreach (var playerClass in classes)
            {
                if (playerClass != null)
                {
                    classDropdown.options.Add(new TMP_Dropdown.OptionData(playerClass.className));
                }
            }
            
            classDropdown.RefreshShownValue();
            // Debug.Log($"LobbyClassSelectionUI: Populated dropdown with {classDropdown.options.Count} classes");
        }
        
        private void UpdateDropdownToCurrentSelection()
        {
            if (classDropdown == null) return;
            if (LobbyManager.Instance == null) return;
            if (ClassRegistry.Instance == null) return;
            
            // get the local player's current class id from lobby
            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            int selectedClassId = LobbyManager.Instance.GetPlayerClassId(localClientId);
            
            // find the dropdown index whose classId matches
            var allClasses = ClassRegistry.Instance.GetAllClasses();
            for (int i = 0; i < allClasses.Length; i++)
            {
                if (allClasses[i] != null && allClasses[i].classId == selectedClassId)
                {
                    classDropdown.value = i;
                    return;
                }
            }
        }
        
        private void OnClassSelectionChanged(int index)
        {
            if (LobbyManager.Instance == null) return;
            if (NetworkManager.Singleton == null) return;
            if (ClassRegistry.Instance == null) return;
            
            // look up the class id from the registry by dropdown index (not enum cast)
            var allClasses = ClassRegistry.Instance.GetAllClasses();
            if (index < 0 || index >= allClasses.Length || allClasses[index] == null) return;
            int classId = allClasses[index].classId;
            
            // save to persistent ClassSelectionManager (creates it if needed)
            ClassSelectionManager.SetClass(classId);
            
            // send to server
            if (NetworkManager.Singleton.IsServer)
            {
                // host sets directly
                LobbyManager.Instance.SetHostPlayerClassId(classId);
            }
            else
            {
                // client sends to server
                LobbyManager.Instance.SendLocalPlayerClassId(classId);
            }
            
            // Debug.Log($"LobbyClassSelectionUI: Selected classId {classId}");
        }
        
        private void OnLobbyPlayersChanged()
        {
            // update dropdown if needed
            UpdateDropdownToCurrentSelection();
        }
    }
}
