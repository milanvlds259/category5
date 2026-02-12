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
        [SerializeField] private GameObject settingsPanel;
        
        [Header("external panels")]
        [SerializeField] private GameObject characterViewPanel; // external floating panel
        
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
            SwitchToTab(LobbyTab.Character);
        }
        
        public void OnChatTabClicked()
        {
            SwitchToTab(LobbyTab.Chat);
        }
        
        public void OnCharacterTabClicked()
        {
            SwitchToTab(LobbyTab.Character);
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
            SetPanelActive(settingsPanel, false);
            
            // hide character view panel when switching away from character tab
            if (tab != LobbyTab.Character)
            {
                SetPanelActive(characterViewPanel, false);
            }
            
            // show the appropriate panel
            switch (tab)
            {
                case LobbyTab.Chat:
                    SetPanelActive(chatPanel, true);
                    break;
                case LobbyTab.Character:
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
    }
}
