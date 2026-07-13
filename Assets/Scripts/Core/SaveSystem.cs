using UnityEngine;

namespace Category5.Core
{
    /// <summary>
    /// Handles JSON-based save/load of SaveData to Application.persistentDataPath.
    /// Call Save() after any metaprogression change (skill unlock, respec, run end).
    /// </summary>
    public static class SaveSystem
    {
        private const string SAVE_FILE_NAME = "category5_save.json";

        /// <summary>Cached save data loaded at startup. Always use this as the in-memory state.</summary>
        private static SaveData _cachedData;

        /// <summary>In-memory save data. Loads from disk on first access.</summary>
        public static SaveData Data
        {
            get
            {
                if (_cachedData == null)
                {
                    Load();
                }
                return _cachedData;
            }
        }

        /// <summary>Returns the full path to the save file.</summary>
        public static string GetSavePath()
        {
            return System.IO.Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        }

        /// <summary>
        /// Loads save data from disk into the cache. Returns a new SaveData if no file exists.
        /// Called automatically on first Data access, or call manually to force a reload.
        /// </summary>
        public static SaveData Load()
        {
            string path = GetSavePath();

            if (System.IO.File.Exists(path))
            {
                try
                {
                    string json = System.IO.File.ReadAllText(path);
                    _cachedData = JsonUtility.FromJson<SaveData>(json);
                    if (_cachedData == null)
                    {
                        Debug.LogWarning("SaveSystem: Save file was corrupted, creating new save data.");
                        _cachedData = new SaveData();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"SaveSystem: Failed to load save file: {e.Message}");
                    _cachedData = new SaveData();
                }
            }
            else
            {
                _cachedData = new SaveData();
            }

            return _cachedData;
        }

        /// <summary>
        /// Serializes the cached save data to disk as JSON.
        /// Call after any metaprogression change.
        /// </summary>
        public static void Save()
        {
            if (_cachedData == null)
            {
                Debug.LogWarning("SaveSystem: Cannot save - no cached data. Call Load() first.");
                return;
            }

            string path = GetSavePath();
            try
            {
                string json = JsonUtility.ToJson(_cachedData, true);
                System.IO.File.WriteAllText(path, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SaveSystem: Failed to save file: {e.Message}");
            }
        }

        /// <summary>
        /// Replaces the cached save data with a fresh instance and saves to disk.
        /// Use with caution - this wipes all metaprogression.
        /// </summary>
        public static void ResetSaveData()
        {
            _cachedData = new SaveData();
            Save();
            Debug.Log("SaveSystem: Save data has been reset.");
        }

        /// <summary>Returns true if a save file exists on disk.</summary>
        public static bool SaveExists()
        {
            return System.IO.File.Exists(GetSavePath());
        }
    }
}