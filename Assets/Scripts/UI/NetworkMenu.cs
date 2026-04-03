using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using System;
using System.Collections;
using Category5.Core;
using Category5.Player;
using DG.Tweening; // for UI animations

namespace Category5.UI
{
    // handles main menu networking ui for host/join functionality
    public class NetworkMenu : MonoBehaviour
    {
        [Header("ui references - title screen")]
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject titleLogoImage;
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("ui references - main menu")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private Button backToTitleButton;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_InputField joinCodeInputField;
        [SerializeField] private TMP_InputField playerNameInputField;

        [Header("lobby")]
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private Button startGameButton; // host only - shown/hidden by ShowLobby
        [SerializeField] private Button readyButton;
        [SerializeField] private TextMeshProUGUI readyButtonText;
        [SerializeField] private TextMeshProUGUI allPlayersReadyText;

        [Header("lobby panels")]
        [SerializeField] private LobbyTabController lobbyTabController;     // top-left icon bar (leave, chat, settings)
        [SerializeField] private CharacterSelectPanel characterSelectPanel; // scrollable left panel
        [SerializeField] private CharacterViewPanel characterViewPanel;     // hover overlay (starts hidden)
        [SerializeField] private LobbyPartyPanel lobbyPartyPanel;           // party portrait panel + join code header
        [SerializeField] private LobbySettingsPanel lobbySettingsPanel;     // settings overlay
        [SerializeField] private ChatToggleController chatToggleController; // indicator + chat panel toggle

        [Header("status & connecting")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject connectingPanel;
        [SerializeField] private TextMeshProUGUI connectingStatusText;
        [SerializeField] private Button cancelConnectionButton;

        [Header("settings")]
        [SerializeField] private string gameSceneName = "SampleScene";
        [SerializeField] private int maxRelayConnections = 4;
        [SerializeField] private float connectionTimeout = 10f;

        [Header("UI Animations")]
        [SerializeField] private float panelFadeDuration = 0.2f;

        private UnityTransport transport;
        private bool isInLobby = false;
        private bool isConnecting = false;
        private bool isRelayReady = false;
        private string currentJoinCode;
        private Coroutine connectionTimeoutCoroutine;

        private void Awake()
        {
            // hide the lobby immediately so it never flashes on title/main menu
            if (lobbyPanel != null)
                HideUI(lobbyPanel);
        }

        private void Start()
        {
            // ensure cursor is visible in menu
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // ensure PlayerNameManager exists
            EnsurePlayerNameManager();

            // cache the transport reference
            if (NetworkManager.Singleton != null)
            {
                transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            }

            // initialize relay services
            InitializeRelayAsync();

            // load saved player name
            if (playerNameInputField != null)
            {
                string savedName = PlayerNameManager.Instance != null
                    ? PlayerNameManager.Instance.GetDisplayName()
                    : "Player";
                playerNameInputField.text = savedName;
                playerNameInputField.onEndEdit.AddListener(OnPlayerNameChanged);
            }

            // setup title screen button listeners
            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlayButtonClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnSettingsButtonClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnQuitButtonClicked);
            }

            if (backToTitleButton != null)
            {
                backToTitleButton.onClick.AddListener(ShowTitleScreen);
            }

            // setup button listeners
            if (hostButton != null)
            {
                hostButton.onClick.AddListener(OnHostClicked);
            }

            if (joinButton != null)
            {
                joinButton.onClick.AddListener(OnJoinClicked);
            }

            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameClicked);
            }

            // leave lobby handled by icon bar event
            LobbyTabController.OnLeaveLobbyClicked += OnLeaveLobbyClicked;

            if (cancelConnectionButton != null)
            {
                cancelConnectionButton.onClick.AddListener(OnCancelConnectionClicked);
            }

            // setup ready button listener
            if (readyButton != null)
            {
                readyButton.onClick.AddListener(OnReadyButtonClicked);
            }

            // hide connecting panel initially
            if (connectingPanel != null)
            {
                HideUI(connectingPanel);
            }

            // hide cancel button initially
            SetCancelButtonVisible(false);

            // show title screen initially
            ShowTitleScreen();
        }

        // ensures PlayerNameManager singleton exists
        private void EnsurePlayerNameManager()
        {
            if (PlayerNameManager.Instance == null)
            {
                var go = new GameObject("PlayerNameManager");
                go.AddComponent<PlayerNameManager>();
                // PlayerNameManager handles its own DontDestroyOnLoad
            }
        }

        // called when player name input field loses focus
        private void OnPlayerNameChanged(string newName)
        {
            if (PlayerNameManager.Instance != null)
            {
                PlayerNameManager.Instance.SetLocalPlayerName(newName);
                // Debug.Log($"NetworkMenu: Player name changed to '{newName}'");
            }
        }

        private void OnDestroy()
        {
            // cleanup title screen listeners
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayButtonClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(OnSettingsButtonClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnQuitButtonClicked);
            }

            if (backToTitleButton != null)
            {
                backToTitleButton.onClick.RemoveListener(ShowTitleScreen);
            }

            // cleanup listeners
            if (hostButton != null)
            {
                hostButton.onClick.RemoveListener(OnHostClicked);
            }

            if (joinButton != null)
            {
                joinButton.onClick.RemoveListener(OnJoinClicked);
            }

            if (startGameButton != null)
            {
                startGameButton.onClick.RemoveListener(OnStartGameClicked);
            }

            LobbyTabController.OnLeaveLobbyClicked -= OnLeaveLobbyClicked;

            if (cancelConnectionButton != null)
            {
                cancelConnectionButton.onClick.RemoveListener(OnCancelConnectionClicked);
            }

            if (readyButton != null)
            {
                readyButton.onClick.RemoveListener(OnReadyButtonClicked);
            }

            if (playerNameInputField != null)
            {
                playerNameInputField.onEndEdit.RemoveListener(OnPlayerNameChanged);
            }

            // unsubscribe from lobby events
            LobbyManager.OnLobbyPlayersChanged -= RefreshPlayerList;

            // unsubscribe from network events
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnLobbyClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnLobbyClientDisconnected;
            }
        }

        // called when host button is clicked
        // creates a relay allocation and starts hosting
        public async void OnHostClicked()
        {
            if (NetworkManager.Singleton == null)
            {
                UpdateStatus("Error: NetworkManager not found!");
                Debug.LogError("NetworkMenu: NetworkManager.Singleton is null");
                return;
            }

            if (!isRelayReady)
            {
                UpdateStatus("Services still initializing, please wait...");
                return;
            }

            UpdateStatus("Creating relay...");
            SetButtonsInteractable(false);

            try
            {
                // create relay allocation and get join code
                var (joinCode, serverData) = await RelayHelper.CreateRelayAsync(maxRelayConnections);
                currentJoinCode = joinCode;

                // configure transport to use relay
                transport.SetRelayServerData(serverData);

                // register callback for when host starts
                NetworkManager.Singleton.OnServerStarted += OnServerStarted;

                bool success = NetworkManager.Singleton.StartHost();

                if (!success)
                {
                    UpdateStatus("Failed to start host.");
                    Debug.LogError("NetworkMenu: Failed to start host");
                    SetButtonsInteractable(true);
                    NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
                    currentJoinCode = null;
                }
            }
            catch (Exception e)
            {
                UpdateStatus($"Failed to create relay: {e.Message}");
                Debug.LogError($"NetworkMenu: Relay creation failed - {e}");
                SetButtonsInteractable(true);
            }
        }

        // called when start game button is clicked (host only)
        public void OnStartGameClicked()
        {
            if (!NetworkManager.Singleton.IsHost)
            {
                Debug.LogWarning("NetworkMenu: Only the host can start the game");
                return;
            }

            // check if all players are ready
            if (LobbyManager.Instance != null && !LobbyManager.Instance.AreAllPlayersReady())
            {
                UpdateStatus("Cannot start - not all players are ready!");
                Debug.LogWarning("NetworkMenu: Cannot start game - not all players are ready");
                return;
            }

            // Debug.Log("NetworkMenu: Host starting the game");

            // load the game scene for all clients via SceneTransitionManager
            // this shows the loading screen and handles player spawning after load
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.LoadGameScene();
            }
            else if (NetworkManager.Singleton.SceneManager != null)
            {
                // fallback if SceneTransitionManager is missing
                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("NetworkMenu: SceneManager is null");
            }
        }

        // called when leave lobby button is clicked
        public void OnLeaveLobbyClicked()
        {
            // Debug.Log("NetworkMenu: Leaving lobby");

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            isInLobby = false;
            ShowMainMenu();
            UpdateStatus("Left lobby");
        }

        // called when join button is clicked
        // joins a relay using the provided join code
        public async void OnJoinClicked()
        {
            if (NetworkManager.Singleton == null)
            {
                UpdateStatus("Error: NetworkManager not found!");
                Debug.LogError("NetworkMenu: NetworkManager.Singleton is null");
                return;
            }

            if (!isRelayReady)
            {
                UpdateStatus("Services still initializing, please wait...");
                return;
            }

            string joinCode = joinCodeInputField != null ? joinCodeInputField.text : "";

            // validate join code
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                UpdateStatus("Please enter a join code");
                return;
            }

            UpdateStatus($"Joining relay...");
            SetButtonsInteractable(false);
            ShowConnectingPanel(true);
            SetCancelButtonVisible(true);
            isConnecting = true;

            try
            {
                // join the relay allocation
                var serverData = await RelayHelper.JoinRelayAsync(joinCode);

                // configure transport to use relay
                transport.SetRelayServerData(serverData);

                // register callbacks for connection result
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

                bool success = NetworkManager.Singleton.StartClient();

                if (!success)
                {
                    UpdateStatus("Failed to start client");
                    Debug.LogError("NetworkMenu: Failed to start client");
                    SetButtonsInteractable(true);
                    ShowConnectingPanel(false);
                    isConnecting = false;
                    UnregisterClientCallbacks();
                }
                else
                {
                    // start timeout countdown
                    connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeoutCoroutine(joinCode));
                }
            }
            catch (Exception e)
            {
                UpdateStatus($"Failed to join relay: {e.Message}");
                Debug.LogError($"NetworkMenu: Relay join failed - {e}");
                SetButtonsInteractable(true);
                ShowConnectingPanel(false);
                SetCancelButtonVisible(false);
                isConnecting = false;
            }
        }

        // coroutine that handles connection timeout with countdown display
        private IEnumerator ConnectionTimeoutCoroutine(string joinCode)
        {
            float elapsed = 0f;

            while (elapsed < connectionTimeout && isConnecting)
            {
                elapsed += Time.deltaTime;
                float remaining = connectionTimeout - elapsed;

                // update connecting status with countdown
                if (connectingStatusText != null)
                {
                    connectingStatusText.text = $"Joining {joinCode}...\n({Mathf.CeilToInt(remaining)}s)";
                }
                else
                {
                    UpdateStatus($"Joining {joinCode}... ({Mathf.CeilToInt(remaining)}s)");
                }

                yield return null;
            }

            // if still connecting after timeout, cancel
            if (isConnecting)
            {
                CancelConnection("Connection timed out. Check the join code and try again.");
            }
        }

        // called when cancel button is clicked
        public void OnCancelConnectionClicked()
        {
            if (!isConnecting) return;

            // Debug.Log("NetworkMenu: Connection cancelled by user");
            CancelConnection("Connection cancelled");
        }

        // cancels the current connection attempt
        private void CancelConnection(string reason)
        {
            isConnecting = false;

            // stop timeout coroutine
            if (connectionTimeoutCoroutine != null)
            {
                StopCoroutine(connectionTimeoutCoroutine);
                connectionTimeoutCoroutine = null;
            }

            // shutdown network
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                NetworkManager.Singleton.Shutdown();
            }

            UnregisterClientCallbacks();
            ShowConnectingPanel(false);
            SetCancelButtonVisible(false);
            SetButtonsInteractable(true);
            UpdateStatus(reason);
        }

        // shows or hides the cancel connection button
        private void SetCancelButtonVisible(bool visible)
        {
            if (cancelConnectionButton != null)
            {
                cancelConnectionButton.gameObject.SetActive(visible);
            }
        }

        private void OnServerStarted()
        {
            // Debug.Log("NetworkMenu: Server started successfully, entering lobby");
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;

            // subscribe to client connect/disconnect for player count updates
            NetworkManager.Singleton.OnClientConnectedCallback += OnLobbyClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnLobbyClientDisconnected;

            // initialize lobby manager for host
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.Initialize();
            }

            // show the lobby instead of immediately loading the game
            ShowLobby(true);

            // pass join code to party panel header so host can share it with friends
            if (lobbyPartyPanel != null)
                lobbyPartyPanel.SetJoinCode(currentJoinCode ?? "");

            UpdateStatus("Waiting for players to join...");
        }

        private void OnClientConnected(ulong clientId)
        {
            // only care about local client connection
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                // Debug.Log($"NetworkMenu: Connected to server as client {clientId}");

                // stop timeout coroutine - we connected successfully
                isConnecting = false;
                if (connectionTimeoutCoroutine != null)
                {
                    StopCoroutine(connectionTimeoutCoroutine);
                    connectionTimeoutCoroutine = null;
                }

                ShowConnectingPanel(false);
                SetCancelButtonVisible(false);

                // initialize lobby manager for client and send our name
                if (LobbyManager.Instance != null)
                {
                    LobbyManager.Instance.Initialize();
                    LobbyManager.Instance.SendLocalPlayerName();
                }

                // show lobby as client
                ShowLobby(false);
                UpdateStatus("Connected! Waiting for host to start game...");

                UnregisterClientCallbacks();
            }
        }

        private void OnLobbyClientConnected(ulong clientId)
        {
            // Debug.Log($"NetworkMenu: Player {clientId} joined the lobby");
        }

        private void OnLobbyClientDisconnected(ulong clientId)
        {
            // Debug.Log($"NetworkMenu: Player {clientId} left the lobby");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            // only care about local client disconnection
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                // Debug.Log("NetworkMenu: Disconnected from server");

                // if we were still trying to connect, this is a connection failure
                if (isConnecting)
                {
                    CancelConnection("Connection failed. Host may not be available.");
                }
                else
                {
                    // we were already connected and got disconnected
                    UpdateStatus("Disconnected from server");
                    SetButtonsInteractable(true);
                    ShowConnectingPanel(false);
                    UnregisterClientCallbacks();
                }
            }
        }

        private void UnregisterClientCallbacks()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            // Debug.Log($"NetworkMenu: {message}");
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (hostButton != null) hostButton.interactable = interactable;
            if (joinButton != null) joinButton.interactable = interactable;
            if (joinCodeInputField != null) joinCodeInputField.interactable = interactable;
        }

        private void ShowConnectingPanel(bool show)
        {
            if (connectingPanel != null)
            {
                ShowUI(connectingPanel);
            }
        }

        private void ShowMainMenu()
        {
            isInLobby = false;

            // unsubscribe from lobby events
            LobbyManager.OnLobbyPlayersChanged -= RefreshPlayerList;

            // cleanup lobby manager
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.Cleanup();
            }

            // cleanup new lobby panels
            CleanupLobbyPanels();

            if (mainMenuPanel != null)
            {
                ShowUI(mainMenuPanel);
            }

            if (lobbyPanel != null)
            {
                HideUI(lobbyPanel);
            }

            // hide title panel when showing main menu
            if (titlePanel != null)
            {
                HideUI(titlePanel);
            }

            SetButtonsInteractable(true);
        }

        // shows the title screen and hides other panels
        private void ShowTitleScreen()
        {
            isInLobby = false;

            // shutdown network if connected
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            // unsubscribe from lobby events
            LobbyManager.OnLobbyPlayersChanged -= RefreshPlayerList;

            // cleanup lobby manager
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.Cleanup();
            }

            // cleanup new lobby panels
            CleanupLobbyPanels();

            if (titlePanel != null)
            {
                ShowUI(titlePanel);
            }

            if (titleLogoImage != null)
            {
                titleLogoImage.SetActive(true);
            }

            if (mainMenuPanel != null)
            {
                HideUI(mainMenuPanel);
            }

            if (lobbyPanel != null)
            {
                HideUI(lobbyPanel);
            }
        }

        // called when play button on title screen is clicked
        public void OnPlayButtonClicked()
        {
            if (titlePanel != null)
            {
                HideUI(titlePanel);
            }

            if (titleLogoImage != null)
            {
                titleLogoImage.SetActive(false);
            }

            if (mainMenuPanel != null)
            {
                //ShowUI(mainMenuPanel);

                // call animation for main menu panel
                AnimatePanelIn(mainMenuPanel);
            }

            SetButtonsInteractable(isRelayReady);
            UpdateStatus(isRelayReady ? "Ready to connect" : "Connecting to services...");
        }

        // placeholder for settings button
        public void OnSettingsButtonClicked()
        {
            // todo: implement settings panel
        }

        // placeholder for quit button
        public void OnQuitButtonClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ShowLobby(bool isHost)
        {
            isInLobby = true;

            if (mainMenuPanel != null)
            {
                HideUI(mainMenuPanel);
            }

            if (lobbyPanel != null)
            {
                ShowUI(lobbyPanel);
            }

            // initialize lobby panels
            InitializeLobbyPanels();

            // only the host can start the game (visible but may be disabled until all ready)
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(isHost);
            }

            // show ready button for all players
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(true);
                UpdateReadyButtonVisual();
            }

            // subscribe to lobby player changes
            LobbyManager.OnLobbyPlayersChanged += RefreshPlayerList;

            RefreshPlayerList();
            UpdateStartButtonState();
        }

        // initialize all lobby panel components
        private void InitializeLobbyPanels()
        {
            // initialize icon bar
            if (lobbyTabController != null)
            {
                lobbyTabController.Initialize();
            }

            // initialize character select (scrollable list)
            if (characterSelectPanel != null)
            {
                characterSelectPanel.Initialize();
            }

            // ensure character view panel starts hidden
            if (characterViewPanel != null)
            {
                HideUI(characterViewPanel.gameObject);
            }

            // initialize party panel
            if (lobbyPartyPanel != null)
            {
                lobbyPartyPanel.Initialize();
                lobbyPartyPanel.SetJoinCode(currentJoinCode ?? "");
            }

            // initialize settings (opened via gear icon)
            if (lobbySettingsPanel != null)
            {
                lobbySettingsPanel.Initialize();
            }

            // initialize chat indicator + panel toggle
            if (chatToggleController != null)
            {
                chatToggleController.Initialize();
            }
            else
            {
                Debug.LogError("NetworkMenu: chatToggleController is null - assign it in the inspector on NetworkMenu");
            }
        }

        // cleanup lobby panels when leaving
        private void CleanupLobbyPanels()
        {
            // hide character view panel
            if (characterViewPanel != null)
            {
                HideUI(characterViewPanel.gameObject);
            }

            // cleanup chat (disables input action)
            if (chatToggleController != null)
            {
                chatToggleController.Cleanup();
            }
        }

        // called when LobbyManager.OnLobbyPlayersChanged fires
        // LobbyPartyPanel handles the visual list itself - we just update button states here
        private void RefreshPlayerList()
        {
            UpdateStartButtonState();
            UpdateReadyButtonVisual();
        }

        // called when ready button is clicked
        private void OnReadyButtonClicked()
        {
            if (LobbyManager.Instance == null) return;

            // toggle ready state
            bool currentReady = LobbyManager.Instance.IsLocalPlayerReady();
            LobbyManager.Instance.SendLocalPlayerReady(!currentReady);

            UpdateReadyButtonVisual();
        }

        // update ready button text based on current state
        // button is disabled until the player has selected a class
        private void UpdateReadyButtonVisual()
        {
            if (readyButtonText == null || LobbyManager.Instance == null) return;
            if (NetworkManager.Singleton == null) return;

            int classId = LobbyManager.Instance.GetPlayerClassId(NetworkManager.Singleton.LocalClientId);
            bool hasClass = classId != PlayerClass.NoClassId;

            if (!hasClass)
            {
                readyButtonText.text = "Select a class";
                if (readyButton != null)
                {
                    readyButton.interactable = false;
                    var colors = readyButton.colors;
                    colors.normalColor = new Color(0.5f, 0.5f, 0.5f);
                    readyButton.colors = colors;
                }
                return;
            }

            if (readyButton != null) readyButton.interactable = true;
            bool isReady = LobbyManager.Instance.IsLocalPlayerReady();
            readyButtonText.text = isReady ? "Unready" : "Ready!";

            if (readyButton != null)
            {
                var colors = readyButton.colors;
                colors.normalColor = isReady ? new Color(0.8f, 0.4f, 0.4f) : new Color(0.4f, 0.8f, 0.4f);
                readyButton.colors = colors;
            }
        }

        // update start button interactability based on all players ready
        private void UpdateStartButtonState()
        {
            bool allReady = LobbyManager.Instance != null && LobbyManager.Instance.AreAllPlayersReady();

            // update ready text for everyone
            if (allPlayersReadyText != null)
            {
                allPlayersReadyText.text = allReady ? "All players ready!" : "Waiting for players...";
                allPlayersReadyText.color = allReady ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.8f, 0.4f);
            }

            // start button is host-only
            if (startGameButton == null || !NetworkManager.Singleton.IsHost) return;
            startGameButton.interactable = allReady;
        }

        // initialize relay services in the background
        private async void InitializeRelayAsync()
        {
            UpdateStatus("Connecting to services...");
            SetButtonsInteractable(false);

            try
            {
                await RelayHelper.InitializeAsync();
                isRelayReady = true;
                UpdateStatus("Ready to connect");
                SetButtonsInteractable(true);
            }
            catch (Exception e)
            {
                UpdateStatus($"Service error: {e.Message}");
                Debug.LogError($"NetworkMenu: Failed to initialize relay services - {e}");
            }
        }

        private void AnimatePanelIn(GameObject panel) // DOTween animation for fading in panels (used on main menu panel when coming from title screen)
        {
            if (panel != null)
            {
                CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.DOFade(1, panelFadeDuration);
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }
            }
        }

        private void HideUI(GameObject panel) // helper to hide all UI elements in a panel 
        {
            if (panel != null)
            {
                CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }
            }
        }

        private void ShowUI(GameObject panel) // helper to show all UI elements in a panel
        {
            if (panel != null)
            {
                CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }
            }
        }
    }
}
    


