using UnityEngine;
using Category5.UI;
using Category5.Core;

namespace Category5.Interactions
{
    public class DepartureGate : HubInteractable
    {
        public override void Interact(GameObject player)
        {
            var lobbyManager = LobbyManager.Instance;
            if (lobbyManager == null) return;

            if (lobbyManager.AreAllPlayersReady())
            {
                var menu = FindFirstObjectByType<NetworkMenu>();
                if (menu != null)
                {
                    menu.OnStartGameClicked();
                }
            }
            else
            {
                Debug.Log("DepartureGate: Not all players are ready!");
                // Could show a UI message here
            }
        }

        public override string GetInteractPrompt()
        {
            var lobbyManager = LobbyManager.Instance;
            string basePrompt = "Wait for Players";
            
            if (lobbyManager != null && lobbyManager.AreAllPlayersReady())
            {
                basePrompt = "Start Run";
            }
            
            return $"[F] {basePrompt}";
        }
}
}
