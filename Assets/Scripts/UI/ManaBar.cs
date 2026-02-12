using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Category5.UI
{
    public class ManaBar : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI manaText;
        [SerializeField] private Color manaColor = new Color(0.2f, 0.4f, 1f); // blue

        public void Initialize(int maxMana, int currentMana)
        {
            if (slider == null) return;
            
            slider.maxValue = maxMana;
            slider.value = currentMana;
            UpdateManaText(currentMana, maxMana);
            
            if (fillImage != null)
            {
                fillImage.color = manaColor;
            }
        }

        public void UpdateMana(int currentMana, int maxMana)
        {
            if (slider == null) return;

            slider.maxValue = maxMana;
            slider.value = currentMana;
            UpdateManaText(currentMana, maxMana);
        }

        private void UpdateManaText(int currentMana, int maxMana)
        {
            if (manaText == null) return;

            manaText.text = $"{currentMana}/{maxMana}";
        }
    }
}
