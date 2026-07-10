using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
using Category5.Core;
using Category5.Player;

namespace Category5.SkillTree
{
    /// <summary>
    /// Main skill tree panel controller. Singleton that manages opening/closing,
    /// displaying nodes, handling unlock/respec actions, and currency display.
    /// Artist animation hooks are marked with [ARTIST ANIMATION] comments.
    /// </summary>
    public class SkillTreeUI : MonoBehaviour
    {
        public static SkillTreeUI Instance { get; private set; }

        [Header("Panel")]
        [Tooltip("Root canvas group for fade in/out.")]
        [SerializeField] private CanvasGroup rootCanvasGroup;

        [Tooltip("Root panel GameObject.")]
        [SerializeField] private GameObject panel;

        [Header("Header")]
        [Tooltip("Title text (e.g. 'Skill Evolution').")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Tooltip("Text displaying current skill point balance.")]
        [SerializeField] private TextMeshProUGUI skillPointsText;

        [Tooltip("Icon next to the skill point display.")]
        [SerializeField] private Image skillPointIcon;

        [Header("Character Display")]
        [Tooltip("Image displaying the current character portrait.")]
        [SerializeField] private Image characterPortrait;

        [Tooltip("Text displaying the current character name.")]
        [SerializeField] private TextMeshProUGUI characterNameText;

        [Header("Tree Content")]
        [Tooltip("Parent transform where node UI elements are instantiated.")]
        [SerializeField] private Transform nodeContainer;

        [Tooltip("Prefab for individual skill tree nodes. Must have SkillTreeNodeUI component.")]
        [SerializeField] private GameObject nodePrefab;

        [Tooltip("Prefab for connection lines between nodes (optional for MVP).")]
        [SerializeField] private GameObject linePrefab;

        [Header("Buttons")]
        [Tooltip("Reset/respec button.")]
        [SerializeField] private Button resetButton;

        [Tooltip("Close button.")]
        [SerializeField] private Button closeButton;

        [Header("Respec Confirmation")]
        [Tooltip("Confirmation dialog for respec.")]
        [SerializeField] private GameObject respecConfirmPanel;

        [Tooltip("Text showing respec cost in the confirmation dialog.")]
        [SerializeField] private TextMeshProUGUI respecCostText;

        [Tooltip("Confirm respec button.")]
        [SerializeField] private Button confirmRespecButton;

        [Tooltip("Cancel respec button.")]
        [SerializeField] private Button cancelRespecButton;

        [Header("Respec Cost Display")]
        [Tooltip("Text on the reset button showing the cost (e.g. 'Free' or '50 SP').")]
        [SerializeField] private TextMeshProUGUI resetButtonText;

        private bool _isOpen = false;
        private int _currentClassId = PlayerClass.NoClassId;
        private List<SkillTreeNodeUI> _nodeUIs = new List<SkillTreeNodeUI>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Close();
        }

        private void Update()
        {
            if (_isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void OnEnable()
        {
            if (SkillPointManager.Instance != null)
            {
                SkillPointManager.Instance.OnSkillPointsChanged += UpdateSkillPointsDisplay;
            }
        }

        private void OnDisable()
        {
            if (SkillPointManager.Instance != null)
            {
                SkillPointManager.Instance.OnSkillPointsChanged -= UpdateSkillPointsDisplay;
            }
        }

        /// <summary>
        /// Opens the skill tree UI for the specified class.
        /// </summary>
        public void Open(int classId)
        {
            if (_isOpen) return;
            _isOpen = true;
            _currentClassId = classId;

            // Notify hub UI system
            Category5.UI.HubUI.OnMenuOpened();

            // Show panel
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 1f;
                rootCanvasGroup.interactable = true;
                rootCanvasGroup.blocksRaycasts = true;
            }

            if (panel != null)
            {
                panel.SetActive(true);
            }

            // [ARTIST ANIMATION] - Panel entrance
            // Add DOTween entrance animation here, e.g.:
            // rootCanvasGroup.DOFade(1f, 0.3f).From(0f);
            // panel.transform.DOScale(1f, 0.3f).From(0.8f).SetEase(Ease.OutBack);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SetupButtons();
            PopulateTree(classId);
            UpdateSkillPointsDisplay(SaveSystem.Data.skillPoints);
            UpdateCharacterDisplay(classId);
            UpdateResetButton();
        }

        /// <summary>Closes the skill tree UI.</summary>
        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            // [ARTIST ANIMATION] - Panel exit
            // Add DOTween exit animation here, e.g.:
            // rootCanvasGroup.DOFade(0f, 0.2f).OnComplete(() => panel.SetActive(false));

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 0f;
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }

            if (panel != null)
            {
                panel.SetActive(false);
            }

            if (respecConfirmPanel != null)
            {
                respecConfirmPanel.SetActive(false);
            }

            Category5.UI.HubUI.OnMenuClosed();

            if (!Category5.UI.HubUI.IsAnyMenuOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>Populates the tree with nodes for the given class.</summary>
        private void PopulateTree(int classId)
        {
            // Clear existing nodes
            ClearNodes();

            if (SkillTreeManager.Instance == null)
            {
                Debug.LogError("SkillTreeUI: SkillTreeManager not found!");
                return;
            }

            SkillTreeData treeData = SkillTreeManager.Instance.GetTreeData(classId);
            if (treeData == null)
            {
                Debug.LogWarning($"SkillTreeUI: No skill tree data for classId {classId}");
                return;
            }

            if (treeData.nodes == null || treeData.nodes.Length == 0)
            {
                Debug.LogWarning($"SkillTreeUI: Skill tree for classId {classId} has no nodes");
                return;
            }

            int currentPoints = SaveSystem.Data.skillPoints;

            foreach (var node in treeData.nodes)
            {
                if (node == null) continue;

                // Determine node state
                bool isUnlocked = SkillTreeManager.Instance.IsNodeUnlocked(classId, node.nodeId);
                bool prereqsMet = SkillTreeManager.Instance.ArePrerequisitesMet(classId, node);
                bool canAfford = currentPoints >= node.skillPointCost;

                NodeUIState state;
                if (isUnlocked)
                {
                    state = NodeUIState.Unlocked;
                }
                else if (prereqsMet && canAfford)
                {
                    state = NodeUIState.Available;
                }
                else
                {
                    state = NodeUIState.Locked;
                }

                // Instantiate node UI
                GameObject nodeObj = Instantiate(nodePrefab, nodeContainer);
                var nodeUI = nodeObj.GetComponent<SkillTreeNodeUI>();
                if (nodeUI != null)
                {
                    nodeUI.Initialize(node, state, this);
                    nodeUI.OnNodeClicked += HandleNodeClicked;
                    _nodeUIs.Add(nodeUI);
                }
            }
        }

        /// <summary>Clears all instantiated node UIs.</summary>
        private void ClearNodes()
        {
            foreach (var nodeUI in _nodeUIs)
            {
                if (nodeUI != null && nodeUI.gameObject != null)
                {
                    Destroy(nodeUI.gameObject);
                }
            }
            _nodeUIs.Clear();
        }

        /// <summary>Handles a node being clicked - attempts to unlock it.</summary>
        private void HandleNodeClicked(SkillTreeNodeUI nodeUI)
        {
            if (SkillTreeManager.Instance == null) return;

            bool success = SkillTreeManager.Instance.TryUnlockNode(_currentClassId, nodeUI.NodeData.nodeId);
            if (success)
            {
                // [ARTIST ANIMATION] - Node unlock celebration
                // Add DOTween celebration here, e.g.:
                // nodeUI.transform.DOPunchScale(Vector3.one * 0.3f, 0.5f);
                // Spawn particle effect at node position

                // Refresh the tree to show updated states
                PopulateTree(_currentClassId);
                UpdateSkillPointsDisplay(SaveSystem.Data.skillPoints);
                UpdateResetButton();
            }
        }

        /// <summary>Updates the skill point display text.</summary>
        private void UpdateSkillPointsDisplay(int points)
        {
            if (skillPointsText != null)
            {
                skillPointsText.text = points.ToString("N0");
            }

            // [ARTIST ANIMATION] - Counter tick-up
            // If the points increased, animate the number counting up, e.g.:
            // DOTween.To(() => currentDisplayed, x => skillPointsText.text = x.ToString("N0"), points, 0.5f);
        }

        /// <summary>Updates the character portrait and name.</summary>
        private void UpdateCharacterDisplay(int classId)
        {
            if (ClassRegistry.Instance == null) return;

            PlayerClass classData = ClassRegistry.Instance.GetClass(classId);
            if (classData == null) return;

            if (characterPortrait != null && classData.classPortrait != null)
            {
                characterPortrait.sprite = classData.classPortrait;
            }

            if (characterNameText != null)
            {
                characterNameText.text = classData.className;
            }

            if (titleText != null)
            {
                titleText.text = "Skill Evolution";
            }
        }

        /// <summary>Sets up button listeners.</summary>
        private void SetupButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
                resetButton.onClick.AddListener(ShowRespecConfirm);
            }

            if (confirmRespecButton != null)
            {
                confirmRespecButton.onClick.RemoveAllListeners();
                confirmRespecButton.onClick.AddListener(ConfirmRespec);
            }

            if (cancelRespecButton != null)
            {
                cancelRespecButton.onClick.RemoveAllListeners();
                cancelRespecButton.onClick.AddListener(HideRespecConfirm);
            }
        }

        /// <summary>Updates the reset button text with current cost.</summary>
        private void UpdateResetButton()
        {
            if (SkillPointManager.Instance == null || SkillTreeManager.Instance == null) return;

            int cost = SkillPointManager.Instance.GetRespecCost(_currentClassId);
            int freeRemaining = SkillPointManager.Instance.GetFreeRespecs(_currentClassId);
            int unlockedCount = SkillTreeManager.Instance.GetUnlockedCount(_currentClassId);

            if (resetButton != null)
            {
                resetButton.interactable = unlockedCount > 0;
            }

            if (resetButtonText != null)
            {
                if (unlockedCount == 0)
                {
                    resetButtonText.text = "Reset Skill";
                }
                else if (freeRemaining > 0)
                {
                    resetButtonText.text = "Reset Skill (Free)";
                }
                else
                {
                    resetButtonText.text = $"Reset Skill ({cost} SP)";
                }
            }
        }

        /// <summary>Shows the respec confirmation dialog.</summary>
        private void ShowRespecConfirm()
        {
            if (respecConfirmPanel == null)
            {
                // No confirmation panel - just do it
                ConfirmRespec();
                return;
            }

            if (SkillPointManager.Instance != null)
            {
                int cost = SkillPointManager.Instance.GetRespecCost(_currentClassId);
                int freeRemaining = SkillPointManager.Instance.GetFreeRespecs(_currentClassId);

                if (respecCostText != null)
                {
                    if (freeRemaining > 0)
                    {
                        respecCostText.text = "Reset all skills for free? This will refund all spent skill points.";
                    }
                    else
                    {
                        respecCostText.text = $"Reset all skills for {cost} Skill Points? This will refund all spent skill points.";
                    }
                }
            }

            respecConfirmPanel.SetActive(true);
        }

        /// <summary>Hides the respec confirmation dialog.</summary>
        private void HideRespecConfirm()
        {
            if (respecConfirmPanel != null)
            {
                respecConfirmPanel.SetActive(false);
            }
        }

        /// <summary>Confirms and executes the respec.</summary>
        private void ConfirmRespec()
        {
            HideRespecConfirm();

            if (SkillTreeManager.Instance == null) return;

            bool success = SkillTreeManager.Instance.TryRespec(_currentClassId);
            if (success)
            {
                // [ARTIST ANIMATION] - Respec feedback
                // Add a reset animation here, e.g.:
                // All nodes flash and reset to locked state

                PopulateTree(_currentClassId);
                UpdateSkillPointsDisplay(SaveSystem.Data.skillPoints);
                UpdateResetButton();
            }
        }
    }
}