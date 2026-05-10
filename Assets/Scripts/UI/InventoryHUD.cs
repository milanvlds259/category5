using UnityEngine;
using Category5.Items;
using Unity.Netcode;
using System.Collections.Generic;

namespace Category5.UI
{
    public class InventoryHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ItemSlotUI slotPrefab;
        [SerializeField] private Transform container;

        private PlayerInventory _localInventory;
        private List<ItemSlotUI> _instantiatedSlots = new List<ItemSlotUI>();

        private void Start()
        {
            if (container == null) container = transform;
            
            // Try to find the local player's inventory if it exists already (fallback)
            TryFindLocalInventory();
        }

        public void Initialize(PlayerInventory inventory)
        {
            if (_localInventory != null)
            {
                _localInventory.OnInventoryChanged -= RefreshUI;
            }

            _localInventory = inventory;
            
            if (_localInventory != null)
            {
                _localInventory.OnInventoryChanged += RefreshUI;
                RefreshUI();
            }
        }

        private void Update()
{
            if (_localInventory == null)
            {
                TryFindLocalInventory();
            }
        }

        private void TryFindLocalInventory()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;

            var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (localPlayer != null)
            {
                _localInventory = localPlayer.GetComponent<PlayerInventory>();
                if (_localInventory != null)
                {
                    _localInventory.OnInventoryChanged += RefreshUI;
                    RefreshUI();
                }
            }
        }

        private void OnDestroy()
        {
            if (_localInventory != null)
            {
                _localInventory.OnInventoryChanged -= RefreshUI;
            }
        }

        public void RefreshUI()
        {
            if (_localInventory == null || slotPrefab == null) return;

            // Get items with tiers. Note: PlayerInventory.GetAllItemsWithTier returns only non-empty slots.
            var items = _localInventory.GetAllItemsWithTier();
            
            // Hide all current slots
            foreach (var slot in _instantiatedSlots)
            {
                slot.gameObject.SetActive(false);
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (i >= _instantiatedSlots.Count)
                {
                    var newSlot = Instantiate(slotPrefab, container);
                    _instantiatedSlots.Add(newSlot);
                }

                _instantiatedSlots[i].gameObject.SetActive(true);
                _instantiatedSlots[i].SetItem(items[i].item, items[i].tier);
                _instantiatedSlots[i].SetInteractable(false); // HUD icons are purely visual
                
                // Scale down for sidebar display
                RectTransform slotRect = _instantiatedSlots[i].GetComponent<RectTransform>();
                if (slotRect != null)
                {
                    slotRect.sizeDelta = new Vector2(60, 60);
                }
                }
}
    }
}
