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
        [SerializeField] private TextMeshProUGUI upgradeBannerText; // shows "UPGRADE" when upgrading

        private ItemData _item;
        private ItemSelectionUI _selectionUI;

        public void Initialize(ItemData item, ItemSelectionUI selectionUI, int currentTier = 0)
        {
            _item = item;
            _selectionUI = selectionUI;

            if (item == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            bool isUpgrade = currentTier > 0;
            int nextTier = currentTier + 1;

            // set ui elements
            if (nameText != null)
            {
                if (isUpgrade)
                    nameText.text = $"{item.ItemName} (Tier {currentTier}→{nextTier})";
                else
                    nameText.text = item.ItemName;
            }
            if (descriptionText != null) descriptionText.text = item.Description;
            if (iconImage != null && item.Icon != null) iconImage.sprite = item.Icon;
            if (backgroundImage != null) backgroundImage.color = item.GlowColor;

            // show upgrade banner if upgrading
            if (upgradeBannerText != null)
            {
                upgradeBannerText.gameObject.SetActive(isUpgrade);
                if (isUpgrade) upgradeBannerText.text = "UPGRADE";
            }

            // show effects with tier scaling
            if (effectsText != null)
            {
                if (isUpgrade)
                    effectsText.text = GetUpgradeEffectsText(item, currentTier, nextTier);
                else
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

        // shows effect values at current tier vs next tier
        private string GetUpgradeEffectsText(ItemData item, int currentTier, int nextTier)
        {
            if (item.Effects == null || item.Effects.Length == 0) return "";

            string text = "";
            foreach (var effect in item.Effects)
            {
                if (text.Length > 0) text += "\n";

                float currentValue = ItemData.GetTierScaledValue(effect.value, currentTier);
                float nextValue = ItemData.GetTierScaledValue(effect.value, nextTier);

                switch (effect.effectType)
                {
                    case ItemEffectType.DamageMultiplier:
                        text += $"+{(currentValue * 100):F0}% → +{(nextValue * 100):F0}% damage";
                        break;
                    case ItemEffectType.MaxHealthBonus:
                        text += $"+{currentValue:F0} → +{nextValue:F0} max health";
                        break;
                    case ItemEffectType.DodgeCooldownReduction:
                        text += $"-{currentValue:F1}s → -{nextValue:F1}s dodge cooldown";
                        break;
                    case ItemEffectType.FlatDamageBonus:
                        text += $"+{currentValue:F0} → +{nextValue:F0} damage";
                        break;
                    case ItemEffectType.Lifesteal:
                        text += $"{currentValue:F0} → {nextValue:F0} lifesteal";
                        break;
                    case ItemEffectType.MoveSpeedMultiplier:
                        text += $"+{(currentValue * 100):F0}% → +{(nextValue * 100):F0}% move speed";
                        break;
                    case ItemEffectType.AttackSpeedMultiplier:
                        text += $"+{(currentValue * 100):F0}% → +{(nextValue * 100):F0}% attack speed";
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
