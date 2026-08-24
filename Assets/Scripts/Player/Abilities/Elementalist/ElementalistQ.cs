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

        [Header("q icons")]
        [SerializeField] private Sprite fireIcon;
        [SerializeField] private Sprite iceIcon;
        [SerializeField] private Sprite thunderIcon;

        [Header("element switch buff")]
        [Tooltip("how long the damage buff lasts after cycling element")]
        [SerializeField] private float eBuffDuration = 4f;

        [Tooltip("additive damage multiplier applied to E after cycling element")]
        [SerializeField] private float eBuffDamageMultiplier = 0.25f;

        [Header("vfx")]
        [Tooltip("vfx spawned when cycling to fire")]
        [SerializeField] private GameObject fireCycleVfxPrefab;
        [Tooltip("vfx spawned when cycling to ice")]
        [SerializeField] private GameObject iceCycleVfxPrefab;
        [Tooltip("vfx spawned when cycling to thunder")]
        [SerializeField] private GameObject thunderCycleVfxPrefab;

        // public accessor for other scripts (dispatcher reads this)
        public ElementMode CurrentElement => currentElement;

        public Sprite CurrentIcon => GetIconForElement(currentElement);

        // event fired when element changes (for ui/vfx)
        public static event Action<ElementMode> OnElementChanged;

        // event fired when the element switch buff starts (for ui/vfx - buff indicator)
        public static event Action<ElementMode, float> OnElementBuffStarted; // element, duration

        public Sprite GetIconForElement(ElementMode mode)
        {
            return mode switch
            {
                ElementMode.Fire => fireIcon,
                ElementMode.Ice => iceIcon,
                ElementMode.Thunder => thunderIcon,
                _ => fireIcon
            };
        }

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

            // Debug.Log($"[ElementalistQ] element cycled to {currentElement}");

            // notify listeners (ui, vfx)
            OnElementChanged?.Invoke(currentElement);

            // spawn element-specific cycle vfx
            GameObject cycleVfx = currentElement switch
            {
                ElementMode.Fire => fireCycleVfxPrefab,
                ElementMode.Ice => iceCycleVfxPrefab,
                ElementMode.Thunder => thunderCycleVfxPrefab,
                _ => null
            };
            if (cycleVfx != null)
                Instantiate(cycleVfx, playerController.transform.position, Quaternion.identity);

            // apply temporary damage buff (encourages switching elements rather than spamming fireball)
            if (playerStats != null && eBuffDamageMultiplier > 0f && eBuffDuration > 0f)
            {
                playerStats.ApplyTemporaryMultiplier("damage", eBuffDamageMultiplier, eBuffDuration);
                OnElementBuffStarted?.Invoke(currentElement, eBuffDuration);
            }

            // play audio/vfx feedback
            SpawnVfx(playerController.transform.position);
            PlayAudio(playerController.transform.position);
        }
    }
}
