using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System;
using System.Collections.Generic;

namespace Category5.Core
{
    // chat message data structure for network serialization
    public struct ChatMessage : INetworkSerializable
    {
        public ulong SenderClientId;
        public FixedString64Bytes SenderName;
        public FixedString512Bytes Message;
        public float Timestamp;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SenderClientId);
            serializer.SerializeValue(ref SenderName);
            serializer.SerializeValue(ref Message);
            serializer.SerializeValue(ref Timestamp);
        }
    }
    
    // manages networked chat in the lobby using custom messaging
    // follows the same pattern as LobbyManager for consistency
    public class LobbyChatManager : MonoBehaviour
    {
        public static LobbyChatManager Instance { get; private set; }
        
        // chat message buffer (server keeps last N messages for late joiners)
        private const int MAX_BUFFERED_MESSAGES = 20;
        private List<ChatMessage> _messageBuffer = new List<ChatMessage>();
        
        // custom message names
        private const string MSG_CHAT_SEND = "LobbyChatSend";
        private const string MSG_CHAT_BROADCAST = "LobbyChatBroadcast";
        private const string MSG_CHAT_HISTORY = "LobbyChatHistory";
        private const string MSG_CHAT_REQUEST_HISTORY = "LobbyChatRequestHistory";
        
        // events for ui updates
        public static event Action<ChatMessage> OnChatMessageReceived;
        public static event Action<List<ChatMessage>> OnChatHistoryLoaded;
        
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
        
        // call this when entering the lobby
        public void Initialize()
        {
            if (_isInitialized) return;
            if (NetworkManager.Singleton == null) return;
            
            // CustomMessagingManager is only available when the network manager is listening
            if (NetworkManager.Singleton.CustomMessagingManager == null)
            {
                // Debug.LogWarning("LobbyChatManager: Cannot initialize handlers because CustomMessagingManager is null (NetworkManager not listening).");
                return;
            }
            
            _messageBuffer.Clear();
            
            // register message handlers
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MSG_CHAT_SEND, OnChatSendReceived);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MSG_CHAT_BROADCAST, OnChatBroadcastReceived);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MSG_CHAT_HISTORY, OnChatHistoryReceived);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MSG_CHAT_REQUEST_HISTORY, OnChatHistoryRequested);
            
            _isInitialized = true;
            
            // if we're a client (not host), request chat history (so clients that join late can get recent messages)
            if (!NetworkManager.Singleton.IsServer)
            {
                RequestChatHistory();
            }
            
            // Debug.Log("LobbyChatManager: Initialized");
        }

        // call this when leaving the lobby
        public void Cleanup()
        {
            if (!_isInitialized) return;
            
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
            {
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(MSG_CHAT_SEND);
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(MSG_CHAT_BROADCAST);
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(MSG_CHAT_HISTORY);
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(MSG_CHAT_REQUEST_HISTORY);
            }
            
            _messageBuffer.Clear();
            _isInitialized = false;
            
            // Debug.Log("LobbyChatManager: Cleaned up");
        }
        
        // send a chat message (called by local player)
        public void SendChatMessage(string message)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;
            if (string.IsNullOrWhiteSpace(message)) return;
            
            // truncate message if too long
            // i dont think anyone will type this much but just in case cuz whatever
            if (message.Length > 500)
                message = message.Substring(0, 500);
            
            // get sender name
            string senderName = PlayerNameManager.Instance?.GetDisplayName() ?? "Player";
            
            var chatMsg = new ChatMessage
            {
                SenderClientId = NetworkManager.Singleton.LocalClientId,
                SenderName = new FixedString64Bytes(senderName),
                Message = new FixedString512Bytes(message),
                Timestamp = Time.time
            };
            
            if (NetworkManager.Singleton.IsServer)
            {
                // host sends directly and broadcasts
                AddMessageToBuffer(chatMsg);
                BroadcastChatMessage(chatMsg);
                OnChatMessageReceived?.Invoke(chatMsg);
            }
            else
            {
                // client sends to server
                using var writer = new FastBufferWriter(1024, Allocator.Temp);
                writer.WriteValueSafe(chatMsg);
                
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                    MSG_CHAT_SEND,
                    NetworkManager.ServerClientId,
                    writer
                );
            }
        }
        
        // server receives chat message from client
        private void OnChatSendReceived(ulong senderClientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            
            reader.ReadValueSafe(out ChatMessage chatMsg);
            
            // ensure sender id matches for security
            chatMsg.SenderClientId = senderClientId;
            
            // update sender name from lobby manager for verification
            if (LobbyManager.Instance != null)
            {
                string verifiedName = LobbyManager.Instance.GetPlayerName(senderClientId);
                chatMsg.SenderName = new FixedString64Bytes(verifiedName);
            }
            
            // update timestamp to server time
            chatMsg.Timestamp = Time.time;
            
            // add to buffer and broadcast
            AddMessageToBuffer(chatMsg);
            BroadcastChatMessage(chatMsg);
            
            // also notify local ui on server/host
            OnChatMessageReceived?.Invoke(chatMsg);
        }
        
        // server broadcasts chat message to all clients
        private void BroadcastChatMessage(ChatMessage chatMsg)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            writer.WriteValueSafe(chatMsg);
            
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (clientId != NetworkManager.ServerClientId)
                {
                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                        MSG_CHAT_BROADCAST,
                        clientId,
                        writer
                    );
                }
            }
        }
        
        // client receives broadcasted chat message
        private void OnChatBroadcastReceived(ulong senderClientId, FastBufferReader reader)
        {
            if (NetworkManager.Singleton.IsServer) return;
            
            reader.ReadValueSafe(out ChatMessage chatMsg);
            // buffer it so the ui can read missed messages when the panel is opened later
            AddMessageToBuffer(chatMsg);
            OnChatMessageReceived?.Invoke(chatMsg);
        }
        
        // add message to buffer, removing oldest if at capacity
        private void AddMessageToBuffer(ChatMessage msg)
        {
            _messageBuffer.Add(msg);
            
            while (_messageBuffer.Count > MAX_BUFFERED_MESSAGES)
            {
                _messageBuffer.RemoveAt(0);
            }
        }
        
        // client requests chat history from server
        private void RequestChatHistory()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;
            if (NetworkManager.Singleton.IsServer) return;
            
            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe((byte)1); // dummy data
            
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                MSG_CHAT_REQUEST_HISTORY,
                NetworkManager.ServerClientId,
                writer
            );
        }
        
        // server receives history request from client
        private void OnChatHistoryRequested(ulong senderClientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            
            // send chat history to requesting client
            SendChatHistoryToClient(senderClientId);
        }
        
        // server sends chat history to specific client
        private void SendChatHistoryToClient(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            
            using var writer = new FastBufferWriter(1024 * 20, Allocator.Temp); // large buffer for history
            
            writer.WriteValueSafe(_messageBuffer.Count);
            
            foreach (var msg in _messageBuffer)
            {
                writer.WriteValueSafe(msg);
            }
            
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                MSG_CHAT_HISTORY,
                clientId,
                writer
            );
        }
        
        // client receives chat history from server
        private void OnChatHistoryReceived(ulong senderClientId, FastBufferReader reader)
        {
            if (NetworkManager.Singleton.IsServer) return;
            
            reader.ReadValueSafe(out int count);
            
            var history = new List<ChatMessage>();
            
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ChatMessage msg);
                history.Add(msg);
            }
            
            // store history locally so GetMessageBuffer() works when the panel opens later
            _messageBuffer.Clear();
            _messageBuffer.AddRange(history);
            
            // Debug.Log($"LobbyChatManager: Received {count} messages of chat history");
            OnChatHistoryLoaded?.Invoke(history);
        }
        
        // get current message buffer (for ui initialization)
        public List<ChatMessage> GetMessageBuffer()
        {
            return new List<ChatMessage>(_messageBuffer);
        }
    }
}
