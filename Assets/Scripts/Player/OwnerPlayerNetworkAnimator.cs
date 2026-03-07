using Unity.Netcode.Components;
using UnityEngine;

namespace Category5.Player
{
    // this makes player animation sync owner authoritative so the local player can drive anim params
	// ik this probably looks stupid but trust me its necessary
    public class OwnerPlayerNetworkAnimator : NetworkAnimator
    {
        private bool _runtimeAnimatorInitialized;
        private RuntimeAnimatorController _initializedController;
        private int _initializedLayerCount;

        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }

        // networkanimator only builds its internal caches in awake
        // our real animator gets assigned later after the class model is spawned
        public void BindRuntimeAnimator(Animator animator)
        {
            if (animator == null)
            {
                Debug.LogError("OwnerPlayerNetworkAnimator: Cannot bind a null animator.");
                return;
            }

            Animator = animator;

            if (!_runtimeAnimatorInitialized)
            {
                base.Awake();
                _runtimeAnimatorInitialized = true;
                _initializedController = animator.runtimeAnimatorController;
                _initializedLayerCount = animator.layerCount;
                return;
            }

            if (_initializedLayerCount != animator.layerCount)
            {
                Debug.LogError($"OwnerPlayerNetworkAnimator: Animator layer count changed from {_initializedLayerCount} to {animator.layerCount}. this can break animation sync.");
            }

            if (_initializedController != null && animator.runtimeAnimatorController != null && _initializedController != animator.runtimeAnimatorController)
            {
                Debug.LogWarning("OwnerPlayerNetworkAnimator: Animator controller changed after initialization. animation sync only stays safe if the parameter and layer layout still matches.");
            }
        }
    }
}
