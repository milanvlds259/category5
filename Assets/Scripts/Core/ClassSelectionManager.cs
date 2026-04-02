using UnityEngine;
using Category5.Player;

namespace Category5.Core
{
    // persistent class selection manager that survives scene transitions
    // similar to PlayerNameManager - keeps the selected class until needed
    public class ClassSelectionManager : MonoBehaviour
    {
        public static ClassSelectionManager Instance { get; private set; }

        private int _selectedClassId = PlayerClass.NoClassId;

        private void Awake()
        {
            // singleton pattern with DontDestroyOnLoad
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // get or create instance
        private static ClassSelectionManager GetOrCreateInstance()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("ClassSelectionManager");
            return go.AddComponent<ClassSelectionManager>();
        }

        // set the local player's selected class id (called from lobby ui)
        public static void SetClass(int classId)
        {
            GetOrCreateInstance()._selectedClassId = classId;
            // Debug.Log($"ClassSelectionManager: Local player class id set to {classId}");
        }

        // get the local player's selected class id (called by PlayerClassManager)
        public static int GetClassId()
        {
            return GetOrCreateInstance()._selectedClassId;
        }

        // reset selection (call when returning to menu or starting new game)
        public void ResetSelection()
        {
            _selectedClassId = PlayerClass.NoClassId;
        }
    }
}
