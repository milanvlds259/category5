using UnityEngine;

namespace Category5.Core
{
    // visual-only storm map customization - to let summer control how the map looks
    // without affecting the procedural room layout, connections, or gameplay
    // create via right-click > create > category5 > storm blueprint
    // assign to StormData.blueprint — UI reads it automatically
    [CreateAssetMenu(menuName = "Category5/Storm Blueprint")]
    public class StormBlueprint : ScriptableObject
    {
        [Header("map visuals")]
        [Tooltip("background art shown behind the map in the selection UI")]
        public Sprite mapBackground;

        [Tooltip("overall color tint for the map")]
        public Color mapTint = Color.white;

        [Header("room visuals")]
        [Tooltip("visual overrides per eyewall ring — index 0 = outermost, -1 = eye room")]
        public RoomVisualOverride[] roomVisuals;

        // =====================================
        // lookups
        // =====================================

        // finds the visual override for a given eyewall index (-1 = eye)
        // returns null if no matching override exists
        public RoomVisualOverride? GetVisualForEyewall(int eyewallIndex)
        {
            if (roomVisuals == null) return null;

            for (int i = 0; i < roomVisuals.Length; i++)
            {
                if (roomVisuals[i].eyewallIndex == eyewallIndex)
                    return roomVisuals[i];
            }

            return null;
        }

        // returns the icon for a given eyewall index, or null if not overridden
        public Sprite GetIconForEyewall(int eyewallIndex)
        {
            var visual = GetVisualForEyewall(eyewallIndex);
            return visual.HasValue ? visual.Value.roomIcon : null;
        }

        // returns the node color for a given eyewall index, or white if not overridden
        public Color GetColorForEyewall(int eyewallIndex)
        {
            var visual = GetVisualForEyewall(eyewallIndex);
            return visual.HasValue ? visual.Value.nodeColor : Color.white;
        }

        // returns the label for a given eyewall index, or empty if not overridden
        public string GetLabelForEyewall(int eyewallIndex)
        {
            var visual = GetVisualForEyewall(eyewallIndex);
            return visual.HasValue ? visual.Value.label : string.Empty;
        }
    }

    // visual override for a single eyewall ring
    // the artist can set different icons/colors/labels per ring depth
    [System.Serializable]
    public struct RoomVisualOverride
    {
        [Tooltip("which ring this applies to: -1 = eye room, 0 = outermost, 1 = next inner, etc.")]
        public int eyewallIndex;

        [Tooltip("custom icon shown on map nodes in this ring (null = use default)")]
        public Sprite roomIcon;

        [Tooltip("color of map nodes in this ring")]
        public Color nodeColor;

        [Tooltip("optional label shown on or near nodes in this ring")]
        public string label;
    }
}
