using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Category5.Core;
using Category5.Player;
using System.Collections.Generic;

namespace Category5.UI
{
    // manages character selection as a scrollable vertical list of class cards
    // click a card to select that class, hover to show character view panel
    public class CharacterSelectPanel : MonoBehaviour
    {
        [Header("references")]
        [SerializeField] private CharacterViewPanel characterViewPanel;
        
        [Header("card list")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform cardContainer; // vertical layout group parent
        [SerializeField] private LobbyClassCard cardPrefab;
        [SerializeField] private Sprite defaultClassSprite; // fallback if no portrait
        
        [Header("header")]
        [SerializeField] private TextMeshProUGUI headerText;
        
        private PlayerClass[] _availableClasses;
        private List<LobbyClassCard> _cards = new List<LobbyClassCard>();
        private int _selectedIndex = 0;
        
        private void OnEnable()
        {
            LobbyClassCard.OnCardClicked += OnCardClicked;
            LobbyClassCard.OnCardHoverEnter += OnCardHoverEnter;
            LobbyClassCard.OnCardHoverExit += OnCardHoverExit;
        }
        
        private void OnDisable()
        {
            LobbyClassCard.OnCardClicked -= OnCardClicked;
            LobbyClassCard.OnCardHoverEnter -= OnCardHoverEnter;
            LobbyClassCard.OnCardHoverExit -= OnCardHoverExit;
        }
        
        // call this when entering the lobby
        public void Initialize()
        {
            if (ClassRegistry.Instance == null)
            {
                Debug.LogError("CharacterSelectPanel: ClassRegistry not found!");
                return;
            }
            
            _availableClasses = ClassRegistry.Instance.GetAllClasses();
            
            if (_availableClasses == null || _availableClasses.Length == 0)
            {
                Debug.LogError("CharacterSelectPanel: No classes found in ClassRegistry!");
                return;
            }
            
            // clear old cards
            ClearCards();
            
            // find saved selection
            var savedClass = ClassSelectionManager.GetClass();
            _selectedIndex = 0;
            
            for (int i = 0; i < _availableClasses.Length; i++)
            {
                if (_availableClasses[i].classType == savedClass)
                {
                    _selectedIndex = i;
                    break;
                }
            }
            
            // spawn a card for each class
            for (int i = 0; i < _availableClasses.Length; i++)
            {
                var cardGO = Instantiate(cardPrefab.gameObject, cardContainer);
                var card = cardGO.GetComponent<LobbyClassCard>();
                card.Setup(_availableClasses[i], defaultClassSprite);
                card.SetSelected(i == _selectedIndex);
                _cards.Add(card);
            }
            
            // make sure character view panel starts hidden
            if (characterViewPanel != null)
                characterViewPanel.gameObject.SetActive(false);
        }
        
        private void OnCardClicked(LobbyClassCard clickedCard)
        {
            // find which index was clicked
            int index = _cards.IndexOf(clickedCard);
            if (index < 0 || index >= _availableClasses.Length) return;
            
            // skip if already selected
            if (index == _selectedIndex) return;
            
            _selectedIndex = index;
            var selectedClass = _availableClasses[_selectedIndex];
            
            // update card visuals
            for (int i = 0; i < _cards.Count; i++)
                _cards[i].SetSelected(i == _selectedIndex);
            
            // save selection locally
            ClassSelectionManager.SetClass(selectedClass.classType);
            
            // send to server via lobby manager
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    if (LobbyManager.Instance != null)
                        LobbyManager.Instance.SetHostPlayerClass(selectedClass.classType);
                }
                else
                {
                    if (LobbyManager.Instance != null)
                        LobbyManager.Instance.SendLocalPlayerClass(selectedClass.classType);
                }
            }
        }
        
        private void OnCardHoverEnter(LobbyClassCard card)
        {
            if (characterViewPanel == null) return;
            if (card.PlayerClass == null) return;
            
            characterViewPanel.ShowClass(card.PlayerClass);
            characterViewPanel.gameObject.SetActive(true);
        }
        
        private void OnCardHoverExit(LobbyClassCard card)
        {
            if (characterViewPanel == null) return;
            
            characterViewPanel.gameObject.SetActive(false);
        }
        
        private void ClearCards()
        {
            foreach (var card in _cards)
            {
                if (card != null)
                    Destroy(card.gameObject);
            }
            _cards.Clear();
        }
        
        // public accessor for currently selected class
        public PlayerClass GetSelectedClass()
        {
            if (_availableClasses == null || _selectedIndex >= _availableClasses.Length)
                return null;
            return _availableClasses[_selectedIndex];
        }
    }
}
