using UnityEngine;
using UnityEngine.InputSystem;
using Category5.Core;

namespace Category5.UI
{
    public class StandaloneCharacterSelectUI : MonoBehaviour
    {
        public static StandaloneCharacterSelectUI Instance { get; private set; }

        [SerializeField] private CharacterSelectPanel characterSelectPanel;
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

        public void Open()
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

            if (characterSelectPanel != null)
            {
                characterSelectPanel.Initialize();
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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

            if (!HubUI.IsAnyMenuOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
}
}
