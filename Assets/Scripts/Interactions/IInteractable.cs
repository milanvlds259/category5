using UnityEngine;

namespace Category5.Interactions
{
    public interface IInteractable
    {
        string GetInteractPrompt();
        void Interact(GameObject player);
        bool CanInteract(GameObject player);
    }
}
