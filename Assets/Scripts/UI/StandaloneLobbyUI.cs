using UnityEngine;
using UnityEngine.InputSystem;
using Category5.Core;

namespace Category5.UI
{
    public class StandaloneLobbyUI : MonoBehaviour
    {
        public static StandaloneLobbyUI Instance { get; private set; }

        [SerializeField] private NetworkMenu networkMenu;
        [SerializeField] private CanvasGroup rootCanvasGroup;

        private bool _isOpen = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            Close();
        }

        private void Update()
        {
            if (_isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        public void OpenHostJoin()
        {
            if (_isOpen) return;
            _isOpen = true;
            HubUI.OnMenuOpened();

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 1f;
                rootCanvasGroup.interactable = true;
                rootCanvasGroup.blocksRaycasts = true;
            }

            if (networkMenu != null)
            {
                networkMenu.OpenHostJoinScreen();
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void OpenParty()
        {
            if (_isOpen) return;
            _isOpen = true;
            HubUI.OnMenuOpened();

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 1f;
                rootCanvasGroup.interactable = true;
                rootCanvasGroup.blocksRaycasts = true;
            }

            if (networkMenu != null)
            {
                networkMenu.OpenPartyScreen();
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Open()
        {
            // Default to Host/Join if using generic Open
            OpenHostJoin();
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            HubUI.OnMenuClosed();

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 0f;
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }

            if (networkMenu != null)
            {
                networkMenu.CloseAllUI();
            }

            if (!HubUI.IsAnyMenuOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
}
}
