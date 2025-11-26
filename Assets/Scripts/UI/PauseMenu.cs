using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Category5.Core;

namespace Category5.UI
{
    // handles pause menu functionality including disconnect/quit options
    public class PauseMenu : MonoBehaviour
    {
        [Header("ui references")]
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button quitButton;
        
        private bool isPaused = false;
        private InputSystem_Actions inputActions;
        
        private void Awake()
        {
            inputActions = new InputSystem_Actions();
        }
        
        private void OnEnable()
        {
            inputActions.Enable();
            // we may need to add a pause action to our input actions
            // for now we'll use escape key directly
        }
        
        private void OnDisable()
        {
            inputActions.Disable();
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
        
        private void OnDestroy()
        {
            if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
            if (disconnectButton != null) disconnectButton.onClick.RemoveListener(Disconnect);
            if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
        }
        
        private void Update()
        {
            // toggle pause on escape
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
        }
        
        public void TogglePause()
        {
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
            
            // unlock cursor for menu interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            Debug.Log("PauseMenu: Game paused");
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
