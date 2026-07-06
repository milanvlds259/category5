using UnityEngine;
using System.Collections.Generic;
using Category5.Player;

namespace Category5.Core
{
    // singleton that holds class definitions and provides access to class data
    // persists across scenes so menu and game can both access class information
    public class ClassRegistry : MonoBehaviour
    {
        public static ClassRegistry Instance { get; private set; }
        
        [SerializeField] private PlayerClass[] availableClasses = new PlayerClass[0];
        public PlayerClass[] AvailableClasses { get => availableClasses; set => availableClasses = value; }

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
        
        // get a specific class by id
        public PlayerClass GetClass(int classId)
        {
            foreach (var playerClass in availableClasses)
            {
                if (playerClass != null && playerClass.classId == classId)
                {
                    return playerClass;
                }
            }
            
            Debug.LogError($"ClassRegistry: No class data found for classId {classId}!");
            return null;
        }
        
        // get the name of a class by id
        public string GetClassName(int classId)
        {
            var classData = GetClass(classId);
            return classData != null ? classData.className : $"Unknown({classId})";
        }
        
        // get all available classes
        public PlayerClass[] GetAllClasses()
        {
            return availableClasses;
        }
        
        // get a class id by name (for ui dropdowns, etc.)
        public int GetClassIdByName(string className)
        {
            foreach (var playerClass in availableClasses)
            {
                if (playerClass != null && playerClass.className == className)
                {
                    return playerClass.classId;
                }
            }
            
            Debug.LogWarning($"ClassRegistry: No class found with name '{className}'");
            return PlayerClass.NoClassId;
        }
    }
}
