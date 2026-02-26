using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace Category5.UI
{
    // handles the game over screen display
    public class GameOverUI : MonoBehaviour
    {
        [Header("ui references")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI roundReachedText;
        [SerializeField] private TextMeshProUGUI statsText; // optional for future stats display
        [SerializeField] private Button returnToMenuButton;
        
        [Header("settings")]
        [SerializeField] private string menuSceneName = "MainMenu";
        
        private bool _isSubscribed = false;
        
        private void Start()
        {
            TrySubscribeToEvents();
            
            // hide panel initially
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }
            
            // setup button listener
            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);
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
            
            if (Category5.Core.GameFlowManager.Instance != null)
            {
                Category5.Core.GameFlowManager.Instance.OnGameOver += ShowGameOver;
                _isSubscribed = true;
                Debug.Log("GameOverUI: Subscribed to GameFlowManager events");
            }
        }
        
        private void OnDestroy()
        {
            if (Category5.Core.GameFlowManager.Instance != null)
            {
                Category5.Core.GameFlowManager.Instance.OnGameOver -= ShowGameOver;
            }
            
            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.RemoveListener(OnReturnToMenuClicked);
            }
        }
        
        private void ShowGameOver()
        {
            if (gameOverPanel == null) return;
            
            Debug.Log("GameOverUI: Showing game over screen");
            
            // show panel
            gameOverPanel.SetActive(true);
            
            // unlock cursor for menu interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // update title
            if (titleText != null)
            {
                titleText.text = "GAME OVER";
            }
            
            // update round reached text
            if (roundReachedText != null)
            {
                int roundReached = Category5.Core.GameFlowManager.Instance != null 
                    ? Category5.Core.GameFlowManager.Instance.CurrentRound.Value 
                    : 1;
                roundReachedText.text = $"Reached Round {roundReached}";
            }
            
            // optional stats display (can be expanded later)
            if (statsText != null)
            {
                statsText.text = "All players were eliminated!";
            }
        }
        
        private void HideGameOver()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }
        }
        
        private void OnReturnToMenuClicked()
        {
            Debug.Log("GameOverUI: Returning to menu");
            
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
    }
}
