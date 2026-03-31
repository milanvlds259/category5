using UnityEngine;
using Category5.Player;

namespace Category5.Core
{
    // persistent class selection manager that survives scene transitions
    // similar to PlayerNameManager - keeps the selected class until needed
    public class ClassSelectionManager : MonoBehaviour
    {
        public static ClassSelectionManager Instance { get; private set; }

        private PlayerClassType _localPlayerSelectedClass = PlayerClassType.Ranger;

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

        // set the local player's selected class (called from lobby UI)
        public static void SetClass(PlayerClassType classType)
        {
            GetOrCreateInstance()._localPlayerSelectedClass = classType;
            // Debug.Log($"ClassSelectionManager: Local player class set to {classType}");
        }

        // get the local player's selected class (called by PlayerClassManager)
        public static PlayerClassType GetClass()
        {
            return GetOrCreateInstance()._localPlayerSelectedClass;
        }

        // reset selection (call when returning to menu or starting new game)
        public void ResetSelection()
        {
            _localPlayerSelectedClass = PlayerClassType.Ranger;
        }
    }
}
