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

            // for upgrades show what you'll get (next tier), otherwise show tier 1
            int displayTier = isUpgrade ? nextTier : 1;

            // set name — include tier transition for upgrades
            if (nameText != null)
            {
                nameText.text = isUpgrade
                    ? $"{item.ItemName} (T{currentTier}→T{nextTier})"
                    : item.ItemName;
            }

            // get format args from the behaviour prefab (safe to call GetComponent on prefab asset)
            object[] formatArgs = null;
            if (item.BehaviourPrefab != null)
            {
                var beh = item.BehaviourPrefab.GetComponent<ItemBehaviour>();
                if (beh != null) formatArgs = beh.GetFormatValues(displayTier);
            }

            // format the description template — designers write {0}, {1} etc. and <color> tags in the asset
            if (descriptionText != null)
                descriptionText.text = item.FormatDescription(displayTier, formatArgs);

            if (iconImage != null && item.Icon != null) iconImage.sprite = item.Icon;
            if (backgroundImage != null) backgroundImage.color = item.GlowColor;

            // show upgrade banner if upgrading
            if (upgradeBannerText != null)
            {
                upgradeBannerText.gameObject.SetActive(isUpgrade);
                if (isUpgrade) upgradeBannerText.text = "UPGRADE";
            }

            // setup button
            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(OnSelectClicked);
            }
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
