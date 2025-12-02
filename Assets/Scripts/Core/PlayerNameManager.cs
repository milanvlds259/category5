using UnityEngine;

namespace Category5.Core
{
    // manages local player name storage
    // this will eventually be replaced with steam integration
    // provides a central point for getting/setting player name before connecting
    public class PlayerNameManager : MonoBehaviour
    {
        public static PlayerNameManager Instance { get; private set; }
        
        private const string PLAYER_NAME_PREF_KEY = "Category5_PlayerName";
        private const string DEFAULT_PLAYER_NAME = "Player";
        
        // the local player's name (before network sync)
        public string LocalPlayerName { get; private set; }
        
        // event fired when local name changes
        public static event System.Action<string> OnLocalNameChanged;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // load saved name or use default
            LocalPlayerName = PlayerPrefs.GetString(PLAYER_NAME_PREF_KEY, DEFAULT_PLAYER_NAME);
            Debug.Log($"PlayerNameManager: Loaded player name '{LocalPlayerName}'");
        }
        
        // set the local player name (called from menu input field)
        public void SetLocalPlayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = DEFAULT_PLAYER_NAME;
            }
            
            // sanitize and limit length
            name = name.Trim();
            if (name.Length > 20)
            {
                name = name.Substring(0, 20);
            }
            
            LocalPlayerName = name;
            PlayerPrefs.SetString(PLAYER_NAME_PREF_KEY, name);
            PlayerPrefs.Save();
            
            Debug.Log($"PlayerNameManager: Set player name to '{name}'");
            OnLocalNameChanged?.Invoke(name);
        }
        
        // get a display name with fallback
        public string GetDisplayName()
        {
            if (string.IsNullOrWhiteSpace(LocalPlayerName))
            {
                return DEFAULT_PLAYER_NAME;
            }
            return LocalPlayerName;
        }
        
        // future steam integration point
        // this method will be called when steam sdk is integrated
        public void SetSteamName(string steamName)
        {
            // when steam is integrated, this will override the local name
            // for now, it just sets the name normally
            SetLocalPlayerName(steamName);
        }
    }
}
