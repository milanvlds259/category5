using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System;

namespace Category5.Core
{
    // manages scene transitions in a networked environment
    // handles both networked and local scene loading
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }
        
        [Header("scene names")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string gameSceneName = "SampleScene";
        
        public event Action OnSceneLoadStarted;
        public event Action<string> OnSceneLoadCompleted;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            // subscribe to network scene events if available
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadComplete += OnNetworkSceneLoadComplete;
            }
        }
        
        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnNetworkSceneLoadComplete;
            }
        }
        

        // loads the game scene, uses network scene management if available
        public void LoadGameScene()
        {
            LoadScene(gameSceneName);
        }
        
        // loads the main menu scene and disconnects from network
        public void LoadMainMenu()
        {
            // disconnect from network first
            DisconnectFromNetwork();
            
            // load main menu locally since we're disconnected
            OnSceneLoadStarted?.Invoke();
            SceneManager.LoadScene(mainMenuSceneName);
            OnSceneLoadCompleted?.Invoke(mainMenuSceneName);
        }
        
        // loads a scene by name, uses network scene manager if host
        public void LoadScene(string sceneName)
        {
            OnSceneLoadStarted?.Invoke();
            
            // check if we should use network scene management
            if (NetworkManager.Singleton != null && 
                NetworkManager.Singleton.IsServer && 
                NetworkManager.Singleton.SceneManager != null)
            {
                Debug.Log($"SceneTransitionManager: Loading scene '{sceneName}' via NetworkSceneManager");
                NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
            else
            {
                // local scene load for non-networked scenarios
                Debug.Log($"SceneTransitionManager: Loading scene '{sceneName}' locally");
                SceneManager.LoadScene(sceneName);
                OnSceneLoadCompleted?.Invoke(sceneName);
            }
        }
        
        // disconnects from the current network session
        public void DisconnectFromNetwork()
        {
            if (NetworkManager.Singleton == null) return;
            
            if (NetworkManager.Singleton.IsHost)
            {
                Debug.Log("SceneTransitionManager: Shutting down host");
                NetworkManager.Singleton.Shutdown();
            }
            else if (NetworkManager.Singleton.IsClient)
            {
                Debug.Log("SceneTransitionManager: Disconnecting client");
                NetworkManager.Singleton.Shutdown();
            }
        }
        
        private void OnNetworkSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            // only fire event for local client
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log($"SceneTransitionManager: Network scene load complete - {sceneName}");
                OnSceneLoadCompleted?.Invoke(sceneName);
            }
        }
        
        // checks if currently in a networked session
        public bool IsInNetworkSession()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        }

        public string GetCurrentSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }
    }
}
