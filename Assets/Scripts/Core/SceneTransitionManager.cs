using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System;
using System.Collections;
using Category5.UI;

namespace Category5.Core
{
    // manages scene transitions in a networked environment
    // handles both networked and local scene loading
    // drives the loading screen overlay during transitions
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }
        
        [Header("scene names")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string gameSceneName = "SampleScene";

        [Header("loading screen")]
        [SerializeField] private LoadingScreenUI loadingScreenUI;
        [SerializeField] private float postLoadDelay = 0.5f;
        
        public event Action OnSceneLoadStarted;
        public event Action<string> OnSceneLoadCompleted;
        
        private AsyncOperation _currentAsyncOp;
        private Coroutine _progressCoroutine;
        private bool _subscribedToNGOLoad;
        
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
            SubscribeToNetworkEvents();
        }

        private void OnEnable()
        {
            // re-subscribe after network restarts (returning to menu and re-hosting)
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted += OnServerStarted;
                NetworkManager.Singleton.OnClientStarted += OnClientStarted;
            }
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
                NetworkManager.Singleton.OnClientStarted -= OnClientStarted;
            }
        }

        private void OnServerStarted()
        {
            SubscribeToNetworkEvents();
        }

        private void OnClientStarted()
        {
            SubscribeToNetworkEvents();
        }

        private void SubscribeToNetworkEvents()
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null) return;
            if (_subscribedToNGOLoad) return;

            // OnLoad fires on ALL clients when a networked scene load begins
            // gives us the AsyncOperation for progress tracking
            NetworkManager.Singleton.SceneManager.OnLoad += OnNetworkSceneLoad;
            NetworkManager.Singleton.SceneManager.OnLoadComplete += OnNetworkSceneLoadComplete;
            _subscribedToNGOLoad = true;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (!_subscribedToNGOLoad) return;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoad -= OnNetworkSceneLoad;
                NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnNetworkSceneLoadComplete;
            }
            _subscribedToNGOLoad = false;
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
                NetworkManager.Singleton.OnClientStarted -= OnClientStarted;
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
            // show loading screen before disconnecting
            ShowLoadingScreen("Returning to menu...");

            // unsubscribe before shutdown so we dont get stale callbacks
            UnsubscribeFromNetworkEvents();
            
            // disconnect from network first
            DisconnectFromNetwork();
            
            OnSceneLoadStarted?.Invoke();

            // use async load so we can track progress
            StartCoroutine(LoadSceneLocalAsync(mainMenuSceneName));
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
                // show loading screen on the host before triggering the networked load
                // clients will get their loading screen via the OnLoad callback
                ShowLoadingScreen("Loading arena...");

                Debug.Log($"SceneTransitionManager: Loading scene '{sceneName}' via NetworkSceneManager");
                NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
            else
            {
                ShowLoadingScreen("Loading...");

                // local scene load for non-networked scenarios
                Debug.Log($"SceneTransitionManager: Loading scene '{sceneName}' locally");
                StartCoroutine(LoadSceneLocalAsync(sceneName));
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

        // =====================================
        // ngo scene load callbacks
        // =====================================

        // fires on ALL clients (host and clients) when a networked scene load starts
        private void OnNetworkSceneLoad(ulong clientId, string sceneName, LoadSceneMode loadSceneMode, AsyncOperation asyncOp)
        {
            // show loading screen for clients that didnt trigger the load themselves
            ShowLoadingScreen("Loading arena...");

            // track the async operation for progress
            _currentAsyncOp = asyncOp;
            if (_progressCoroutine != null)
                StopCoroutine(_progressCoroutine);
            _progressCoroutine = StartCoroutine(TrackProgress());
        }

        private void OnNetworkSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            // only act on our own client completing
            if (NetworkManager.Singleton == null) return;
            if (clientId != NetworkManager.Singleton.LocalClientId) return;

            Debug.Log($"SceneTransitionManager: Network scene load complete - {sceneName}");
            OnSceneLoadCompleted?.Invoke(sceneName);

            // stop progress tracking and fill bar to 100%
            if (_progressCoroutine != null)
            {
                StopCoroutine(_progressCoroutine);
                _progressCoroutine = null;
            }
            _currentAsyncOp = null;

            if (loadingScreenUI != null)
                loadingScreenUI.UpdateProgress(1f);

            // wait a short time for game systems to initialize then hide
            StartCoroutine(HideLoadingScreenAfterDelay());
        }

        // =====================================
        // local (non-networked) async scene loading
        // =====================================

        private IEnumerator LoadSceneLocalAsync(string sceneName)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"SceneTransitionManager: Failed to start async load for '{sceneName}'");
                HideLoadingScreen();
                yield break;
            }

            while (!op.isDone)
            {
                // unity reports 0-0.9 during load, then jumps to 1 on activation
                float progress = Mathf.Clamp01(op.progress / 0.9f);
                if (loadingScreenUI != null)
                    loadingScreenUI.UpdateProgress(progress);
                yield return null;
            }

            if (loadingScreenUI != null)
                loadingScreenUI.UpdateProgress(1f);

            OnSceneLoadCompleted?.Invoke(sceneName);

            yield return new WaitForSecondsRealtime(postLoadDelay);

            HideLoadingScreen();

            // re-subscribe to network events in case we returned to menu and will host again
            _subscribedToNGOLoad = false;
        }

        // =====================================
        // progress tracking coroutine for networked loads
        // =====================================

        private IEnumerator TrackProgress()
        {
            while (_currentAsyncOp != null && !_currentAsyncOp.isDone)
            {
                float progress = Mathf.Clamp01(_currentAsyncOp.progress / 0.9f);
                if (loadingScreenUI != null)
                    loadingScreenUI.UpdateProgress(progress);
                yield return null;
            }

            _progressCoroutine = null;
        }

        // =====================================
        // loading screen helpers
        // =====================================

        private void ShowLoadingScreen(string status)
        {
            if (loadingScreenUI == null) return;

            if (!loadingScreenUI.IsVisible)
                loadingScreenUI.Show(status);
            else
                loadingScreenUI.SetStatus(status);
        }

        private void HideLoadingScreen()
        {
            if (loadingScreenUI != null)
                loadingScreenUI.Hide();
        }

        private IEnumerator HideLoadingScreenAfterDelay()
        {
            if (loadingScreenUI != null)
                loadingScreenUI.SetStatus("Preparing game...");

            yield return new WaitForSecondsRealtime(postLoadDelay);

            HideLoadingScreen();
        }

        // =====================================
        // utility
        // =====================================
        
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
