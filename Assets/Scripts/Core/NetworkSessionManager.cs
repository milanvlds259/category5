using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using Category5.Player;
using Category5.UI;

namespace Category5.Core
{
    // manages network session state and handles disconnections during gameplay
    // this component is FOR THE GAME SCENE (not the menu!!!!!!)
    public class NetworkSessionManager : NetworkBehaviour
    {
        public static NetworkSessionManager Instance { get; private set; }
        
        [Header("settings")]
        [SerializeField] private string menuSceneName = "MainMenu";
        [SerializeField] private float disconnectNotificationDuration = 3f;
        [SerializeField] private float returnToMenuDelay = 2f;
        
        // events for ui to subscribe to
        public static event Action<ulong, string> OnPlayerDisconnected; // clientId, reason
        public static event Action<string> OnHostDisconnected; // reason
        public static event Action<ulong> OnPlayerReconnected; // clientId (for future use)
        
        // track connected players
        private bool _isGameActive = false;
        private bool _isShuttingDown = false;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            _isGameActive = true;
            _isShuttingDown = false;
            
            // subscribe to disconnect events
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            
            // for clients, also detect if we get disconnected
            if (!IsServer)
            {
                NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
            }
            
            // Debug.Log("NetworkSessionManager: Session started, monitoring for disconnects");
        }
        
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
                NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
            }
        }
        
        private new void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        // called when any client disconnects (server sees all, client sees only self)
        private void OnClientDisconnect(ulong clientId)
        {
            if (!_isGameActive || _isShuttingDown) return;
            
            // Debug.Log($"NetworkSessionManager: Client {clientId} disconnected");
            
            if (IsServer)
            {
                // server handles the disconnect
                HandlePlayerDisconnectOnServer(clientId);
            }
            else
            {
                // client disconnected - check if it's us (host left) or we lost connection
                if (clientId == NetworkManager.Singleton.LocalClientId)
                {
                    HandleLocalDisconnect("Connection lost");
                }
            }
        }
        
        // called when transport layer fails (client-side)
        private void OnTransportFailure()
        {
            if (!_isGameActive || _isShuttingDown) return;
            
            // Debug.Log("NetworkSessionManager: Transport failure detected");
            HandleLocalDisconnect("Connection to host lost");
        }
        
        // server-side: handle when a player disconnects
        private void HandlePlayerDisconnectOnServer(ulong clientId)
        {
            // get player name before cleanup (player object may still exist briefly)
            string playerName = GetPlayerNameForClient(clientId);
            
            // notify all clients about the disconnect
            NotifyPlayerDisconnectedClientRpc(clientId, playerName);
            
            // clean up the disconnected player's game state
            CleanupDisconnectedPlayer(clientId);
            
            // check if game should continue
            CheckGameViability();
        }
        
        // get the player name for a given client id
        private string GetPlayerNameForClient(ulong clientId)
        {
            // try UIManager first (most reliable)
            if (UIManager.Instance != null)
            {
                return UIManager.Instance.GetPlayerName(clientId);
            }
            
            // try to find the player object directly (if above method fails for some reason)
            if (NetworkManager.Singleton != null && 
                NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                var player = client.PlayerObject?.GetComponent<PlayerController>();
                if (player != null)
                {
                    string name = player.GetPlayerName();
                    if (!string.IsNullOrWhiteSpace(name) && name != "Player")
                    {
                        return name;
                    }
                }
            }
            
            return $"Player {clientId}";
        }
        
        // clean up a disconnected player's state
        private void CleanupDisconnectedPlayer(ulong clientId)
        {
            if (!IsServer) return;
            
            // the player object should be automatically despawned by NGO
            // but we need to update game systems
            
            // update item selection if in progress
            if (Category5.Items.ItemManager.Instance != null)
            {
                Category5.Items.ItemManager.Instance.HandlePlayerDisconnected(clientId);
            }

            // update game flow (game over check)
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.HandlePlayerDisconnected(clientId);
            }
            
            // Debug.Log($"NetworkSessionManager: Cleaned up state for player {clientId}");
        }
        
        // check if the game can continue with remaining players
        private void CheckGameViability()
        {
            if (!IsServer) return;
            
            int remainingPlayers = NetworkManager.Singleton.ConnectedClientsIds.Count;
            
            // Debug.Log($"NetworkSessionManager: {remainingPlayers} players remaining");
            
            if (remainingPlayers <= 0)
            {
                // no players left, end the session
                // Debug.Log("NetworkSessionManager: No players remaining, ending session");
                EndSession();
            }
            else if (remainingPlayers == 1 && NetworkManager.Singleton.IsHost)
            {
                // only host remains, could show "waiting for players" or continue solo
                // Debug.Log("NetworkSessionManager: Only host remaining");
                // for now, continue the game - host can play solo or wait
            }
        }
        
        // client-side: handle when we get disconnected
        private void HandleLocalDisconnect(string reason)
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;
            
            // Debug.Log($"NetworkSessionManager: Local disconnect - {reason}");
            
            // notify ui
            OnHostDisconnected?.Invoke(reason);
            
            // return to menu after a delay
            StartCoroutine(ReturnToMenuAfterDelay(reason));
        }
        
        private IEnumerator ReturnToMenuAfterDelay(string reason)
        {
            yield return new WaitForSecondsRealtime(returnToMenuDelay);
            
            ReturnToMenu();
        }
        
        // end the session and return everyone to menu
        public void EndSession()
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;
            
            if (IsServer)
            {
                // notify clients before shutting down
                NotifySessionEndingClientRpc("Host ended the session");
                
                // delay shutdown slightly so clients receive the rpc
                StartCoroutine(ShutdownAfterDelay());
            }
        }
        
        private IEnumerator ShutdownAfterDelay()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
            
            ReturnToMenu();
        }
        
        private void ReturnToMenu()
        {
            _isGameActive = false;
            
            // route through SceneTransitionManager for loading screen
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.LoadHomebase();
            }
            else
            {
                SceneManager.LoadScene("Homebase");
            }
        }

        // =====================================
        // rpcs
        // =====================================
        
        [ClientRpc]
        private void NotifyPlayerDisconnectedClientRpc(ulong clientId, string playerName)
        {
            // Debug.Log($"NetworkSessionManager: {playerName} disconnected");
            OnPlayerDisconnected?.Invoke(clientId, playerName);
        }
        
        [ClientRpc]
        private void NotifySessionEndingClientRpc(string reason)
        {
            if (IsServer) return; // server handles this differently
            
            // Debug.Log($"NetworkSessionManager: Session ending - {reason}");
            _isShuttingDown = true;
            OnHostDisconnected?.Invoke(reason);
            
            StartCoroutine(ReturnToMenuAfterDelay(reason));
        }
        
        // =====================================
        // public api
        // =====================================
        
        // check if a client is still connected
        public bool IsClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return false;
            return NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId);
        }
        
        // get count of connected players
        public int GetConnectedPlayerCount()
        {
            if (NetworkManager.Singleton == null) return 0;
            return NetworkManager.Singleton.ConnectedClientsIds.Count;
        }
    }
}
