using UnityEngine;

namespace Category5.Core
{
    // tracks the player's research/achievement level for progression gating
    // stub for now — persists via PlayerPrefs
    // will gate access to higher storm categories in the lobby
    public class ResearchLevel : MonoBehaviour
    {
        public static ResearchLevel Instance { get; private set; }

        private const string PlayerPrefsKey = "ResearchLevel";

        [SerializeField]
        private int currentLevel = 0;

        public int CurrentLevel => currentLevel;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Load();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // increases research level by 1 and saves
        public void AddLevel()
        {
            currentLevel++;
            Save();
        }

        // sets research level to a specific value and saves
        public void SetLevel(int level)
        {
            currentLevel = Mathf.Max(0, level);
            Save();
        }

        // checks if the player has unlocked a given category
        public bool HasUnlocked(StormCategoryData category)
        {
            if (category == null) return false;
            return category.IsUnlocked(currentLevel);
        }

        private void Save()
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, currentLevel);
            PlayerPrefs.Save();
        }

        private void Load()
        {
            currentLevel = PlayerPrefs.GetInt(PlayerPrefsKey, 0);
        }
    }
}
