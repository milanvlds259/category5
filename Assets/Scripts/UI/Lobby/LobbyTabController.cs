using UnityEngine;
using UnityEngine.UI;
using System;

namespace Category5.UI
{
    // manages the top-left icon bar in the lobby
    // leave lobby (back arrow), chat (disabled/greyed out), settings (opens overlay)
    public class LobbyTabController : MonoBehaviour
    {
        [Header("icon buttons")]
        [SerializeField] private Button leaveLobbyButton;
        [SerializeField] private Button chatButton; // greyed out for now
        [SerializeField] private Button settingsButton;
        
        [Header("icon images (for visual state)")]
        [SerializeField] private Image leaveLobbyImage;
        [SerializeField] private Image chatImage;
        [SerializeField] private Image settingsImage;
        
        [Header("panels")]
        [SerializeField] private GameObject settingsPanel; // settings overlay toggled by gear icon
        
        [Header("visual settings")]
        [SerializeField] private Color activeColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        
        // fired when leave lobby is clicked, NetworkMenu subscribes to this
        public static event Action OnLeaveLobbyClicked;
        
        private bool _settingsOpen = false;
        
        private void OnEnable()
        {
            if (leaveLobbyButton != null)
                leaveLobbyButton.onClick.AddListener(OnLeaveClicked);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);
            
            // chat is disabled
            if (chatButton != null)
                chatButton.interactable = false;
        }
        
        private void OnDisable()
        {
            if (leaveLobbyButton != null)
                leaveLobbyButton.onClick.RemoveListener(OnLeaveClicked);
            if (settingsButton != null)
                settingsButton.onClick.RemoveListener(OnSettingsClicked);
        }
        
        // call this when entering the lobby
        public void Initialize()
        {
            _settingsOpen = false;
            
            // grey out chat icon
            if (chatImage != null)
                chatImage.color = disabledColor;
            
            // make sure settings panel starts hidden
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }
        
        private void OnLeaveClicked()
        {
            OnLeaveLobbyClicked?.Invoke();
        }
        
        private void OnSettingsClicked()
        {
            _settingsOpen = !_settingsOpen;
            
            if (settingsPanel != null)
                settingsPanel.SetActive(_settingsOpen);
        }
    }
}
