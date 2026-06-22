using UnityEngine;

namespace Category5.UI
{
    public static class HubUI
    {
        private static int _openMenuCount = 0;

        public static bool IsAnyMenuOpen => _openMenuCount > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            _openMenuCount = 0;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // reset menu count when loading a new scene to prevent input locks
            _openMenuCount = 0;
        }

        public static void OnMenuOpened()
        {
            _openMenuCount++;
            // Debug.Log($"HubUI: Menu opened. Count: {_openMenuCount}");
        }

        public static void OnMenuClosed()
        {
            _openMenuCount = Mathf.Max(0, _openMenuCount - 1);
            // Debug.Log($"HubUI: Menu closed. Count: {_openMenuCount}");
        }
    }
}
