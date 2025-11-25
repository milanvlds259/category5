using UnityEngine;
using UnityEngine.UI;

namespace Category5.UI
{
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Image fillImage;
        [SerializeField] private Color healthyColor = Color.green;
        [SerializeField] private Color criticalColor = Color.red;

        public void Initialize(int maxHealth, int currentHealth)
        {
            if (slider == null) return;
            
            slider.maxValue = maxHealth;
            slider.value = currentHealth;
            UpdateColor();
        }

        public void UpdateHealth(int currentHealth)
        {
            if (slider == null) return;

            slider.value = currentHealth;
            UpdateColor();
        }

		// basically a gradient from red to green based on health percentage
        private void UpdateColor()
        {
            if (fillImage == null) return;

            float healthPercent = slider.value / slider.maxValue;
            fillImage.color = Color.Lerp(criticalColor, healthyColor, healthPercent);
        }
    }
}
