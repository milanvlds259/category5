using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Category5.Items;

namespace Category5.UI
{
    // ui component for displaying an inventory slot (just the item image as a clickable button)
    public class ItemSlotUI : MonoBehaviour
    {
        [Header("ui references")]
        [SerializeField] private Image iconImage; // the main item image (also acts as button background)
        [SerializeField] private Button slotButton; // the button component on the same GameObject
        [SerializeField] private TextMeshProUGUI tierText; // optional tier label

        [Header("empty slot appearance")]
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Sprite emptySprite;

        private ItemData _item;
        public event System.Action OnSlotClicked;

        private void Start()
        {
            // setup button if present
            if (slotButton != null)
            {
                slotButton.onClick.AddListener(() => OnSlotClicked?.Invoke());
            }
        }

        private void OnDestroy()
        {
            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
            }
        }

        public void SetItem(ItemData item, int tier = 1)
        {
            _item = item;

            if (item == null)
            {
                SetEmpty();
                return;
            }

            // show item icon
            if (iconImage != null)
            {
                iconImage.sprite = item.Icon;
                iconImage.color = item.GlowColor; // tint image with item color
            }

            // show tier if above 1
            if (tierText != null)
            {
                tierText.gameObject.SetActive(tier > 1);
                tierText.text = $"T{tier}";
            }

            gameObject.SetActive(true);
        }

        public void SetEmpty()
        {
            _item = null;

            if (iconImage != null)
            {
                iconImage.sprite = emptySprite;
                iconImage.color = emptyColor;
            }

            if (tierText != null)
            {
                tierText.gameObject.SetActive(false);
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (slotButton != null)
            {
                slotButton.interactable = interactable;
            }
        }

        // visually highlight slot during replacement mode (outline, glow, etc)
        public void SetHighlighted(bool highlighted)
        {
            if (iconImage != null)
            {
                // add a subtle brightness boost when highlighted
                var color = iconImage.color;
                // multiply brightness when highlighted
                if (highlighted)
                {
                    color *= 1.3f; // brighten
                }
                else if (_item != null)
                {
                    color = _item.GlowColor; // restore item color
                }
                else
                {
                    color = emptyColor; // restore empty color
                }
                iconImage.color = color;
            }
        }

        public ItemData GetItem() => _item;
    }
}
