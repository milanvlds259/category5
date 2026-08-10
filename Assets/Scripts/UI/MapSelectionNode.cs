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

        // colors
        private static readonly Color ClearedColor = new Color(0.4f, 0.4f, 0.4f, 0.7f);
        private static readonly Color CurrentColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color SelectableColor = new Color(0f, 0.85f, 1f, 1f);
        private static readonly Color EyeColor = new Color(1f, 0.2f, 0.2f, 1f);
        private static readonly Color DefaultColor = new Color(0.55f, 0.55f, 0.65f, 0.8f);

        public void Initialize(int roomIndex, bool isEyeRoom)
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

            // start as default
            SetVisualState(DefaultColor);
        }

        public void SetVisualState(Color color)
        {
            if (backgroundImage != null)
                backgroundImage.color = color;

            if (ringIndicator != null)
                ringIndicator.color = color * 0.8f;
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
