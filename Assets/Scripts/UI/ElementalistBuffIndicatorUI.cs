using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Category5.UI
{
    // shows a small UI indicator with the element icon + countdown timer
    // for the post-Q damage buff on the Elementalist
    // auto-hides when the buff expires
    [DisallowMultipleComponent]
    public class ElementalistBuffIndicatorUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject indicatorRoot;
        [SerializeField] private Image elementIconImage;
        [SerializeField] private Image timerFillImage;
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("Colors")]
        [SerializeField] private Color fireColor = new Color(1f, 0.5f, 0.1f, 1f);
        [SerializeField] private Color iceColor = new Color(0.3f, 0.7f, 1f, 1f);
        [SerializeField] private Color thunderColor = new Color(0.9f, 0.85f, 0.2f, 1f);

        private float _buffRemaining;
        private float _buffTotal;
        private Category5.ElementMode _currentElement;

        private void Awake()
        {
            // hide by default until the buff starts
            if (indicatorRoot != null) indicatorRoot.SetActive(false);
        }

        private void OnEnable()
        {
            Category5.ElementalistQ.OnElementChanged += OnElementChanged;
            Category5.ElementalistQ.OnElementBuffStarted += OnElementBuffStarted;
        }

        private void OnDisable()
        {
            Category5.ElementalistQ.OnElementChanged -= OnElementChanged;
            Category5.ElementalistQ.OnElementBuffStarted -= OnElementBuffStarted;
        }

        private void Update()
        {
            if (_buffRemaining <= 0f) return;

            _buffRemaining -= Time.deltaTime;
            if (_buffRemaining <= 0f)
            {
                Hide();
                return;
            }

            // update fill + text
            if (timerFillImage != null && _buffTotal > 0f)
            {
                timerFillImage.fillAmount = _buffRemaining / _buffTotal;
            }
            if (timerText != null)
            {
                timerText.text = $"{_buffRemaining:F1}s";
            }
        }

        private void OnElementChanged(Category5.ElementMode element)
        {
            _currentElement = element;
        }

        private void OnElementBuffStarted(Category5.ElementMode element, float duration)
        {
            _currentElement = element;
            _buffTotal = duration;
            _buffRemaining = duration;

            Show(element);
        }

        private void Show(Category5.ElementMode element)
        {
            if (indicatorRoot != null) indicatorRoot.SetActive(true);

            // set icon color by element
            if (elementIconImage != null)
            {
                elementIconImage.color = GetColorForElement(element);
            }
            if (timerFillImage != null)
            {
                timerFillImage.color = GetColorForElement(element);
                timerFillImage.fillAmount = 1f;
            }
        }

        private void Hide()
        {
            if (indicatorRoot != null) indicatorRoot.SetActive(false);
        }

        private Color GetColorForElement(Category5.ElementMode element)
        {
            return element switch
            {
                Category5.ElementMode.Fire => fireColor,
                Category5.ElementMode.Ice => iceColor,
                Category5.ElementMode.Thunder => thunderColor,
                _ => Color.white
            };
        }
    }
}
