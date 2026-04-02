using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Category5.Core;
using Category5.Player;

namespace Category5.UI
{
    // displays the party panel in lobby showing each connected player's
    // selected class portrait and username
    public class LobbyPartyPanel : MonoBehaviour
    {
        [Header("references")]
        [SerializeField] private Transform playerSlotContainer; // horizontal layout group
        [SerializeField] private LobbyPlayerEntry playerSlotPrefab;
        
        [Header("header")]
        [SerializeField] private TextMeshProUGUI partyHeaderText;
        
        [Header("fallback sprites")]
        [SerializeField] private Sprite defaultPortraitSprite; // shown when class has no portrait
        
        private List<LobbyPlayerEntry> _slots = new List<LobbyPlayerEntry>();
        
        private void OnEnable()
        {
            LobbyManager.OnLobbyPlayersChanged += RefreshParty;
        }
        
        private void OnDisable()
        {
            LobbyManager.OnLobbyPlayersChanged -= RefreshParty;
        }
        
        // call this when entering the lobby
        public void Initialize()
        {
            RefreshParty();
        }
        
        // update the header to include the join code (host only, clients pass empty string)
        public void SetJoinCode(string code)
        {
            if (partyHeaderText == null) return;
            partyHeaderText.text = string.IsNullOrEmpty(code) ? "PARTY" : $"PARTY  ·  {code}";
        }
        
        // rebuild all player slots from lobby data
        public void RefreshParty()
        {
            ClearSlots();
            
            if (LobbyManager.Instance == null) return;
            
            var players = LobbyManager.Instance.GetLobbyPlayers();
            
            foreach (var player in players)
            {
                var slotGO = Instantiate(playerSlotPrefab.gameObject, playerSlotContainer);
                var rectTransform = slotGO.GetComponent<RectTransform>();
                if (rectTransform != null)
                    rectTransform.localScale = Vector3.one;
                
                var entry = slotGO.GetComponent<LobbyPlayerEntry>();
                if (entry != null)
                {
                    // look up the portrait sprite for this player's selected class
                    Sprite portrait = GetClassPortrait(player.SelectedClassId);
                    string playerName = player.PlayerName.ToString();
                    
                    entry.Setup(playerName, player.IsReady, portrait);
                }
                
                _slots.Add(entry);
            }
        }
        
        // get portrait sprite for a class id from the registry
        private Sprite GetClassPortrait(int classId)
        {
            if (classId == PlayerClass.NoClassId) return defaultPortraitSprite;
            if (ClassRegistry.Instance == null) return defaultPortraitSprite;
            
            var playerClass = ClassRegistry.Instance.GetClass(classId);
            if (playerClass == null) return defaultPortraitSprite;

            // prefer party portrait, fall back to portrait, fall back to icon, then default
            if (playerClass.classPartyPortrait != null) return playerClass.classPartyPortrait;
            if (playerClass.classPortrait != null) return playerClass.classPortrait;
            if (playerClass.classIcon != null) return playerClass.classIcon;
            return defaultPortraitSprite;
        }
        
        private void ClearSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot != null)
                    Destroy(slot.gameObject);
            }
            _slots.Clear();
        }
    }
}
