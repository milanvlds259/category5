using UnityEngine;
using Category5.UI;
using Category5.Core;

namespace Category5.Interactions
{
    public class DepartureGate : HubInteractable
    {
        public override void Interact(GameObject player)
        {
            if (StandaloneLobbyUI.Instance != null)
            {
                StandaloneLobbyUI.Instance.OpenParty();
            }
            else
            {
                var menu = FindFirstObjectByType<NetworkMenu>();
                if (menu != null)
                {
                    menu.OpenPartyScreen();
                }
            }
        }

        public override string GetInteractPrompt()
        {
            return "[F] Party Management";
        }
}
}
