using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

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
        
        [Header("ui references - lobby")]
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private Button startGameButton; // host only
        [SerializeField] private Button leaveLobbyButton;
        [SerializeField] private TextMeshProUGUI playerCountText;
        
        [Header("status display")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject connectingPanel; // optional panel to show while connecting
        
        [Header("settings")]
        [SerializeField] private string gameSceneName = "SampleScene";
        [SerializeField] private string defaultIP = "127.0.0.1";
        [SerializeField] private ushort defaultPort = 7777;
        
        private UnityTransport transport;
        private bool isInLobby = false;
        
        private void Start()
        {
            // ensure cursor is visible in menu
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
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
            
            // hide connecting panel initially
            if (connectingPanel != null)
            {
                connectingPanel.SetActive(false);
            }
            
            // show main menu, hide lobby
            ShowMainMenu();
            
            UpdateStatus("Ready to connect");
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
                UnregisterClientCallbacks();
            }
        }
        
        private void OnServerStarted()
        {
            Debug.Log("NetworkMenu: Server started successfully, entering lobby");
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
            
            // subscribe to client connect/disconnect for player count updates
            NetworkManager.Singleton.OnClientConnectedCallback += OnLobbyClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnLobbyClientDisconnected;
            
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
                ShowConnectingPanel(false);
                
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
                UpdateStatus("Disconnected from server");
                SetButtonsInteractable(true);
                ShowConnectingPanel(false);
                UnregisterClientCallbacks();
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
            
            UpdatePlayerCount();
        }
        
        private void UpdatePlayerCount()
        {
            if (playerCountText != null && NetworkManager.Singleton != null)
            {
                int playerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
                playerCountText.text = $"Players: {playerCount}/4";
            }
        }
    }
}
