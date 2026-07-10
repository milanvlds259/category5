using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Category5.SkillTree
{
    /// <summary>
    /// UI component for a single skill tree node.
    /// Handles display state (locked/available/unlocked), hover tooltip, and click-to-unlock.
    /// Artist animation hooks are marked with [ARTIST ANIMATION] comments.
    /// </summary>
    public class SkillTreeNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("UI References")]
        [Tooltip("Image displaying the node icon.")]
        [SerializeField] private Image iconImage;

        [Tooltip("Background image of the node (changes color by state).")]
        [SerializeField] private Image backgroundImage;

        [Tooltip("Glow/border object shown when the node is available to unlock.")]
        [SerializeField] private GameObject availableGlow;

        [Tooltip("Lock icon shown when the node is locked.")]
        [SerializeField] private GameObject lockIcon;

        [Tooltip("Text showing the skill point cost.")]
        [SerializeField] private TextMeshProUGUI costText;

        [Tooltip("Text showing the node name.")]
        [SerializeField] private TextMeshProUGUI nameText;

        [Header("State Colors")]
        [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color availableColor = new Color(0.5f, 0.7f, 1f, 1f);
        [SerializeField] private Color unlockedColor = new Color(1f, 0.85f, 0.3f, 1f);

        [Header("Tooltip")]
        [Tooltip("Tooltip panel that appears on hover. Must have a SkillTreeTooltip component or just show text.")]
        [SerializeField] private GameObject tooltipPanel;

        [Tooltip("Text component on the tooltip for the description.")]
        [SerializeField] private TextMeshProUGUI tooltipDescriptionText;

        /// <summary>The node data this UI represents.</summary>
        public SkillTreeNode NodeData { get; private set; }

        /// <summary>Current display state.</summary>
        public NodeUIState CurrentState { get; private set; } = NodeUIState.Locked;

        /// <summary>Fired when the player clicks this node. Passes the nodeId.</summary>
        public event System.Action<SkillTreeNodeUI> OnNodeClicked;

        private SkillTreeUI _parentUI;

        /// <summary>Initializes the node UI with data and state.</summary>
        public void Initialize(SkillTreeNode node, NodeUIState state, SkillTreeUI parentUI)
        {
            NodeData = node;
            _parentUI = parentUI;
            CurrentState = state;

            // Set icon
            if (iconImage != null)
            {
                if (node.icon != null)
                {
                    iconImage.sprite = node.icon;
                    iconImage.color = Color.white;
                }
                else
                {
                    iconImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                }
            }

            // Set name
            if (nameText != null)
            {
                nameText.text = node.nodeName;
            }

            // Set cost
            if (costText != null)
            {
                costText.text = $"{node.skillPointCost} SP";
            }

            // Set tooltip
            if (tooltipDescriptionText != null)
            {
                tooltipDescriptionText.text = node.description;
            }

            UpdateVisualState();
        }

        /// <summary>Updates the visual state of the node.</summary>
        public void UpdateVisualState()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = CurrentState switch
                {
                    NodeUIState.Locked => lockedColor,
                    NodeUIState.Available => availableColor,
                    NodeUIState.Unlocked => unlockedColor,
                    _ => lockedColor
                };
            }

            if (availableGlow != null)
            {
                availableGlow.SetActive(CurrentState == NodeUIState.Available);
            }

            if (lockIcon != null)
            {
                lockIcon.SetActive(CurrentState == NodeUIState.Locked);
            }

            // Dim icon if locked
            if (iconImage != null)
            {
                iconImage.color = CurrentState == NodeUIState.Locked
                    ? new Color(0.4f, 0.4f, 0.4f, 1f)
                    : Color.white;
            }
        }

        /// <summary>Sets the state and updates visuals.</summary>
        public void SetState(NodeUIState newState)
        {
            CurrentState = newState;
            UpdateVisualState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // [ARTIST ANIMATION] - Node hover effect
            // Add DOTween glow/pulse here, e.g.:
            // if (availableGlow != null) availableGlow.transform.DOScale(1.1f, 0.2f);
            // backgroundImage.transform.DOPunchScale(Vector3.one * 0.05f, 0.2f);

            if (tooltipPanel != null && CurrentState != NodeUIState.Unlocked)
            {
                tooltipPanel.SetActive(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // [ARTIST ANIMATION] - Node hover exit
            // Reverse the hover effect, e.g.:
            // if (availableGlow != null) availableGlow.transform.DOScale(1f, 0.2f);

            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // [ARTIST ANIMATION] - Node click feedback
            // Add a click animation here, e.g.:
            // transform.DOPunchScale(Vector3.one * 0.15f, 0.3f);

            if (CurrentState == NodeUIState.Available)
            {
                OnNodeClicked?.Invoke(this);
            }
        }
    }

    /// <summary>Display states for a skill tree node UI.</summary>
    public enum NodeUIState
    {
        /// <summary>Prerequisites not met or insufficient points.</summary>
        Locked,
        /// <summary>Can be purchased - prerequisites met and enough points.</summary>
        Available,
        /// <summary>Already unlocked.</summary>
        Unlocked
    }
}