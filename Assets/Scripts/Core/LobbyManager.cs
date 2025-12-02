using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System;
using System.Collections.Generic;

namespace Category5.Core
{
    // data structure for a player in the lobby
    public struct LobbyPlayerData : INetworkSerializable, IEquatable<LobbyPlayerData>
    {
        public ulong ClientId;
        public FixedString64Bytes PlayerName;
        public bool IsHost;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref IsHost);
        }
        
        public bool Equals(LobbyPlayerData other)
        {
            return ClientId == other.ClientId;
        }
        
        public override bool Equals(object obj)
        {
            return obj is LobbyPlayerData other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            return ClientId.GetHashCode();
        }
    }
    
    // manages lobby state and player names before the game starts
    // this is a regular MonoBehaviour that uses custom messaging for sync
    // add this as its own GameObject in the menu scene
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }
        
        // local list of all players in the lobby
        private List<LobbyPlayerData> _lobbyPlayers = new List<LobbyPlayerData>();
        
        // events for ui updates
        public static event Action OnLobbyPlayersChanged;
        
        // custom message names
        private const string MSG_PLAYER_NAME = "LobbyPlayerName";
        private const string MSG_PLAYER_LIST = "LobbyPlayerList";
        private const string MSG_PLAYER_LEFT = "LobbyPlayerLeft";
        
        private bool _isInitialized = false;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        private void OnDestroy()
        {
            Cleanup();
            
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        // call this when hosting or joining to start listening for messages
        public void Initialize()
        {
            if (_isInitialized) return;
            if (NetworkManager.Singleton == null) return;
            
            _lobbyPlayers.Clear();
            
            // register custom message handlers
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MSG_PLAYER_NAME, OnPlayerNameReceived);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MSG_PLAYER_LIST, OnPlayerListReceived);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MSG_PLAYER_LEFT, OnPlayerLeftReceived);
            
            // subscribe to connection events
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            _isInitialized = true;
            
            if (NetworkManager.Singleton.IsServer)
            {
                // host adds themselves immediately
                AddPlayer(NetworkManager.Singleton.LocalClientId, PlayerNameManager.Instance?.GetDisplayName() ?? "Host", true);
            }
            
            Debug.Log("LobbyManager: Initialized");
        }
        
        // call this when leaving the lobby
        public void Cleanup()
        {
            if (!_isInitialized) return;
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.CustomMessagingManager?.UnregisterNamedMessageHandler(MSG_PLAYER_NAME);
                NetworkManager.Singleton.CustomMessagingManager?.UnregisterNamedMessageHandler(MSG_PLAYER_LIST);
                NetworkManager.Singleton.CustomMessagingManager?.UnregisterNamedMessageHandler(MSG_PLAYER_LEFT);
                
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            
            _lobbyPlayers.Clear();
            _isInitialized = false;
            
            Debug.Log("LobbyManager: Cleaned up");
        }
        
        private void OnClientConnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            
            // when a new client connects, they will send us their name
            // host already added in Initialize()
            if (clientId == NetworkManager.Singleton.LocalClientId) return;
            
            Debug.Log($"LobbyManager: Client {clientId} connected, waiting for name...");
        }
        
        private void OnClientDisconnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            
            // remove the player
            for (int i = _lobbyPlayers.Count - 1; i >= 0; i--)
            {
                if (_lobbyPlayers[i].ClientId == clientId)
                {
                    Debug.Log($"LobbyManager: Player {_lobbyPlayers[i].PlayerName} left");
                    _lobbyPlayers.RemoveAt(i);
                    break;
                }
            }
            
            // notify all clients about the removal
            BroadcastPlayerLeft(clientId);
            OnLobbyPlayersChanged?.Invoke();
        }
        
        // client sends their name to server
        public void SendLocalPlayerName()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;
            if (NetworkManager.Singleton.IsServer) return; // host already added
            
            string name = PlayerNameManager.Instance?.GetDisplayName() ?? "Player";
            
            using var writer = new FastBufferWriter(64, Allocator.Temp);
            writer.WriteValueSafe(new FixedString64Bytes(name));
            
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                MSG_PLAYER_NAME, 
                NetworkManager.ServerClientId, 
                writer
            );
            
            Debug.Log($"LobbyManager: Sent name '{name}' to server");
        }
        
        // server receives player name from client
        private void OnPlayerNameReceived(ulong senderClientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            
            reader.ReadValueSafe(out FixedString64Bytes playerName);
            
            AddPlayer(senderClientId, playerName.ToString(), false);
            
            // send full player list to all clients
            BroadcastPlayerList();
        }
        
        // server broadcasts full player list to all clients
        private void BroadcastPlayerList()
        {
            if (!NetworkManager.Singleton.IsServer) return;
            
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            
            // write count
            writer.WriteValueSafe(_lobbyPlayers.Count);
            
            // write each player
            foreach (var player in _lobbyPlayers)
            {
                writer.WriteValueSafe(player);
            }
            
            // send to all clients (not server)
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (clientId != NetworkManager.ServerClientId)
                {
                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                        MSG_PLAYER_LIST,
                        clientId,
                        writer
                    );
                }
            }
            
            OnLobbyPlayersChanged?.Invoke();
        }
        
        // client receives full player list from server
        private void OnPlayerListReceived(ulong senderClientId, FastBufferReader reader)
        {
            if (NetworkManager.Singleton.IsServer) return;
            
            reader.ReadValueSafe(out int count);
            
            _lobbyPlayers.Clear();
            
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out LobbyPlayerData player);
                _lobbyPlayers.Add(player);
            }
            
            Debug.Log($"LobbyManager: Received player list with {count} players");
            OnLobbyPlayersChanged?.Invoke();
        }
        
        // server broadcasts when a player leaves
        private void BroadcastPlayerLeft(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            
            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(clientId);
            
            foreach (var cid in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (cid != NetworkManager.ServerClientId)
                {
                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                        MSG_PLAYER_LEFT,
                        cid,
                        writer
                    );
                }
            }
        }
        
        // client receives notification that a player left
        private void OnPlayerLeftReceived(ulong senderClientId, FastBufferReader reader)
        {
            if (NetworkManager.Singleton.IsServer) return;
            
            reader.ReadValueSafe(out ulong leftClientId);
            
            for (int i = _lobbyPlayers.Count - 1; i >= 0; i--)
            {
                if (_lobbyPlayers[i].ClientId == leftClientId)
                {
                    Debug.Log($"LobbyManager: Player {_lobbyPlayers[i].PlayerName} left");
                    _lobbyPlayers.RemoveAt(i);
                    break;
                }
            }
            
            OnLobbyPlayersChanged?.Invoke();
        }
        
        private void AddPlayer(ulong clientId, string playerName, bool isHost)
        {
            // check if already exists
            foreach (var p in _lobbyPlayers)
            {
                if (p.ClientId == clientId) return;
            }
            
            var player = new LobbyPlayerData
            {
                ClientId = clientId,
                PlayerName = new FixedString64Bytes(playerName),
                IsHost = isHost
            };
            
            _lobbyPlayers.Add(player);
            Debug.Log($"LobbyManager: Added player '{playerName}' (client {clientId}, host: {isHost})");
            
            OnLobbyPlayersChanged?.Invoke();
        }
        
        // get the current lobby players (for UI)
        public LobbyPlayerData[] GetLobbyPlayers()
        {
            return _lobbyPlayers.ToArray();
        }
        
        // get player count
        public int GetPlayerCount()
        {
            return _lobbyPlayers.Count;
        }
        
        // get a player's name by client id
        public string GetPlayerName(ulong clientId)
        {
            foreach (var p in _lobbyPlayers)
            {
                if (p.ClientId == clientId)
                {
                    return p.PlayerName.ToString();
                }
            }
            return $"Player {clientId}";
        }
    }
}
