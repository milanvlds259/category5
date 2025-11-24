using UnityEngine;
using Unity.Netcode;

namespace Category5.Utils
{
    public class AutoStartHost : MonoBehaviour
    {
        [SerializeField] private bool autoStartInEditor = true;

        private void Start()
        {
#if UNITY_EDITOR
            if (autoStartInEditor)
            {
                // Wait one frame to ensure NetworkManager is initialized
                StartCoroutine(StartHostRoutine());
            }
#endif
        }

        private System.Collections.IEnumerator StartHostRoutine()
        {
            yield return null; // Wait one frame

            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                Debug.Log("AutoStartHost: Starting Host...");
                bool success = NetworkManager.Singleton.StartHost();
                if (!success)
                {
                    Debug.LogError("AutoStartHost: Failed to start host. Check console for port errors.");
                }
            }
        }
    }
}
