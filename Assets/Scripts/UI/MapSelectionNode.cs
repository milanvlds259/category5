using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Category5.UI
{
    // a single room node on the map selection UI
    // shows room index, ring info, and visual state
    public class MapSelectionNode : MonoBehaviour
    {
        [Header("visuals")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image ringIndicator;
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("settings")]
        [SerializeField] private float baseSize = 32f;
        [SerializeField] private float eyeSize = 44f;

        private int _roomIndex;
        private bool _isSelectable;
        private Button _button;

        public int RoomIndex => _roomIndex;

        private void Awake()
        {
            // auto-assign references since nodes are created procedurally, not from a prefab
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();

            if (labelText == null)
            {
                var label = transform.Find("Label");
                if (label != null) labelText = label.GetComponent<TextMeshProUGUI>();
            }

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        // colors
        private static readonly Color ClearedColor = new Color(0.4f, 0.4f, 0.4f, 0.7f);
        private static readonly Color CurrentColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color SelectableColor = new Color(0f, 0.85f, 1f, 1f);
        private static readonly Color EyeColor = new Color(1f, 0.2f, 0.2f, 1f);
        private static readonly Color DefaultColor = new Color(0.55f, 0.55f, 0.65f, 0.8f);

        public void Initialize(int roomIndex, bool isEyeRoom, Color? defaultColor = null, Sprite defaultIcon = null)
        {
            _roomIndex = roomIndex;
            _isSelectable = false;

            // sizing
            RectTransform rt = GetComponent<RectTransform>();
            float size = isEyeRoom ? eyeSize : baseSize;
            rt.sizeDelta = new Vector2(size, size);

            // button for clicking
            _button = gameObject.GetComponent<Button>();
            if (_button == null)
                _button = gameObject.AddComponent<Button>();

            _button.transition = Selectable.Transition.None;
            _button.onClick.AddListener(OnClicked);

            // label
            if (labelText != null)
            {
                labelText.text = isEyeRoom ? "★" : $"{roomIndex}";
                labelText.fontSize = isEyeRoom ? 16f : 12f;
            }

            // apply blueprint icon if provided
            if (backgroundImage != null && defaultIcon != null)
                backgroundImage.sprite = defaultIcon;

            // start as default (use blueprint color if provided)
            SetVisualState(defaultColor ?? DefaultColor);
        }

        public void SetVisualState(Color color)
        {
            if (backgroundImage != null)
                backgroundImage.color = color;
        }

        public void SetSelectable(bool selectable)
        {
            _isSelectable = selectable;

            if (_button != null)
                _button.interactable = selectable;

            if (canvasGroup != null)
                canvasGroup.alpha = selectable ? 1f : 0.5f;

            if (selectable)
                SetVisualState(SelectableColor);
        }

        public void SetAsCurrent()
        {
            SetVisualState(CurrentColor);
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        public void SetAsCleared()
        {
            SetVisualState(ClearedColor);
            if (canvasGroup != null) canvasGroup.alpha = 0.5f;
        }

        public void SetAsEyeRoom()
        {
            SetVisualState(EyeColor);
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        private void OnClicked()
        {
            if (!_isSelectable) return;

            var mapUI = FindFirstObjectByType<MapSelectionUI>();
            if (mapUI != null)
                mapUI.OnNodeSelected(_roomIndex);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnClicked);
        }
    }
}
