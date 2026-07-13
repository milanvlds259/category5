using UnityEngine;
using System;
using System.Collections.Generic;
using Category5.Core;
using Category5.Player;

namespace Category5.SkillTree
{
    /// <summary>
    /// Singleton manager for skill tree runtime state.
    /// Loads all SkillTreeData assets at startup, tracks unlocked nodes per class,
    /// and handles unlocking/respec logic. Persistence is handled via SaveSystem.
    /// </summary>
    public class SkillTreeManager : MonoBehaviour
    {
        public static SkillTreeManager Instance { get; private set; }

        [Header("Skill Tree Data")]
        [Tooltip("All skill tree data assets. Assign one per class. The manager will match them to classIds at runtime.")]
        [SerializeField] private SkillTreeData[] skillTreeData;

        /// <summary>Maps classId to its SkillTreeData.</summary>
        private Dictionary<int, SkillTreeData> _treeLookup = new Dictionary<int, SkillTreeData>();

        /// <summary>Fired when a node is unlocked. (classId, nodeId)</summary>
        public event Action<int, int> OnNodeUnlocked;

        /// <summary>Fired when a class tree is reset (respec). (classId)</summary>
        public event Action<int> OnTreeReset;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildTreeLookup();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Builds the classId -> SkillTreeData lookup from the serialized array.</summary>
        private void BuildTreeLookup()
        {
            _treeLookup.Clear();
            if (skillTreeData == null) return;

            foreach (var tree in skillTreeData)
            {
                if (tree != null && tree.classData != null)
                {
                    _treeLookup[tree.classData.classId] = tree;
                }
                else if (tree != null && tree.classData == null)
                {
                    Debug.LogWarning($"SkillTreeManager: SkillTreeData '{tree.name}' has no classData assigned. Skipping.");
                }
            }
        }

        /// <summary>Gets the SkillTreeData for a class. Returns null if not found.</summary>
        public SkillTreeData GetTreeData(int classId)
        {
            _treeLookup.TryGetValue(classId, out var data);
            return data;
        }

        /// <summary>Returns true if the given node is unlocked for the class.</summary>
        public bool IsNodeUnlocked(int classId, int nodeId)
        {
            int[] unlocked = SaveSystem.Data.GetUnlockedNodes(classId);
            foreach (int id in unlocked)
            {
                if (id == nodeId) return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if a node's prerequisites are all unlocked.
        /// A node with no prerequisites always returns true.
        /// </summary>
        public bool ArePrerequisitesMet(int classId, SkillTreeNode node)
        {
            if (node.prerequisiteNodeIds == null || node.prerequisiteNodeIds.Length == 0)
            {
                return true;
            }

            foreach (int prereqId in node.prerequisiteNodeIds)
            {
                if (!IsNodeUnlocked(classId, prereqId))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Attempts to unlock a node. Checks prerequisites and skill point balance.
        /// Returns true if successful, false otherwise.
        /// </summary>
        public bool TryUnlockNode(int classId, int nodeId)
        {
            SkillTreeData treeData = GetTreeData(classId);
            if (treeData == null)
            {
                Debug.LogError($"SkillTreeManager: No tree data for classId {classId}");
                return false;
            }

            SkillTreeNode node = treeData.GetNode(nodeId);
            if (node == null)
            {
                Debug.LogError($"SkillTreeManager: Node {nodeId} not found in tree for classId {classId}");
                return false;
            }

            if (IsNodeUnlocked(classId, nodeId))
            {
                Debug.LogWarning($"SkillTreeManager: Node {nodeId} is already unlocked for classId {classId}");
                return false;
            }

            if (!ArePrerequisitesMet(classId, node))
            {
                Debug.LogWarning($"SkillTreeManager: Prerequisites not met for node {nodeId} on classId {classId}");
                return false;
            }

            if (SkillPointManager.Instance == null)
            {
                Debug.LogError("SkillTreeManager: SkillPointManager not found!");
                return false;
            }

            if (!SkillPointManager.Instance.TrySpendPoints(node.skillPointCost))
            {
                Debug.LogWarning($"SkillTreeManager: Not enough skill points ({node.skillPointCost} needed) for node {nodeId}");
                return false;
            }

            // Add to unlocked list and save
            List<int> unlocked = new List<int>(SaveSystem.Data.GetUnlockedNodes(classId));
            unlocked.Add(nodeId);
            SaveSystem.Data.SetUnlockedNodes(classId, unlocked);
            SaveSystem.Save();

            OnNodeUnlocked?.Invoke(classId, nodeId);
            return true;
        }

        /// <summary>
        /// Resets all unlocked nodes for a class (respec).
        /// Refunds all spent skill points. Uses free respec first, then costs currency.
        /// Returns true if successful, false if respec could not be afforded.
        /// </summary>
        public bool TryRespec(int classId)
        {
            SkillTreeData treeData = GetTreeData(classId);
            if (treeData == null)
            {
                Debug.LogError($"SkillTreeManager: No tree data for classId {classId}");
                return false;
            }

            int[] unlocked = SaveSystem.Data.GetUnlockedNodes(classId);
            if (unlocked.Length == 0)
            {
                Debug.LogWarning($"SkillTreeManager: No nodes to reset for classId {classId}");
                return false;
            }

            if (SkillPointManager.Instance == null)
            {
                Debug.LogError("SkillTreeManager: SkillPointManager not found!");
                return false;
            }

            if (!SkillPointManager.Instance.TryPayRespec(classId))
            {
                Debug.LogWarning($"SkillTreeManager: Cannot afford respec for classId {classId}");
                return false;
            }

            // Calculate refund
            int refund = 0;
            foreach (int nodeId in unlocked)
            {
                SkillTreeNode node = treeData.GetNode(nodeId);
                if (node != null)
                {
                    refund += node.skillPointCost;
                }
            }

            // Clear unlocks and refund points
            SaveSystem.Data.SetUnlockedNodes(classId, new List<int>());
            SkillPointManager.Instance.AddPoints(refund);
            SaveSystem.Save();

            OnTreeReset?.Invoke(classId);
            return true;
        }

        /// <summary>
        /// Returns true if the ultimate is unlocked for the given class.
        /// The ultimate is unlocked when the root node (UltimateUnlock type) is in the unlocked list.
        /// </summary>
        public bool IsUltimateUnlocked(int classId)
        {
            SkillTreeData treeData = GetTreeData(classId);
            if (treeData == null) return true;

            SkillTreeNode rootNode = treeData.GetRootNode();
            if (rootNode == null) return true;

            return IsNodeUnlocked(classId, rootNode.nodeId);
        }

        /// <summary>Gets the total skill points spent on a class (sum of unlocked node costs).</summary>
        public int GetPointsSpent(int classId)
        {
            SkillTreeData treeData = GetTreeData(classId);
            if (treeData == null) return 0;

            int total = 0;
            foreach (int nodeId in SaveSystem.Data.GetUnlockedNodes(classId))
            {
                SkillTreeNode node = treeData.GetNode(nodeId);
                if (node != null)
                {
                    total += node.skillPointCost;
                }
            }
            return total;
        }

        /// <summary>Gets the number of nodes unlocked for a class.</summary>
        public int GetUnlockedCount(int classId)
        {
            return SaveSystem.Data.GetUnlockedNodes(classId).Length;
        }
    }
}