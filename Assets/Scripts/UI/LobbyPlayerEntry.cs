using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Category5.UI
{
    // simple ui component for displaying a player entry in the lobby list
    public class LobbyPlayerEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private Image hostIndicatorIcon;
        [SerializeField] private Image backgroundImage; // kinda optional, for highlighting
        
        [Header("ready indicator")]
        [SerializeField] private Image readyIcon; // optional, for coloring
        
        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color readyColor = new Color(0.4f, 1f, 0.4f); // green for ready
        [SerializeField] private Color notReadyColor = new Color(0.5f, 0.5f, 0.5f); // gray for not ready
        
        public void Setup(string playerName, bool isHost, bool isLocalPlayer)
        {
            Setup(playerName, isHost, isLocalPlayer, false);
        }
        
        public void Setup(string playerName, bool isHost, bool isLocalPlayer, bool isReady)
        {
            // Debug.Log($"LobbyPlayerEntry.Setup called: name='{playerName}', isHost={isHost}, isLocal={isLocalPlayer}, isReady={isReady}");
            
            if (playerNameText != null)
            {
                playerNameText.text = playerName;
            }
            else
            {
                Debug.LogError("LobbyPlayerEntry: playerNameText is null! Make sure it's assigned in the prefab.");
            }
            
            if (hostIndicatorIcon != null)
            {
                hostIndicatorIcon.gameObject.SetActive(isHost);
            }
            
            // highlight local player (if we have a background image in the future)
            if (backgroundImage != null && isLocalPlayer)
            {
                var color = backgroundImage.color;
                color.a = 0.3f;
                backgroundImage.color = color;
            }
            
            // update ready indicator
            UpdateReadyState(isReady);
        }
        
        public void UpdateReadyState(bool isReady)
        {
            if (playerNameText != null)
            {
                playerNameText.color = isReady ? readyColor : notReadyColor;
            }
            
            if (readyIcon != null)
            {
                readyIcon.gameObject.SetActive(isReady);
            }
        }
    }
}
