using UnityEngine;
using TMPro;

namespace Category5.UI
{
    public class InteractionUI : MonoBehaviour
    {
        public static InteractionUI Instance { get; private set; }

        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private TextMeshProUGUI promptText;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            Hide();
        }

        public void Show(string prompt)
        {
            if (promptText != null)
            {
                promptText.text = prompt;
            }

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 1f;
            }
        }

        public void Hide()
        {
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 0f;
            }
        }
    }
}
