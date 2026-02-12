using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Category5.UI
{
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private Color healthyColor = Color.green;
        [SerializeField] private Color criticalColor = Color.red;

        public void Initialize(int maxHealth, int currentHealth)
        {
            if (slider == null) return;
            
            slider.maxValue = maxHealth;
            slider.value = currentHealth;
            UpdateColor();
            UpdateHealthText(currentHealth, maxHealth);
        }

        public void UpdateHealth(int currentHealth)
        {
            if (slider == null) return;

            slider.value = currentHealth;
            UpdateColor();
            UpdateHealthText(currentHealth, (int)slider.maxValue);
        }

		// basically a gradient from red to green based on health percentage
        private void UpdateColor()
        {
            if (fillImage == null) return;

            float healthPercent = slider.value / slider.maxValue;
            fillImage.color = Color.Lerp(criticalColor, healthyColor, healthPercent);
        }

        private void UpdateHealthText(int currentHealth, int maxHealth)
        {
            if (healthText == null) return;

            healthText.text = $"{currentHealth} / {maxHealth}";
        }
    }
}
