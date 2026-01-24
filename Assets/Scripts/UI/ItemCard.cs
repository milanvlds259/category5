using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Category5.Items;

namespace Category5.UI
{
    // ui component for a single item card in the selection screen
    public class ItemCard : MonoBehaviour
    {
        [Header("ui references")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Button selectButton;
        [SerializeField] private TextMeshProUGUI effectsText; // shows item effects

        private ItemData _item;
        private ItemSelectionUI _selectionUI;

        public void Initialize(ItemData item, ItemSelectionUI selectionUI)
        {
            _item = item;
            _selectionUI = selectionUI;

            if (item == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            // set ui elements
            if (nameText != null) nameText.text = item.ItemName;
            if (descriptionText != null) descriptionText.text = item.Description;
            if (iconImage != null && item.Icon != null) iconImage.sprite = item.Icon;
            if (backgroundImage != null) backgroundImage.color = item.GlowColor;

            // show effects
            if (effectsText != null)
            {
                effectsText.text = GetEffectsText(item);
            }

            // setup button
            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(OnSelectClicked);
            }
        }

        private string GetEffectsText(ItemData item)
        {
            if (item.Effects == null || item.Effects.Length == 0) return "";

            string text = "";
            foreach (var effect in item.Effects)
            {
                if (text.Length > 0) text += "\n";

                switch (effect.effectType)
                {
                    case ItemEffectType.DamageMultiplier:
                        text += $"+{(effect.value * 100):F0}% damage";
                        break;
                    case ItemEffectType.MaxHealthBonus:
                        text += $"+{effect.value:F0} max health";
                        break;
                    case ItemEffectType.DodgeCooldownReduction:
                        text += $"-{effect.value:F1}s dodge cooldown";
                        break;
                    case ItemEffectType.FlatDamageBonus:
                        text += $"+{effect.value:F0} damage";
                        break;
                    case ItemEffectType.Lifesteal:
                        text += $"{effect.value:F0} lifesteal";
                        break;
                    case ItemEffectType.MoveSpeedMultiplier:
                        text += $"+{(effect.value * 100):F0}% move speed";
                        break;
                    case ItemEffectType.AttackSpeedMultiplier:
                        text += $"+{(effect.value * 100):F0}% attack speed";
                        break;
                }
            }
            return text;
        }

        private void OnSelectClicked()
        {
            _selectionUI?.OnItemCardClicked(_item);
        }

        public void SetInteractable(bool interactable)
        {
            if (selectButton != null)
            {
                selectButton.interactable = interactable;
            }
        }
    }
}
