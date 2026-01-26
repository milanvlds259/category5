using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Category5.Core;
using Category5.Player;
using Category5.PowerUps;

namespace Category5.UI
{
    // handles pause menu functionality including disconnect/quit options
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }
        public static bool GameIsPaused => Instance != null && Instance.isPaused;
        
        [Header("ui references")]
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button quitButton;
        
        private bool isPaused = false;
        private InputSystem_Actions inputActions;
        
        private void Awake()
        {
            // always take over as the instance since pause menu is scene-specific
            // the previous instance from another scene is no longer valid
            Instance = this;
            inputActions = new InputSystem_Actions();
        }
        
        private void OnEnable()
        {
            // enable only the UI action map for pause input
            inputActions.UI.Enable();
            inputActions.UI.Cancel.performed += OnPausePerformed;
        }
        
        private void OnDisable()
        {
            inputActions.UI.Cancel.performed -= OnPausePerformed;
            inputActions.UI.Disable();
        }
        
        private void OnDestroy()
        {
            // clear instance if we're the current one
            if (Instance == this)
            {
                Instance = null;
            }
            
            if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
            if (disconnectButton != null) disconnectButton.onClick.RemoveListener(Disconnect);
            if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
        }
        
        private void OnPausePerformed(InputAction.CallbackContext context)
        {
            TogglePause();
        }
        
        private void Start()
        {
            // setup button listeners
            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(Resume);
            }
            
            if (disconnectButton != null)
            {
                disconnectButton.onClick.AddListener(Disconnect);
            }
            
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(QuitGame);
            }
            
            // hide pause menu initially
            if (pauseMenuPanel != null)
            {
                pauseMenuPanel.SetActive(false);
            }
        }
        
        public void TogglePause()
        {
            // don't allow pause during game over or victory
            if (Category5.Items.ItemManager.Instance != null)
            {
                var phase = Category5.Items.ItemManager.Instance.CurrentPhase.Value;
                if (phase == Category5.Core.GamePhase.GameOver || phase == Category5.Core.GamePhase.Victory)
                {
                    return;
                }
            }
            
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
        
        public void Pause()
        {
            isPaused = true;
            
            if (pauseMenuPanel != null)
            {
                pauseMenuPanel.SetActive(true);
            } 
            else
            {
                // Debug.LogWarning("PauseMenu: pauseMenuPanel reference is missing!");
            }
            
            // unlock cursor for menu interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Debug.Log($"PauseMenu: Game paused. Instance == this: {Instance == this}, isPaused: {isPaused}, GameIsPaused: {GameIsPaused}");
        }
        
        public void Resume()
        {
            isPaused = false;
            
            if (pauseMenuPanel != null)
            {
                pauseMenuPanel.SetActive(false);
            }
            
            // lock cursor for gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            Debug.Log("PauseMenu: Game resumed");
        }
        
        public void Disconnect()
        {
            Debug.Log("PauseMenu: Disconnecting and returning to main menu");
            
            // use scene transition manager if available
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.LoadMainMenu();
            }
            else
            {
                // fallback to manual disconnect
                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.Shutdown();
                }
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
        }
        
        public void QuitGame()
        {
            Debug.Log("PauseMenu: Quitting game");
            
            // shutdown network if connected
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        
        public bool IsPaused => isPaused;
    }
}
