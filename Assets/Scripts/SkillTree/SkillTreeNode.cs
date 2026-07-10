using UnityEngine;

namespace Category5.SkillTree
{
    /// <summary>
    /// Defines what kind of upgrade a skill tree node provides.
    /// Expand this enum as new node types are added.
    /// </summary>
    public enum NodeType
    {
        /// <summary>Unlocks the character's ultimate (R ability). Always the root node.</summary>
        UltimateUnlock,
        /// <summary>Passive stat boost (e.g. +10% damage, +50 HP).</summary>
        StatBoost,
        /// <summary>Modifies an existing ability (e.g. wider AoE, shorter cooldown).</summary>
        AbilityEnhance,
        /// <summary>Grants a new passive effect.</summary>
        PassiveUnlock
    }

    /// <summary>
    /// ScriptableObject defining a single node in a character's skill tree.
    /// Designers create these via Create > Category5 > Skill Tree Node.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkillTreeNode", menuName = "Category5/Skill Tree Node")]
    public class SkillTreeNode : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique numeric ID for this node within its tree. Must be unique per tree.")]
        public int nodeId;

        [Tooltip("Display name shown in the UI.")]
        public string nodeName;

        [TextArea(3, 6)]
        [Tooltip("Description shown when hovering or selecting the node.")]
        public string description;

        [Tooltip("Icon sprite for the node. Optional - can be null for placeholder.")]
        public Sprite icon;

        [Header("Cost & Prerequisites")]
        [Tooltip("Skill point cost to unlock this node.")]
        public int skillPointCost = 1;

        [Tooltip("Node IDs that must be unlocked before this one becomes available. Empty = no prerequisites (root node).")]
        public int[] prerequisiteNodeIds;

        [Header("Node Type")]
        [Tooltip("What this node does when unlocked.")]
        public NodeType nodeType = NodeType.UltimateUnlock;

        // Future fields for stat boosts, ability modifiers, and passive effects
        // will be added here as the tree expands beyond the MVP ultimate-unlock node.
    }
}