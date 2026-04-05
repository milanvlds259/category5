using UnityEngine;
using UnityEngine.InputSystem;
using Category5.Core;

namespace Category5.UI
{
    // manages the toggle between the chat indicator (icon + enter hint) and the full chat panel
    // pressing Enter opens chat, pressing Enter on an empty field closes it
    public class ChatToggleController : MonoBehaviour
    {
        [Header("panels")]
        [SerializeField] private GameObject chatIndicatorPanel; // icon + enter key hint, shown when chat is closed
        [SerializeField] private LobbyChatUI lobbyChatUI;       // the full chat panel
        [SerializeField] private GameObject notificationBubble; // shown when a message arrives while chat is closed

        // owns a private input actions instance so no inspector wiring is needed
        private InputSystem_Actions _inputActions;
        private bool _isOpen = false;
        // prevents the same enter press that opens chat from immediately closing it via TMP's onSubmit
        private bool _justOpened = false;

        private void OnEnable()
        {
            LobbyChatUI.OnCloseRequested += CloseChat;
            LobbyChatManager.OnChatMessageReceived += OnMessageReceived;
        }

        private void OnDisable()
        {
            LobbyChatUI.OnCloseRequested -= CloseChat;
            LobbyChatManager.OnChatMessageReceived -= OnMessageReceived;
        }

        // call this when entering the lobby
        public void Initialize()
        {
            Debug.Log("ChatToggleController.Initialize called");

            // initialize the chat backend first so message handlers are registered
            // before the ui tries to subscribe or request history
            if (LobbyChatManager.Instance != null)
                LobbyChatManager.Instance.Initialize();
            else
                Debug.LogError("ChatToggleController: LobbyChatManager instance not found in scene");

            // create input actions instance and hook up lobby/chat
            if (_inputActions == null)
            {
                _inputActions = new InputSystem_Actions();
                _inputActions.Lobby.Chat.performed += OnChatActionPerformed;
                Debug.Log("ChatToggleController: input actions created and Chat callback registered");
            }

            // make sure chat ui is ready
            if (lobbyChatUI != null)
                lobbyChatUI.Initialize();
            else
                Debug.LogError("ChatToggleController: lobbyChatUI is null - assign it in the inspector");

            if (chatIndicatorPanel == null)
                Debug.LogError("ChatToggleController: chatIndicatorPanel is null - assign it in the inspector");

            // start in indicator state with chat action ready
            SetChatOpen(false);
            SetNotificationBubble(false);
            _inputActions.Lobby.Enable();
            Debug.Log($"ChatToggleController: Lobby action map enabled, Chat action enabled: {_inputActions.Lobby.Chat.enabled}");
        }

        // call this when leaving the lobby
        public void Cleanup()
        {
            if (_inputActions != null)
            {
                _inputActions.Lobby.Disable();
            }

            SetChatOpen(false);
            SetNotificationBubble(false);

            // cleanup the chat backend and unregister message handlers
            if (LobbyChatManager.Instance != null)
                LobbyChatManager.Instance.Cleanup();
        }

        private void OnDestroy()
        {
            if (_inputActions != null)
            {
                _inputActions.Lobby.Chat.performed -= OnChatActionPerformed;
                _inputActions.Dispose();
                _inputActions = null;
            }
        }

        private void OnChatActionPerformed(InputAction.CallbackContext ctx)
        {
            Debug.Log($"ChatToggleController: Chat action fired, _isOpen={_isOpen}");
            // only open from indicator state - while open, Enter is handled by the TMP_InputField
            if (!_isOpen)
                OpenChat();
        }

        private void OpenChat()
        {
            // flag to swallow the TMP onSubmit that fires on this same enter keypress
            _justOpened = true;

            // clear the notification - player is reading the chat now
            SetNotificationBubble(false);

            // disable the action while chat is open so Enter doesn't double-fire
            _inputActions.Lobby.Disable();

            SetChatOpen(true);

            // re-initialize ui so any messages received while the panel was closed are shown
            if (lobbyChatUI != null)
                lobbyChatUI.Initialize();

            // focus input field so player can start typing immediately
            if (lobbyChatUI != null)
                lobbyChatUI.FocusInputField();
        }

        private void CloseChat()
        {
            // swallow the first close request - it came from the same enter that opened us
            if (_justOpened)
            {
                _justOpened = false;
                return;
            }

            SetChatOpen(false);

            // re-enable so Enter can open chat again
            if (_inputActions != null)
                _inputActions.Lobby.Enable();
        }

        private void SetChatOpen(bool open)
        {
            _isOpen = open;

            if (chatIndicatorPanel != null)
                chatIndicatorPanel.SetActive(!open);

            if (lobbyChatUI != null)
                lobbyChatUI.gameObject.SetActive(open);
        }

        private void SetNotificationBubble(bool visible)
        {
            if (notificationBubble != null)
                notificationBubble.SetActive(visible);
        }

        private void OnMessageReceived(ChatMessage msg)
        {
            // only show bubble if the chat panel is currently closed
            if (!_isOpen)
                SetNotificationBubble(true);
        }
    }
}
