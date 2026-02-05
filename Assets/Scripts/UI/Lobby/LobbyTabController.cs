using UnityEngine;
using UnityEngine.UI;
using System;

namespace Category5.UI
{
    // manages tabbed navigation within the lobby panel
    // switches between chat, character select, and settings sub-panels
    public class LobbyTabController : MonoBehaviour
    {
        [Header("tab buttons")]
        [SerializeField] private Button chatTabButton;
        [SerializeField] private Button characterTabButton;
        [SerializeField] private Button settingsTabButton;
        
        [Header("tab button images (for highlighting)")]
        [SerializeField] private Image chatTabImage;
        [SerializeField] private Image characterTabImage;
        [SerializeField] private Image settingsTabImage;
        
        [Header("content panels")]
        [SerializeField] private GameObject chatPanel;
        [SerializeField] private GameObject characterSelectPanel;
        [SerializeField] private GameObject characterViewPanel;
        [SerializeField] private GameObject settingsPanel;
        
        [Header("visual settings")]
        [SerializeField] private Color activeTabColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color inactiveTabColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        
        public enum LobbyTab
        {
            Chat,
            Character,
            Settings
        }
        
        private LobbyTab _currentTab = LobbyTab.Character;
        
        // event fired when tab changes
        public static event Action<LobbyTab> OnTabChanged;
        
        // track if we're in character view vs character select
        private bool _isInCharacterView = false;
        
        private void OnEnable()
        {
            // setup button listeners
            if (chatTabButton != null)
                chatTabButton.onClick.AddListener(OnChatTabClicked);
            if (characterTabButton != null)
                characterTabButton.onClick.AddListener(OnCharacterTabClicked);
            if (settingsTabButton != null)
                settingsTabButton.onClick.AddListener(OnSettingsTabClicked);
        }
        
        private void OnDisable()
        {
            // cleanup button listeners
            if (chatTabButton != null)
                chatTabButton.onClick.RemoveListener(OnChatTabClicked);
            if (characterTabButton != null)
                characterTabButton.onClick.RemoveListener(OnCharacterTabClicked);
            if (settingsTabButton != null)
                settingsTabButton.onClick.RemoveListener(OnSettingsTabClicked);
        }
        
        // call this when entering the lobby to set initial state
        public void Initialize()
        {
            _isInCharacterView = false;
            SwitchToTab(LobbyTab.Character);
        }
        
        public void OnChatTabClicked()
        {
            SwitchToTab(LobbyTab.Chat);
        }
        
        public void OnCharacterTabClicked()
        {
            // if already on character tab, reset to select view
            if (_currentTab == LobbyTab.Character)
            {
                ShowCharacterSelectView();
            }
            else
            {
                SwitchToTab(LobbyTab.Character);
            }
        }
        
        public void OnSettingsTabClicked()
        {
            SwitchToTab(LobbyTab.Settings);
        }
        
        public void SwitchToTab(LobbyTab tab)
        {
            _currentTab = tab;
            
            // hide all panels first
            SetPanelActive(chatPanel, false);
            SetPanelActive(characterSelectPanel, false);
            SetPanelActive(characterViewPanel, false);
            SetPanelActive(settingsPanel, false);
            
            // show the appropriate panel
            switch (tab)
            {
                case LobbyTab.Chat:
                    SetPanelActive(chatPanel, true);
                    break;
                case LobbyTab.Character:
                    // show select or view based on current state
                    if (_isInCharacterView)
                        SetPanelActive(characterViewPanel, true);
                    else
                        SetPanelActive(characterSelectPanel, true);
                    break;
                case LobbyTab.Settings:
                    SetPanelActive(settingsPanel, true);
                    break;
            }
            
            // update tab button visuals
            UpdateTabVisuals();
            
            OnTabChanged?.Invoke(tab);
        }
        
        // called by CharacterSelectPanel when "View" button is clicked
        public void ShowCharacterViewPanel()
        {
            _isInCharacterView = true;
            SetPanelActive(characterSelectPanel, false);
            SetPanelActive(characterViewPanel, true);
        }
        
        // called by CharacterViewPanel to go back to select
        public void ShowCharacterSelectView()
        {
            _isInCharacterView = false;
            SetPanelActive(characterViewPanel, false);
            SetPanelActive(characterSelectPanel, true);
        }
        
        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }
        
        private void UpdateTabVisuals()
        {
            // update tab button colors to show active state
            if (chatTabImage != null)
                chatTabImage.color = _currentTab == LobbyTab.Chat ? activeTabColor : inactiveTabColor;
            if (characterTabImage != null)
                characterTabImage.color = _currentTab == LobbyTab.Character ? activeTabColor : inactiveTabColor;
            if (settingsTabImage != null)
                settingsTabImage.color = _currentTab == LobbyTab.Settings ? activeTabColor : inactiveTabColor;
        }
        
        public LobbyTab CurrentTab => _currentTab;
        public bool IsInCharacterView => _isInCharacterView;
    }
}
