using UnityEngine;
using Category5.UI;

namespace Category5.Interactions
{
    public class ClassSelectionStation : HubInteractable
    {
        public override void Interact(GameObject player)
        {
            if (StandaloneCharacterSelectUI.Instance != null)
            {
                StandaloneCharacterSelectUI.Instance.Open();
            }
            else
            {
                // Fallback to NetworkMenu if standalone not found
                var menu = FindFirstObjectByType<NetworkMenu>();
                if (menu != null)
                {
                    menu.OpenCharacterSelect();
                }
                else
                {
                    Debug.LogError("ClassSelectionStation: Could not find StandaloneCharacterSelectUI or NetworkMenu in scene.");
                }
            }
        }
    }
}
