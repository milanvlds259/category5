using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Category5.PowerUps
{
    // ui component for a single power-up card in the selection screen
    public class PowerUpCard : MonoBehaviour
    {
        [Header("ui references")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Button selectButton;

        private int _powerUpIndex;
        private PowerUpSelectionUI _selectionUI;

        public void Initialize(PowerUpData powerUp, int index, PowerUpSelectionUI selectionUI)
        {
            _powerUpIndex = index;
            _selectionUI = selectionUI;

            if (powerUp == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            // set ui elements
            if (nameText != null) nameText.text = powerUp.PowerUpName;
            if (descriptionText != null) descriptionText.text = powerUp.Description;
            if (iconImage != null && powerUp.Icon != null) iconImage.sprite = powerUp.Icon;
            if (backgroundImage != null) backgroundImage.color = powerUp.GlowColor;

            // setup button
            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(OnSelectClicked);
            }
        }

        private void OnSelectClicked()
        {
            _selectionUI?.OnCardSelected(_powerUpIndex);
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
