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
            Debug.Log($"LobbyClassSelectionUI: Populated dropdown with {classDropdown.options.Count} classes");
        }
        
        private void UpdateDropdownToCurrentSelection()
        {
            if (classDropdown == null) return;
            if (LobbyManager.Instance == null) return;
            
            // get the local player's current class selection from lobby
            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            PlayerClassType selectedClass = LobbyManager.Instance.GetPlayerClass(localClientId);
            
            // set dropdown to the selected class index
            classDropdown.value = (int)selectedClass;
        }
        
        private void OnClassSelectionChanged(int index)
        {
            if (LobbyManager.Instance == null) return;
            if (NetworkManager.Singleton == null) return;
            
            PlayerClassType selectedClass = (PlayerClassType)index;
            
            // send to server
            if (NetworkManager.Singleton.IsServer)
            {
                // host sets directly
                LobbyManager.Instance.SetHostPlayerClass(selectedClass);
            }
            else
            {
                // client sends to server
                LobbyManager.Instance.SendLocalPlayerClass(selectedClass);
            }
            
            Debug.Log($"LobbyClassSelectionUI: Selected class {selectedClass}");
        }
        
        private void OnLobbyPlayersChanged()
        {
            // update dropdown if needed
            UpdateDropdownToCurrentSelection();
        }
    }
}
