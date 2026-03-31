using UnityEngine;
using Category5.Player;

namespace Category5.Core
{
    // singleton that holds class definitions and provides access to class data
    // persists across scenes so menu and game can both access class information
    public class ClassRegistry : MonoBehaviour
    {
        public static ClassRegistry Instance { get; private set; }
        
        [SerializeField] private PlayerClass[] availableClasses = new PlayerClass[5];
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Debug.Log($"ClassRegistry: Initialized with {availableClasses.Length} classes");
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        // get a specific class by type
        public PlayerClass GetClass(PlayerClassType classType)
        {
            foreach (var playerClass in availableClasses)
            {
                if (playerClass != null && playerClass.classType == classType)
                {
                    return playerClass;
                }
            }
            
            Debug.LogError($"ClassRegistry: No class data found for {classType}!");
            return null;
        }
        
        // get the name of a class by type
        public string GetClassName(PlayerClassType classType)
        {
            var classData = GetClass(classType);
            return classData != null ? classData.className : classType.ToString();
        }
        
        // get all available classes
        public PlayerClass[] GetAllClasses()
        {
            return availableClasses;
        }
        
        // get a class by name (for UI dropdowns, etc.)
        public PlayerClassType GetClassTypeByName(string className)
        {
            foreach (var playerClass in availableClasses)
            {
                if (playerClass != null && playerClass.className == className)
                {
                    return playerClass.classType;
                }
            }
            
            Debug.LogWarning($"ClassRegistry: No class found with name '{className}', defaulting to Ranger");
            return PlayerClassType.Ranger;
        }
    }
}
