using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Category5.PowerUps;

namespace Category5.UI
{
    // handles the victory screen display when players complete all rounds
    public class VictoryUI : MonoBehaviour
    {
        [Header("ui references")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private Button returnToMenuButton;
        [SerializeField] private Button playAgainButton;
        
        [Header("settings")]
        [SerializeField] private string menuSceneName = "MainMenu";
        [SerializeField] private string gameSceneName = "GameScene";
        
        [Header("visuals")]
        [SerializeField] private Color titleColor = new Color(1f, 0.8f, 0f); // gold kinda looking thing
        
        private bool _isSubscribed = false;
        
        private void Start()
        {
            TrySubscribeToEvents();
            
            // hide panel initially
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(false);
            }
            
            // setup button listeners
            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);
            }
            
            if (playAgainButton != null)
            {
                playAgainButton.onClick.AddListener(OnPlayAgainClicked);
            }
        }
        
        private void Update()
        {
            // keep trying to subscribe if we haven't yet
            if (!_isSubscribed)
            {
                TrySubscribeToEvents();
            }
        }
        
        private void TrySubscribeToEvents()
        {
            if (_isSubscribed) return;
            
            if (PowerUpManager.Instance != null)
            {
                PowerUpManager.Instance.OnVictory += ShowVictory;
                _isSubscribed = true;
                Debug.Log("VictoryUI: Subscribed to PowerUpManager events");
            }
        }
        
        private void OnDestroy()
        {
            if (PowerUpManager.Instance != null)
            {
                PowerUpManager.Instance.OnVictory -= ShowVictory;
            }
            
            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.RemoveListener(OnReturnToMenuClicked);
            }
            
            if (playAgainButton != null)
            {
                playAgainButton.onClick.RemoveListener(OnPlayAgainClicked);
            }
        }
        
        private void ShowVictory()
        {
            if (victoryPanel == null) return;
            
            Debug.Log("VictoryUI: Showing victory screen");
            
            // show panel
            victoryPanel.SetActive(true);
            
            // unlock cursor for menu interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // update title
            if (titleText != null)
            {
                titleText.text = "VICTORY!";
                titleText.color = titleColor;
            }
            
            // update subtitle
            if (subtitleText != null)
            {
                subtitleText.text = "The storm has been conquered!";
            }
            
            // stats display
            if (statsText != null)
            {
                int totalRounds = PowerUpManager.Instance != null 
                    ? PowerUpManager.Instance.CurrentRound.Value 
                    : 3;
                statsText.text = $"Completed all {totalRounds} rounds!";
            }
            
            // only show play again button for host
            if (playAgainButton != null)
            {
                bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
                playAgainButton.gameObject.SetActive(isHost);
            }
        }
        
        private void HideVictory()
        {
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(false);
            }
        }
        
        private void OnReturnToMenuClicked()
        {
            Debug.Log("VictoryUI: Returning to menu");
            
            // ensure cursor is unlocked before scene transition
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // disconnect from network
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
            
            // load menu scene
            SceneManager.LoadScene(menuSceneName);
        }
        
        private void OnPlayAgainClicked()
        {
            Debug.Log("VictoryUI: Playing again");
            
            // only host can restart
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;
            
            // reload game scene for all clients
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
    }
}
