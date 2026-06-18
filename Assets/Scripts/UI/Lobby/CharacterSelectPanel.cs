using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Category5.Core;
using Category5.Player;
using System.Collections.Generic;
using DG.Tweening;

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
        private int _selectedIndex = -1;
        
        private void OnEnable()
        {
            LobbyClassCard.OnCardClicked += OnCardClicked;
            LobbyClassCard.OnCardHoverEnter += OnCardHoverEnter;
            LobbyClassCard.OnCardHoverExit += OnCardHoverExit;
            LobbyManager.OnLobbyPlayersChanged += RefreshCardStates;
        }
        
        private void OnDisable()
        {
            LobbyClassCard.OnCardClicked -= OnCardClicked;
            LobbyClassCard.OnCardHoverEnter -= OnCardHoverEnter;
            LobbyClassCard.OnCardHoverExit -= OnCardHoverExit;
            LobbyManager.OnLobbyPlayersChanged -= RefreshCardStates;
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
            var savedClassId = ClassSelectionManager.GetClassId();
            _selectedIndex = -1;
            
            for (int i = 0; i < _availableClasses.Length; i++)
            {
                if (savedClassId != PlayerClass.NoClassId && _availableClasses[i].classId == savedClassId)
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
            
            // make sure the card container expands to fit all cards so the scroll rect can scroll
            var fitter = cardContainer.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = cardContainer.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // force layout rebuild immediately so content height is correct before first scroll
            if (cardContainer is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            
            // ensure the scroller background catches raycasts so scroll works in empty areas between cards
            if (scrollRect != null)
            {
                var bg = scrollRect.GetComponent<Image>();
                if (bg == null)
                    bg = scrollRect.gameObject.AddComponent<Image>();
                bg.color = Color.clear;
                bg.raycastTarget = true;
            }

            // make sure character view panel starts hidden
            if (characterViewPanel != null)
            {
                HideUI(characterViewPanel.gameObject);
                // characterViewPanel.gameObject.SetActive(false);
            }
            
            // sync taken states from current lobby
            RefreshCardStates();
        }
        
        // called whenever lobby player list changes — updates which cards are greyed out
        private void RefreshCardStates()
        {
            if (_cards.Count == 0 || _availableClasses == null) return;
            if (LobbyManager.Instance == null) return;
            
            ulong localId = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) 
                ? NetworkManager.Singleton.LocalClientId : 0;
            
            var players = LobbyManager.Instance.GetLobbyPlayers();
            
            // collect classes taken by other players
            var takenIds = new HashSet<int>();
            foreach (var p in players)
            {
                if (p.ClientId != localId && p.SelectedClassId != PlayerClass.NoClassId)
                    takenIds.Add(p.SelectedClassId);
            }
            
            // sync local selection index to server-authoritative state
            int serverClassId = LobbyManager.Instance.GetPlayerClassId(localId);
            _selectedIndex = -1;
            for (int i = 0; i < _availableClasses.Length; i++)
            {
                if (_availableClasses[i].classId == serverClassId)
                {
                    _selectedIndex = i;
                    break;
                }
            }
            
            // update each card's selected and taken state
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].SetSelected(i == _selectedIndex);
                _cards[i].SetTaken(takenIds.Contains(_availableClasses[i].classId));
            }
        }
        
        private void OnCardClicked(LobbyClassCard clickedCard)
        {
            // ignore clicks on taken cards (another player has this class)
            if (clickedCard.IsTaken) return;
            
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
            ClassSelectionManager.SetClass(selectedClass.classId);
            
            // send to server via lobby manager
            if (LobbyManager.Instance != null)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    if (NetworkManager.Singleton.IsServer)
                    {
                        LobbyManager.Instance.SetHostPlayerClassId(selectedClass.classId);
                    }
                    else
                    {
                        LobbyManager.Instance.SendLocalPlayerClassId(selectedClass.classId);
                    }
                }
                else
                {
                    // offline mode
                    LobbyManager.Instance.SendLocalPlayerClassId(selectedClass.classId);
                }
            }
        }

        private void OnCardHoverEnter(LobbyClassCard card)
        {
            if (characterViewPanel == null) return;
            if (card.PlayerClass == null) return;
            
            characterViewPanel.ShowClass(card.PlayerClass);
            StopCurrentAnimations(characterViewPanel.gameObject);
            ShowUI(characterViewPanel.gameObject);
            //characterViewPanel.gameObject.SetActive(true);
        }
        
        private void OnCardHoverExit(LobbyClassCard card)
        {
            if (characterViewPanel == null) return;
            
            //characterViewPanel.gameObject.SetActive(false);
            SoftHideUI(characterViewPanel.gameObject, 0.25f); // fade out 
            //HideUI(characterViewPanel.gameObject);
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
            if (_availableClasses == null || _selectedIndex < 0 || _selectedIndex >= _availableClasses.Length)
                return null;
            return _availableClasses[_selectedIndex];
        }

        private void HideUI(GameObject panel) // helper to hide all UI elements in a panel 
        {
            if (panel != null)
            {
                CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }
            }
        }

        private void ShowUI(GameObject panel) // helper to show all UI elements in a panel
        {
            if (panel != null)
            {
                CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }
            }
        }

        private void SoftHideUI(GameObject panel, float fadeLength)
        {
            if (panel != null)
            {
                CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.DOKill();
                    canvasGroup.DOFade(0, fadeLength);
                    //RT.DOAnchorPosY(-100, fadeLength).SetRelative(); // slide out to the bottom while fading
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }

            }
        }

        private void StopCurrentAnimations(GameObject panel)
        {
            if (panel != null)
            {
                CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.DOKill();
                }
            }
        }
    }
}
