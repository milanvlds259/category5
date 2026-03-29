using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Category5.UI
{
    // ui component for displaying a player in the lobby party panel
    // shows class portrait + username, outline on bg toggles when ready
    public class LobbyPlayerEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private Image backgroundImage; // optional, for styling
        [SerializeField] private Outline bgOutline; // outline on bg image, toggled when player is readyy

        [Header("portrait")]
        [SerializeField] private Image portraitImage; // class portrait shown in party panel

        [Header("colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color readyColor = new Color(0.4f, 1f, 0.4f); // green when ready
        [SerializeField] private Color notReadyColor = new Color(0.5f, 0.5f, 0.5f); // grey when not ready

        // simple overload - no ready state
        public void Setup(string playerName)
        {
            Setup(playerName, false, null);
        }

        // overload without portrait
        public void Setup(string playerName, bool isReady)
        {
            Setup(playerName, isReady, null);
        }

        // full setup with portrait and ready state for party panel
        public void Setup(string playerName, bool isReady, Sprite classPortrait)
        {
            if (playerNameText != null)
            {
                playerNameText.text = playerName;
            }
            else
            {
                Debug.LogError("LobbyPlayerEntry: playerNameText is null!");
            }

            // set class portrait if provided
            if (portraitImage != null)
            {
                if (classPortrait != null)
                {
                    portraitImage.sprite = classPortrait;
                    portraitImage.gameObject.SetActive(true);
                }
            }

            UpdateReadyState(isReady);
        }

        public void UpdateReadyState(bool isReady)
        {
            if (playerNameText != null)
            {
                playerNameText.color = isReady ? readyColor : notReadyColor;
            }

            // toggle bg outline to show ready state
            if (bgOutline != null)
            {
                bgOutline.enabled = isReady;
            }
        }
    }
}
