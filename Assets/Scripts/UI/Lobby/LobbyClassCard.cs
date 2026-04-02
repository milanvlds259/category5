using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Category5.Player;
using System;
using System.Collections;

namespace Category5.UI
{
    // individual class card in the lobby select list
    // click to select, hover to show character view panel
    public class LobbyClassCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IScrollHandler
    {
        [Header("display")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI characterNameText;
        [SerializeField] private TextMeshProUGUI classNameText;
        
        [Header("selection visuals")]
        [SerializeField] private GameObject selectedBorder;
        [SerializeField] private Image cardBackground;
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color hoverColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        [SerializeField] private Color selectedColor = new Color(0.25f, 0.5f, 0.45f, 0.9f);

        [Header("hover scale")]
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float scaleSpeed = 8f;

        private Vector3 _normalScale;
        private Coroutine _scaleCoroutine;
        private ScrollRect _parentScrollRect;

        private void Awake()
        {
            _normalScale = transform.localScale;
            // cache the parent scroll rect so scroll events can be forwarded up
            _parentScrollRect = GetComponentInParent<ScrollRect>();
        }

        // events
        public static event Action<LobbyClassCard> OnCardClicked;
        public static event Action<LobbyClassCard> OnCardHoverEnter;
        public static event Action<LobbyClassCard> OnCardHoverExit;
        
        private PlayerClass _playerClass;
        private bool _isSelected;
        private bool _isHovered;
        private bool _isTaken;
        
        public PlayerClass PlayerClass => _playerClass;
        public bool IsSelected => _isSelected;
        public bool IsTaken => _isTaken;
        
        // set up the card with class data
        public void Setup(PlayerClass playerClass, Sprite fallbackSprite)
        {
            _playerClass = playerClass;
            
            if (portraitImage != null)
            {
                portraitImage.sprite = playerClass.classPortrait != null
                    ? playerClass.classPortrait
                    : (playerClass.classIcon != null ? playerClass.classIcon : fallbackSprite);
            }
            
            if (characterNameText != null)
            {
                // use characterName if set, otherwise fall back to className
                characterNameText.text = !string.IsNullOrEmpty(playerClass.characterName)
                    ? playerClass.characterName.ToUpper()
                    : playerClass.className.ToUpper();
            }
            
            if (classNameText != null)
            {
                classNameText.text = playerClass.className.ToUpper();
            }
            
            SetSelected(false);
        }
        
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            
            if (selectedBorder != null)
                selectedBorder.SetActive(selected);
            
            UpdateVisuals();
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            UpdateVisuals();
            ScaleTo(hoverScale);
            OnCardHoverEnter?.Invoke(this);
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            UpdateVisuals();
            ScaleTo(1f);
            OnCardHoverExit?.Invoke(this);
        }

        private void ScaleTo(float targetScale)
        {
            if (_scaleCoroutine != null)
                StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = StartCoroutine(ScaleCoroutine(_normalScale * targetScale));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isTaken) return;
            OnCardClicked?.Invoke(this);
        }

        // forward scroll events up to the parent scroll rect so the card list can be scrolled
        public void OnScroll(PointerEventData eventData)
        {
            if (_parentScrollRect != null)
                _parentScrollRect.OnScroll(eventData);
        }
        
        // mark this card as taken by another player - greys out portrait and blocks selection
        public void SetTaken(bool taken)
        {
            _isTaken = taken;
            
            if (portraitImage != null)
                portraitImage.color = taken ? new Color(0.35f, 0.35f, 0.35f, 1f) : Color.white;
            
            UpdateVisuals();
        }
        
        private void UpdateVisuals()
        {
            if (cardBackground == null) return;
            
            if (_isTaken)
                cardBackground.color = new Color(0.12f, 0.12f, 0.12f, 0.8f);
            else if (_isSelected)
                cardBackground.color = selectedColor;
            else if (_isHovered)
                cardBackground.color = hoverColor;
            else
                cardBackground.color = normalColor;
        }

        private IEnumerator ScaleCoroutine(Vector3 targetScale)
        {
            while (!Mathf.Approximately(transform.localScale.x, targetScale.x))
            {
                transform.localScale = Vector3.Lerp(
                    transform.localScale,
                    targetScale,
                    Time.deltaTime * scaleSpeed
                );
                yield return null;
            }
            transform.localScale = targetScale;
        }
    }
}
