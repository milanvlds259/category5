using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Category5.Player;

namespace Category5.UI
{
    // displays detailed view of a character class
    // shows class art, name, and Q/E/R ability descriptions
    public class CharacterViewPanel : MonoBehaviour
    {
        [Header("class display")]
        [SerializeField] private Image classArtImage;
        [SerializeField] private TextMeshProUGUI classNameText;
        [SerializeField] private TextMeshProUGUI classDescriptionText;
        [SerializeField] private Sprite defaultClassSprite; // fallback if no icon
        
        [Header("ability Q")]
        [SerializeField] private Image ability1Icon;
        [SerializeField] private TextMeshProUGUI ability1NameText;
        [SerializeField] private TextMeshProUGUI ability1DescriptionText;
        
        [Header("ability E")]
        [SerializeField] private Image ability2Icon;
        [SerializeField] private TextMeshProUGUI ability2NameText;
        [SerializeField] private TextMeshProUGUI ability2DescriptionText;
        
        [Header("ability R")]
        [SerializeField] private Image ability3Icon;
        [SerializeField] private TextMeshProUGUI ability3NameText;
        [SerializeField] private TextMeshProUGUI ability3DescriptionText;
        
        [Header("navigation")]
        [SerializeField] private Button closeButton;
        
        [Header("default sprites")]
        [SerializeField] private Sprite defaultAbilityIcon;
        
        private PlayerClass _currentClass;
        
        private void OnEnable()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseClicked);
        }
        
        private void OnDisable()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnCloseClicked);
        }
        
        // called by CharacterSelectPanel when View is clicked
        public void ShowClass(PlayerClass playerClass)
        {
            _currentClass = playerClass;
            UpdateDisplay();
        }
        
        private void OnCloseClicked()
        {
            // hide the panel
            gameObject.SetActive(false);
        }
        
        private void UpdateDisplay()
        {
            if (_currentClass == null) return;
            
            // update class art
            if (classArtImage != null)
            {
                classArtImage.sprite = _currentClass.classIcon != null 
                    ? _currentClass.classIcon 
                    : defaultClassSprite;
            }
            
            // update class name
            if (classNameText != null)
            {
                classNameText.text = _currentClass.className;
            }
            
            // update class description
            if (classDescriptionText != null)
            {
                classDescriptionText.text = _currentClass.classDescription;
            }
            
            // update abilities
            UpdateAbilityDisplay(
                _currentClass.ability1Prefab,
                ability1Icon,
                ability1NameText,
                ability1DescriptionText,
                "Q - Unknown Ability"
            );
            
            UpdateAbilityDisplay(
                _currentClass.ability2Prefab,
                ability2Icon,
                ability2NameText,
                ability2DescriptionText,
                "E - Unknown Ability"
            );
            
            UpdateAbilityDisplay(
                _currentClass.ability3Prefab,
                ability3Icon,
                ability3NameText,
                ability3DescriptionText,
                "R - Unknown Ability"
            );
        }
        
        private void UpdateAbilityDisplay(
            GameObject abilityPrefab,
            Image iconImage,
            TextMeshProUGUI nameText,
            TextMeshProUGUI descriptionText,
            string defaultName)
        {
            AbilityData abilityData = null;
            
            // try to get ability data from prefab
            if (abilityPrefab != null)
            {
                var abilityBase = abilityPrefab.GetComponent<AbilityBase>();
                if (abilityBase != null)
                {
                    abilityData = abilityBase.Data;
                }
            }
            
            if (abilityData != null)
            {
                // update icon
                if (iconImage != null)
                {
                    iconImage.sprite = abilityData.abilityIcon != null 
                        ? abilityData.abilityIcon 
                        : defaultAbilityIcon;
                    iconImage.gameObject.SetActive(true);
                }
                
                // update name
                if (nameText != null)
                {
                    nameText.text = abilityData.abilityName;
                }
                
                // update description
                if (descriptionText != null)
                {
                    descriptionText.text = abilityData.description;
                }
            }
            else
            {
                // no ability data, show defaults
                if (iconImage != null)
                {
                    iconImage.sprite = defaultAbilityIcon;
                    iconImage.gameObject.SetActive(defaultAbilityIcon != null);
                }
                
                if (nameText != null)
                {
                    nameText.text = defaultName;
                }
                
                if (descriptionText != null)
                {
                    descriptionText.text = "No ability data available.";
                }
            }
        }
        
        public PlayerClass CurrentClass => _currentClass;
    }
}
