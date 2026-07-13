using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Category5.Player;
using Category5.Core;

namespace Category5.DebugTools
{
    /// <summary>
    /// Handles automatic host startup and initial setup when entering the Debug Map.
    /// This allows one-click testing from the editor.
    /// </summary>
    public class DebugBootstrapper : MonoBehaviour
    {
        [Header("Auto-Start Settings")]
        [SerializeField] private bool autoStartHost = true;
        [SerializeField] private int defaultClassId = 0; // Default to Ranger or similar

        private void Start()
        {
            // Only run if we are in the DebugMap scene and not already connected
            if (autoStartHost && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[DebugBootstrapper] Auto-starting host for debug testing...");
                
                // Ensure a class is selected in ClassSelectionManager before hosting
                // This mimics the lobby behavior
                if (ClassSelectionManager.GetClassId() == PlayerClass.NoClassId)
                {
                    Debug.Log($"[DebugBootstrapper] No class selected, setting default class ID: {defaultClassId}");
                    ClassSelectionManager.SetClass(defaultClassId);
                }

                NetworkManager.Singleton.StartHost();
            }
        }
    }
}
