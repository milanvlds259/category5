using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Category5.Core;
using Category5.Player;

namespace Category5.UI
{
    // manages character selection carousel in the lobby
    // displays class cards with arrow navigation
    public class CharacterSelectPanel : MonoBehaviour
    {
        [Header("references")]
        [SerializeField] private CharacterViewPanel characterViewPanel;
        
        [Header("carousel display")]
        [SerializeField] private Image currentClassIcon;
        [SerializeField] private TextMeshProUGUI currentClassName;
        [SerializeField] private Sprite defaultClassSprite; // fallback if no icon
        
        [Header("navigation buttons")]
        [SerializeField] private Button leftArrowButton;
        [SerializeField] private Button rightArrowButton;
        
        [Header("action buttons")]
        [SerializeField] private Button selectButton;
        [SerializeField] private Button viewButton;
        
        [Header("selection indicator")]
        [SerializeField] private GameObject selectedIndicator; // shows when current class is selected
        [SerializeField] private TextMeshProUGUI selectedText;
        
        private PlayerClass[] _availableClasses;
        private int _currentIndex = 0;
        private int _selectedIndex = 0; // the actually selected class
        
        private void OnEnable()
        {
            // setup button listeners
            if (leftArrowButton != null)
                leftArrowButton.onClick.AddListener(OnLeftArrowClicked);
            if (rightArrowButton != null)
                rightArrowButton.onClick.AddListener(OnRightArrowClicked);
            if (selectButton != null)
                selectButton.onClick.AddListener(OnSelectClicked);
            if (viewButton != null)
                viewButton.onClick.AddListener(OnViewClicked);
        }
        
        private void OnDisable()
        {
            // cleanup button listeners
            if (leftArrowButton != null)
                leftArrowButton.onClick.RemoveListener(OnLeftArrowClicked);
            if (rightArrowButton != null)
                rightArrowButton.onClick.RemoveListener(OnRightArrowClicked);
            if (selectButton != null)
                selectButton.onClick.RemoveListener(OnSelectClicked);
            if (viewButton != null)
                viewButton.onClick.RemoveListener(OnViewClicked);
        }
        
        // call this when entering the lobby
        public void Initialize()
        {
            // get classes from registry
            if (ClassRegistry.Instance == null)
            {
                Debug.LogError("CharacterSelectPanel: ClassRegistry not found!");
                return;
            }
            
            _availableClasses = ClassRegistry.Instance.GetAllClasses();
            
            if (_availableClasses == null || _availableClasses.Length == 0)
            {
                Debug.LogError("CharacterSelectPanel: No classes found in ClassRegistry!");
                return;
            }
            
            // start with previously selected class if available
            var savedClass = ClassSelectionManager.GetClass();
            bool foundSavedClass = false;
            for (int i = 0; i < _availableClasses.Length; i++)
            {
                if (_availableClasses[i].classType == savedClass)
                {
                    _currentIndex = i;
                    _selectedIndex = i;
                    foundSavedClass = true;
                    break;
                }
            }
            
            if (!foundSavedClass)
            {
                // default to first class (usually Ranger based on existing code)
                _currentIndex = 0;
                _selectedIndex = 0;
            }
            
            UpdateDisplay();
            UpdateCharacterViewPanel();
        }
        
        private void OnLeftArrowClicked()
        {
            if (_availableClasses == null || _availableClasses.Length == 0) return;
            
            _currentIndex--;
            if (_currentIndex < 0)
                _currentIndex = _availableClasses.Length - 1;
            
            UpdateDisplay();
            UpdateCharacterViewPanel();
        }
        
        private void OnRightArrowClicked()
        {
            if (_availableClasses == null || _availableClasses.Length == 0) return;
            
            _currentIndex++;
            if (_currentIndex >= _availableClasses.Length)
                _currentIndex = 0;
            
            UpdateDisplay();
            UpdateCharacterViewPanel();
        }
        
        private void OnSelectClicked()
        {
            if (_availableClasses == null || _currentIndex >= _availableClasses.Length) return;
            
            var selectedClass = _availableClasses[_currentIndex];
            _selectedIndex = _currentIndex;
            
            // save selection locally
            ClassSelectionManager.SetClass(selectedClass.classType);
            
            // send to server via lobby manager
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    // host updates directly
                    if (LobbyManager.Instance != null)
                    {
                        LobbyManager.Instance.SetHostPlayerClass(selectedClass.classType);
                    }
                }
                else
                {
                    // client sends to server
                    if (LobbyManager.Instance != null)
                    {
                        LobbyManager.Instance.SendLocalPlayerClass(selectedClass.classType);
                    }
                }
            }
            
            UpdateDisplay();
            
            Debug.Log($"CharacterSelectPanel: Selected class {selectedClass.className}");
        }
        
        private void OnViewClicked()
        {
            if (characterViewPanel == null) return;
            
            // simply toggle panel visibility
            bool isCurrentlyVisible = characterViewPanel.gameObject.activeSelf;
            characterViewPanel.gameObject.SetActive(!isCurrentlyVisible);
        }
        
        private void UpdateDisplay()
        {
            if (_availableClasses == null || _availableClasses.Length == 0) return;
            
            var currentClass = _availableClasses[_currentIndex];
            
            // update icon
            if (currentClassIcon != null)
            {
                currentClassIcon.sprite = currentClass.classIcon != null 
                    ? currentClass.classIcon 
                    : defaultClassSprite;
            }
            
            // update name
            if (currentClassName != null)
            {
                currentClassName.text = currentClass.className;
            }
            
            // update selected indicator
            bool isSelected = _currentIndex == _selectedIndex;
            if (selectedIndicator != null)
            {
                selectedIndicator.SetActive(isSelected);
            }
            
            if (selectedText != null)
            {
                selectedText.text = isSelected ? "SELECTED" : "";
            }
            
            // disable select button if already selected
            if (selectButton != null)
            {
                selectButton.interactable = !isSelected;
            }
        }
        
        // update the character view panel with current class (if it exists) (it should)
        private void UpdateCharacterViewPanel()
        {
            if (characterViewPanel == null) return;
            if (_availableClasses == null || _currentIndex >= _availableClasses.Length) return;
            
            var currentClass = _availableClasses[_currentIndex];
            characterViewPanel.ShowClass(currentClass);
        }
        
        // public accessor for current class being viewed
        public PlayerClass GetCurrentDisplayedClass()
        {
            if (_availableClasses == null || _currentIndex >= _availableClasses.Length)
                return null;
            return _availableClasses[_currentIndex];
        }
        
        // public accessor for currently selected class
        public PlayerClass GetSelectedClass()
        {
            if (_availableClasses == null || _selectedIndex >= _availableClasses.Length)
                return null;
            return _availableClasses[_selectedIndex];
        }
    }
}
