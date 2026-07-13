using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Category5.Player;
using Category5.Core;
using Category5.Items;
using Unity.Netcode;
using System.Collections.Generic;

namespace Category5.DebugTools
{
    public class DebugMenuUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Transform characterListContent;
        [SerializeField] private Transform itemListContent;
        [SerializeField] private GameObject buttonPrefab;
        
        public static bool IsMenuOpen { get; private set; } = false;

        private InputAction toggleAction;

        private void Start()
        {
            // Set up input
            toggleAction = InputSystem.actions.FindAction("Debug/ToggleMenu");
            
            if (menuPanel != null)
                menuPanel.SetActive(false);

            PopulateCharacters();
            PopulateItems();
            SetupActionButtons();
        }

        private void SetupActionButtons()
        {
            if (menuPanel == null) return;

            var buttons = menuPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn.name.Contains("HEAL")) btn.onClick.AddListener(HealPlayer);
                if (btn.name.Contains("MANA")) btn.onClick.AddListener(RestoreMana);
                if (btn.name.Contains("RESPAWN")) btn.onClick.AddListener(ResetPlayer);
                if (btn.name.Contains("CLEAR")) btn.onClick.AddListener(ClearInventory);
            }
        }

        private void Update()
{
            if (toggleAction != null && toggleAction.WasPressedThisFrame())
            {
                ToggleMenu();
            }
        }

        public void ToggleMenu()
        {
            IsMenuOpen = !IsMenuOpen;
            if (menuPanel != null)
            {
                menuPanel.SetActive(IsMenuOpen);
                if (IsMenuOpen)
                {
                    PopulateCharacters();
                    PopulateItems();
                }
            }

            // Toggle cursor lock
            if (IsMenuOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void PopulateCharacters()
        {
            if (characterListContent == null || ClassRegistry.Instance == null) return;

            foreach (Transform child in characterListContent) Destroy(child.gameObject);

            var classes = ClassRegistry.Instance.GetAllClasses();
            foreach (var playerClass in classes)
            {
                if (playerClass == null) continue;

                GameObject btnObj = Instantiate(buttonPrefab, characterListContent);
                var text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = playerClass.className;

                var btn = btnObj.GetComponent<Button>();
                int classId = playerClass.classId;
                btn.onClick.AddListener(() => SwitchClass(classId));
            }
        }

        private void PopulateItems()
        {
            if (itemListContent == null || ItemRegistry.Instance == null) return;

            foreach (Transform child in itemListContent) Destroy(child.gameObject);

            var items = ItemRegistry.Instance.AllItems;
            foreach (var item in items)
            {
                if (item == null) continue;

                GameObject btnObj = Instantiate(buttonPrefab, itemListContent);
                var text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = item.ItemName;

                var btn = btnObj.GetComponent<Button>();
                string itemId = item.UniqueId;
                btn.onClick.AddListener(() => AddItem(itemId));
            }
        }

        private void SwitchClass(int classId)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                var classManager = localPlayer.GetComponent<PlayerClassManager>();
                if (classManager != null)
                {
                    classManager.RequestSetClassIdServerRpc(classId);
                    Debug.Log($"[DebugMenu] Requested class switch to ID: {classId}");
                }
            }
        }

        private void AddItem(string itemId)
        {
            // Adding item is server-only. Since debug is usually host, this works.
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                var inventory = localPlayer.GetComponent<PlayerInventory>();
                if (inventory != null)
                {
                    // AddItem is server-side.
                    if (NetworkManager.Singleton.IsServer)
                    {
                        inventory.AddItem(itemId);
                        Debug.Log($"[DebugMenu] Added item: {itemId}");
                    }
                    else
                    {
                        Debug.LogWarning("[DebugMenu] Cannot add item from client. Must be Host/Server.");
                    }
                }
            }
        }

        public void HealPlayer()
        {
            if (!NetworkManager.Singleton.IsServer) return;
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                var controller = localPlayer.GetComponent<PlayerController>();
                if (controller != null)
                {
                    controller.Heal(9999);
                    Debug.Log("[DebugMenu] Healed player to full.");
                }
            }
        }

        public void RestoreMana()
        {
            if (!NetworkManager.Singleton.IsServer) return;
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                var controller = localPlayer.GetComponent<PlayerController>();
                if (controller != null)
                {
                    controller.RestoreMana(9999);
                    Debug.Log("[DebugMenu] Restored player mana to full.");
                }
            }
        }

        public void ResetPlayer()
{
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                var controller = localPlayer.GetComponent<PlayerController>();
                if (controller != null)
                {
                    controller.Respawn();
                }
            }
        }

        public void ClearInventory()
        {
            if (!NetworkManager.Singleton.IsServer) return;

            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                var inventory = localPlayer.GetComponent<PlayerInventory>();
                if (inventory != null)
                {
                    inventory.ClearInventory();
                }
            }
        }
    }
}
