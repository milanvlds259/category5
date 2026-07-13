using UnityEngine;
using Category5.Player;

namespace Category5.SkillTree
{
    /// <summary>
    /// ScriptableObject defining a character's full skill tree.
    /// Contains all nodes for one class. Designers create via Create > Category5 > Skill Tree.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkillTree", menuName = "Category5/Skill Tree")]
    public class SkillTreeData : ScriptableObject
    {
        [Header("Class Link")]
        [Tooltip("The PlayerClass this skill tree belongs to.")]
        public PlayerClass classData;

        [Header("Nodes")]
        [Tooltip("All skill tree nodes for this class. The node with nodeType UltimateUnlock and no prerequisites is the root.")]
        public SkillTreeNode[] nodes;

        [Header("Visuals (Optional)")]
        [Tooltip("Background image for the skill tree panel. Optional - artist can assign later.")]
        public Sprite treeBackground;

        /// <summary>Finds a node by its nodeId within this tree. Returns null if not found.</summary>
        public SkillTreeNode GetNode(int nodeId)
        {
            if (nodes == null) return null;
            foreach (var node in nodes)
            {
                if (node != null && node.nodeId == nodeId)
                {
                    return node;
                }
            }
            return null;
        }

        /// <summary>Returns the root node (UltimateUnlock type with no prerequisites), or null if not found.</summary>
        public SkillTreeNode GetRootNode()
        {
            if (nodes == null) return null;
            foreach (var node in nodes)
            {
                if (node != null && node.nodeType == NodeType.UltimateUnlock &&
                    (node.prerequisiteNodeIds == null || node.prerequisiteNodeIds.Length == 0))
                {
                    return node;
                }
            }
            return null;
        }
    }
}