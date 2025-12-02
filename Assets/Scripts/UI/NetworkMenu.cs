using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using System.Collections;
using Category5.Core;

namespace Category5.UI
{
    // handles main menu networking ui for host/join functionality
    public class NetworkMenu : MonoBehaviour
    {
        [Header("ui references - main menu")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_InputField ipInputField;
        [SerializeField] private TMP_InputField portInputField;
        [SerializeField] private TMP_InputField playerNameInputField;
        
        [Header("ui references - lobby")]
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private Button startGameButton; // host only
        [SerializeField] private Button leaveLobbyButton;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private Transform playerListContainer; // parent for player entries
        [SerializeField] private LobbyPlayerEntry playerEntryPrefab; // prefab for each player entry
        
        [Header("status display")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject connectingPanel; // optional panel to show while connecting
        
        [Header("settings")]
        [SerializeField] private string gameSceneName = "SampleScene";
        [SerializeField] private string defaultIP = "127.0.0.1";
        [SerializeField] private ushort defaultPort = 7777;
        
        [Header("connection timeout")]
        [SerializeField] private float connectionTimeout = 10f;
        [SerializeField] private Button cancelConnectionButton;
        [SerializeField] private TextMeshProUGUI connectingStatusText; // shows countdown (kinda optional if we want)
        
        private UnityTransport transport;
        private bool isInLobby = false;
        private bool isConnecting = false;
        private Coroutine connectionTimeoutCoroutine;
        
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
            
            // set default values
            if (ipInputField != null)
            {
                ipInputField.text = defaultIP;
            }
            
            if (portInputField != null)
            {
                portInputField.text = defaultPort.ToString();
            }
            
            // load saved player name
            if (playerNameInputField != null)
            {
                string savedName = PlayerNameManager.Instance != null 
                    ? PlayerNameManager.Instance.GetDisplayName() 
                    : "Player";
                playerNameInputField.text = savedName;
                playerNameInputField.onEndEdit.AddListener(OnPlayerNameChanged);
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
            
            if (leaveLobbyButton != null)
            {
                leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
            }
            
            if (cancelConnectionButton != null)
            {
                cancelConnectionButton.onClick.AddListener(OnCancelConnectionClicked);
            }
            
            // hide connecting panel initially
            if (connectingPanel != null)
            {
                connectingPanel.SetActive(false);
            }
            
            // hide cancel button initially
            SetCancelButtonVisible(false);
            
            // show main menu, hide lobby
            ShowMainMenu();
            
            UpdateStatus("Ready to connect");
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
                Debug.Log($"NetworkMenu: Player name changed to '{newName}'");
            }
        }
        
        private void Update()
        {
            // update player count while in lobby
            if (isInLobby && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                UpdatePlayerCount();
            }
        }
        
        private void OnDestroy()
        {
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
            
            if (leaveLobbyButton != null)
            {
                leaveLobbyButton.onClick.RemoveListener(OnLeaveLobbyClicked);
            }
            
            if (cancelConnectionButton != null)
            {
                cancelConnectionButton.onClick.RemoveListener(OnCancelConnectionClicked);
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
        // starts hosting and shows the lobby
        public void OnHostClicked()
        {
            if (NetworkManager.Singleton == null)
            {
                UpdateStatus("Error: NetworkManager not found!");
                Debug.LogError("NetworkMenu: NetworkManager.Singleton is null");
                return;
            }
            
            // configure port if specified
            if (transport != null && portInputField != null)
            {
                if (ushort.TryParse(portInputField.text, out ushort port))
                {
                    transport.SetConnectionData(defaultIP, port);
                }
            }
            
            UpdateStatus("Starting host...");
            SetButtonsInteractable(false);
            
            // register callback for when host starts
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            
            bool success = NetworkManager.Singleton.StartHost();
            
            if (!success)
            {
                UpdateStatus("Failed to start host. Port may be in use.");
                Debug.LogError("NetworkMenu: Failed to start host");
                SetButtonsInteractable(true);
                NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
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
            
            Debug.Log("NetworkMenu: Host starting the game");
            UpdateStatus("Loading game...");
            
            // load the game scene for all clients
            // player spawning is handled by NetworkManagerBootstrap after scene loads
            if (NetworkManager.Singleton.SceneManager != null)
            {
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
            Debug.Log("NetworkMenu: Leaving lobby");
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
            
            isInLobby = false;
            ShowMainMenu();
            UpdateStatus("Left lobby");
        }
        
        // called when join button is clicked
        // connects to a host at the specified ip address
        public void OnJoinClicked()
        {
            if (NetworkManager.Singleton == null)
            {
                UpdateStatus("Error: NetworkManager not found!");
                Debug.LogError("NetworkMenu: NetworkManager.Singleton is null");
                return;
            }
            
            string ip = ipInputField != null ? ipInputField.text : defaultIP;
            ushort port = defaultPort;
            
            if (portInputField != null)
            {
                ushort.TryParse(portInputField.text, out port);
            }
            
            // validate ip input
            if (string.IsNullOrWhiteSpace(ip))
            {
                UpdateStatus("Please enter a valid IP address");
                return;
            }
            
            // set connection data
            if (transport != null)
            {
                transport.SetConnectionData(ip, port);
                Debug.Log($"NetworkMenu: Connecting to {ip}:{port}");
            }
            
            UpdateStatus($"Connecting to {ip}:{port}...");
            SetButtonsInteractable(false);
            ShowConnectingPanel(true);
            SetCancelButtonVisible(true);
            isConnecting = true;
            
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
                connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeoutCoroutine(ip, port));
            }
        }
        
        // coroutine that handles connection timeout with countdown display
        private IEnumerator ConnectionTimeoutCoroutine(string ip, ushort port)
        {
            float elapsed = 0f;
            
            while (elapsed < connectionTimeout && isConnecting)
            {
                elapsed += Time.deltaTime;
                float remaining = connectionTimeout - elapsed;
                
                // update connecting status with countdown
                if (connectingStatusText != null)
                {
                    connectingStatusText.text = $"Connecting to {ip}:{port}...\n({Mathf.CeilToInt(remaining)}s)";
                }
                else
                {
                    UpdateStatus($"Connecting to {ip}:{port}... ({Mathf.CeilToInt(remaining)}s)");
                }
                
                yield return null;
            }
            
            // if still connecting after timeout, cancel
            if (isConnecting)
            {
                Debug.Log("NetworkMenu: Connection timed out");
                CancelConnection("Connection timed out. Check the IP address and port.");
            }
        }
        
        // called when cancel button is clicked
        public void OnCancelConnectionClicked()
        {
            if (!isConnecting) return;
            
            Debug.Log("NetworkMenu: Connection cancelled by user");
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
            Debug.Log("NetworkMenu: Server started successfully, entering lobby");
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
            UpdateStatus("Waiting for players to join...");
        }
        
        private void OnClientConnected(ulong clientId)
        {
            // only care about local client connection
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log($"NetworkMenu: Connected to server as client {clientId}");
                
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
            Debug.Log($"NetworkMenu: Player {clientId} joined the lobby");
            UpdatePlayerCount();
        }
        
        private void OnLobbyClientDisconnected(ulong clientId)
        {
            Debug.Log($"NetworkMenu: Player {clientId} left the lobby");
            UpdatePlayerCount();
        }
        
        private void OnClientDisconnected(ulong clientId)
        {
            // only care about local client disconnection
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("NetworkMenu: Disconnected from server");
                
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
            Debug.Log($"NetworkMenu: {message}");
        }
        
        private void SetButtonsInteractable(bool interactable)
        {
            if (hostButton != null) hostButton.interactable = interactable;
            if (joinButton != null) joinButton.interactable = interactable;
            if (ipInputField != null) ipInputField.interactable = interactable;
            if (portInputField != null) portInputField.interactable = interactable;
        }
        
        private void ShowConnectingPanel(bool show)
        {
            if (connectingPanel != null)
            {
                connectingPanel.SetActive(show);
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
            
            // clear player list
            ClearPlayerList();
            
            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(true);
            }
            
            if (lobbyPanel != null)
            {
                lobbyPanel.SetActive(false);
            }
            
            SetButtonsInteractable(true);
        }
        
        private void ShowLobby(bool isHost)
        {
            isInLobby = true;
            
            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(false);
            }
            
            if (lobbyPanel != null)
            {
                lobbyPanel.SetActive(true);
            }
            
            // only the host can start the game
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(isHost);
            }
            
            // subscribe to lobby player changes
            LobbyManager.OnLobbyPlayersChanged += RefreshPlayerList;
            
            UpdatePlayerCount();
            RefreshPlayerList();
        }
        
        private void UpdatePlayerCount()
        {
            if (playerCountText != null)
            {
                int playerCount = LobbyManager.Instance != null 
                    ? LobbyManager.Instance.GetPlayerCount() 
                    : (NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 0);
                playerCountText.text = $"Players: {playerCount}/4";
            }
        }
        
        // refreshes the player list ui from LobbyManager data
        private void RefreshPlayerList()
        {
            if (playerListContainer == null || playerEntryPrefab == null)
            {
                    Debug.LogWarning("NetworkMenu: playerListContainer or playerEntryPrefab is null");
                return;
            }
            
            // clear existing entries
            ClearPlayerList();
            
            // get players from lobby manager
            if (LobbyManager.Instance == null)
            {
                Debug.LogWarning("NetworkMenu: LobbyManager.Instance is null");
                return;
            }
            
            var players = LobbyManager.Instance.GetLobbyPlayers();
            ulong localClientId = NetworkManager.Singleton?.LocalClientId ?? 0;
            
            Debug.Log($"NetworkMenu: Refreshing player list with {players.Length} players");
            
            foreach (var player in players)
            {
                var entryGO = Instantiate(playerEntryPrefab.gameObject, playerListContainer);
                
                // reset scale and anchors to work properly with layout group
                var rectTransform = entryGO.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.localScale = Vector3.one;
                }
                
                var entry = entryGO.GetComponent<LobbyPlayerEntry>();
                if (entry != null)
                {
                    string playerName = player.PlayerName.ToString();
                    Debug.Log($"NetworkMenu: Setting up entry for '{playerName}' (host: {player.IsHost}, local: {player.ClientId == localClientId})");
                    entry.Setup(playerName, player.IsHost, player.ClientId == localClientId);
                }
                else
                {
                    Debug.LogError("NetworkMenu: LobbyPlayerEntry component not found on instantiated prefab");
                }
            }
            
            // force layout rebuild
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(playerListContainer as RectTransform);
            
            UpdatePlayerCount();
        }
        
        // clears all player entries from the list
        private void ClearPlayerList()
        {
            if (playerListContainer == null) return;
            
            foreach (Transform child in playerListContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
