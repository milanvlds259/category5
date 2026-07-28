using UnityEngine;

namespace Category5.Core
{
    // defines a selectable storm category (difficulty tier) shown in the lobby
    // maps to one or more StormData variants
    // create via right-click > create > category5 > storm category
    [CreateAssetMenu(menuName = "Category5/Storm Category")]
    public class StormCategoryData : ScriptableObject
    {
        [Header("identity")]
        public string categoryName = "Category 1";
        public int categoryNumber = 1;

        [Header("progression unlock")]
        [Tooltip("research level required to unlock this category")]
        public int requiredResearchLevel = 0;

        [Header("storms")]
        [Tooltip("storms available within this category (one is picked at random)")]
        public StormData[] availableStorms;

        [Header("ui")]
        public Sprite categoryIcon;
        [TextArea(2, 4)]
        public string description;

        // returns a random StormData from this category's pool
        public StormData GetRandomStorm()
        {
            if (availableStorms == null || availableStorms.Length == 0)
            {
                Debug.LogError($"[StormCategoryData] '{categoryName}' has no storms assigned!");
                return null;
            }

            return availableStorms[Random.Range(0, availableStorms.Length)];
        }

    	// checks if the player's research level meets the unlock requirement
        public bool IsUnlocked(int playerResearchLevel)
        {
            return playerResearchLevel >= requiredResearchLevel;
        }
    }
}
