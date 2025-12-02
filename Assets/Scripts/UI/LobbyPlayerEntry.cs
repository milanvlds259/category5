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
        
        [Header("Colors")]
        [SerializeField] private Color hostColor = new Color(1f, 0.85f, 0.4f); // golden looking color ig
        [SerializeField] private Color normalColor = Color.white;
        
        public void Setup(string playerName, bool isHost, bool isLocalPlayer)
        {
            Debug.Log($"LobbyPlayerEntry.Setup called: name='{playerName}', isHost={isHost}, isLocal={isLocalPlayer}, nameText={playerNameText != null}");
            
            if (playerNameText != null)
            {
                playerNameText.text = playerName;
                playerNameText.color = isHost ? hostColor : normalColor;
                Debug.Log($"LobbyPlayerEntry: Set text to '{playerName}'");
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
        }
    }
}
