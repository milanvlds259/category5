using UnityEngine;
using System;
using Category5.Player;
using Category5.Core;

namespace Category5
{
    // the three element modes the elementalist can cycle through
    public enum ElementMode
    {
        Fire,
        Ice,
        Thunder
    }

    // elementalist q ability - cycle through different elements for E
    public class ElementalistQ : AbilityBase
    {
        [Header("element cycle")]
        [SerializeField] private ElementMode currentElement = ElementMode.Fire;

        // public accessor for other scripts (dispatcher reads this)
        public ElementMode CurrentElement => currentElement;

        // event fired when element changes (for ui/vfx)
        public static event Action<ElementMode> OnElementChanged;

        public override void Execute()
        {
            // cycle to next element: fire -> ice -> thunder -> fire
            currentElement = currentElement switch
            {
                ElementMode.Fire => ElementMode.Ice,
                ElementMode.Ice => ElementMode.Thunder,
                ElementMode.Thunder => ElementMode.Fire,
                _ => ElementMode.Fire
            };

            Debug.Log($"[ElementalistQ] element cycled to {currentElement}");

            // notify listeners (ui, vfx)
            OnElementChanged?.Invoke(currentElement);

            // play audio/vfx feedback
            SpawnVfx(playerController.transform.position);
            PlayAudio(playerController.transform.position);
        }
    }
}
