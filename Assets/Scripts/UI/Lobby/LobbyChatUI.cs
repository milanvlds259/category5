using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Category5.Core;
using Unity.Netcode;

namespace Category5.UI
{
    // ui component for lobby chat panel
    // displays chat messages and handles input
    public class LobbyChatUI : MonoBehaviour
    {
        [Header("message display")]
        [SerializeField] private ScrollRect messageScrollRect;
        [SerializeField] private RectTransform messageContainer;
        [SerializeField] private GameObject messagePrefab; // prefab with TextMeshProUGUI
        
        [Header("input")]
        [SerializeField] private TMP_InputField chatInputField;
        [SerializeField] private Button sendButton;
        
        [Header("visual settings")]
        [SerializeField] private Color localPlayerColor = new Color(0.4f, 0.8f, 1f); // light blue for "you"
        [SerializeField] private Color otherPlayerColor = Color.white;
        [SerializeField] private Color systemMessageColor = new Color(0.7f, 0.7f, 0.7f);
        
        [Header("layout")]
        [SerializeField] private int maxDisplayedMessages = 50;
        
        private List<GameObject> _messageObjects = new List<GameObject>();
        
        private void OnEnable()
        {
            // subscribe to chat events
            LobbyChatManager.OnChatMessageReceived += OnMessageReceived;
            LobbyChatManager.OnChatHistoryLoaded += OnHistoryReceived;
            
            // setup input
            if (chatInputField != null)
            {
                chatInputField.onSubmit.AddListener(OnInputSubmit);
            }
            
            if (sendButton != null)
            {
                sendButton.onClick.AddListener(OnSendButtonClicked);
            }
        }
        
        private void OnDisable()
        {
            // unsubscribe from chat events
            LobbyChatManager.OnChatMessageReceived -= OnMessageReceived;
            LobbyChatManager.OnChatHistoryLoaded -= OnHistoryReceived;
            
            // cleanup input
            if (chatInputField != null)
            {
                chatInputField.onSubmit.RemoveListener(OnInputSubmit);
            }
            
            if (sendButton != null)
            {
                sendButton.onClick.RemoveListener(OnSendButtonClicked);
            }
        }
        
        // call this when entering the lobby to initialize ui
        public void Initialize()
        {
            ClearMessages();
            
            // load any existing messages from buffer
            if (LobbyChatManager.Instance != null)
            {
                var existingMessages = LobbyChatManager.Instance.GetMessageBuffer();
                foreach (var msg in existingMessages)
                {
                    DisplayMessage(msg);
                }
            }
            
            // clear input field
            if (chatInputField != null)
            {
                chatInputField.text = "";
            }
        }
        
        private void OnInputSubmit(string text)
        {
            SendMessage();
            
            // keep focus on input field for quick chatting
            if (chatInputField != null)
            {
                chatInputField.ActivateInputField();
                chatInputField.Select();
            }
        }
        
        private void OnSendButtonClicked()
        {
            SendMessage();
            
            // keep focus on input field
            if (chatInputField != null)
            {
                chatInputField.ActivateInputField();
                chatInputField.Select();
            }
        }
        
        private void SendMessage()
        {
            if (chatInputField == null) return;
            
            string message = chatInputField.text.Trim();
            if (string.IsNullOrEmpty(message)) return;
            
            // send via chat manager
            if (LobbyChatManager.Instance != null)
            {
                LobbyChatManager.Instance.SendChatMessage(message);
            }
            
            // clear input field
            chatInputField.text = "";
        }
        
        private void OnMessageReceived(ChatMessage msg)
        {
            DisplayMessage(msg);
        }
        
        private void OnHistoryReceived(List<ChatMessage> history)
        {
            ClearMessages();
            
            foreach (var msg in history)
            {
                DisplayMessage(msg);
            }
        }
        
        private void DisplayMessage(ChatMessage msg)
        {
            if (messageContainer == null || messagePrefab == null) return;
            
            // create message object
            var msgObj = Instantiate(messagePrefab, messageContainer);
            var textComponent = msgObj.GetComponent<TextMeshProUGUI>();
            
            if (textComponent != null)
            {
                // determine if this is from local player
                bool isLocalPlayer = NetworkManager.Singleton != null && 
                                     msg.SenderClientId == NetworkManager.Singleton.LocalClientId;
                
                string senderName = msg.SenderName.ToString();
                string messageText = msg.Message.ToString();
                
                // format message
                if (isLocalPlayer)
                {
                    textComponent.text = $"<b>You:</b> {messageText}";
                    textComponent.color = localPlayerColor;
                }
                else
                {
                    textComponent.text = $"<b>{senderName}:</b> {messageText}";
                    textComponent.color = otherPlayerColor;
                }
            }
            
            _messageObjects.Add(msgObj);
            
            // remove oldest messages if over limit
            while (_messageObjects.Count > maxDisplayedMessages)
            {
                var oldest = _messageObjects[0];
                _messageObjects.RemoveAt(0);
                Destroy(oldest);
            }
            
            // scroll to bottom
            ScrollToBottom();
        }
        
        // display a system message (not from a player)
        public void DisplaySystemMessage(string message)
        {
            if (messageContainer == null || messagePrefab == null) return;
            
            var msgObj = Instantiate(messagePrefab, messageContainer);
            var textComponent = msgObj.GetComponent<TextMeshProUGUI>();
            
            if (textComponent != null)
            {
                textComponent.text = $"<i>{message}</i>";
                textComponent.color = systemMessageColor;
            }
            
            _messageObjects.Add(msgObj);
            
            while (_messageObjects.Count > maxDisplayedMessages)
            {
                var oldest = _messageObjects[0];
                _messageObjects.RemoveAt(0);
                Destroy(oldest);
            }
            
            ScrollToBottom();
        }
        
        private void ScrollToBottom()
        {
            // wait for layout to update before scrolling
            Canvas.ForceUpdateCanvases();
            
            if (messageScrollRect != null)
            {
                messageScrollRect.verticalNormalizedPosition = 0f;
            }
        }
        
        private void ClearMessages()
        {
            foreach (var obj in _messageObjects)
            {
                if (obj != null)
                    Destroy(obj);
            }
            _messageObjects.Clear();
        }
        
        // public method to focus the input field (called when switching to chat tab)
        public void FocusInputField()
        {
            if (chatInputField != null)
            {
                chatInputField.ActivateInputField();
                chatInputField.Select();
            }
        }
    }
}
