using UnityEngine;
using Category5.UI;

namespace Category5.Interactions
{
    public class NetworkTerminal : HubInteractable
    {
        public override void Interact(GameObject player)
        {
            if (StandaloneLobbyUI.Instance != null)
            {
                StandaloneLobbyUI.Instance.Open();
            }
            else
            {
                // Fallback
                var menu = FindFirstObjectByType<NetworkMenu>();
                if (menu != null)
                {
                    menu.OpenNetworkTerminal();
                }
                else
                {
                    Debug.LogError("NetworkTerminal: Could not find StandaloneLobbyUI or NetworkMenu in scene.");
                }
            }
        }
    }
}
