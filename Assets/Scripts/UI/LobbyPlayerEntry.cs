using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Category5.UI
{
    // simple ui component for displaying a player entry in the lobby list
    public class LobbyPlayerEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI hostIndicatorText; // optional, shows "(Host)" or similar
        [SerializeField] private Image backgroundImage; // kinda optional, for highlighting
        
        [Header("ready indicator")]
        [SerializeField] private GameObject readyIndicator; // checkmark icon or similar
        [SerializeField] private Image readyIcon; // optional, for coloring
        [SerializeField] private TextMeshProUGUI readyText; // optional, shows "Ready" text
        
        [Header("Colors")]
        [SerializeField] private Color hostColor = new Color(1f, 0.85f, 0.4f); // golden looking color ig
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
                playerNameText.color = isHost ? hostColor : normalColor;
            }
            else
            {
                Debug.LogError("LobbyPlayerEntry: playerNameText is null! Make sure it's assigned in the prefab.");
            }
            
            if (hostIndicatorText != null)
            {
                hostIndicatorText.gameObject.SetActive(isHost);
            }
            
            // optionally highlight local player
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
            if (readyIndicator != null)
            {
                readyIndicator.SetActive(isReady);
            }
            
            if (readyIcon != null)
            {
                readyIcon.color = isReady ? readyColor : notReadyColor;
            }
            
            if (readyText != null)
            {
                readyText.text = isReady ? "Ready" : "";
                readyText.color = isReady ? readyColor : notReadyColor;
            }
        }
    }
}
