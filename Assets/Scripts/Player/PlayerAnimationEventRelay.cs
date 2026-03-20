using UnityEngine;
using UnityEngine.Scripting;

// NOTE: the preserve things are to prevent the methods from being stripped out during build since they're 
// only called via animation events and not referenced directly in code which makes them look unused to 
// the build process for some reason if these get stripped then the animation events won't fire and attacks won't work
// so we needed to make sure they stay in the build. ideally we'd have a more robust way of marking animation event methods to preserve but for now this should do the trick.
namespace Category5.Player
{
    // relays animation events from the model animator object to playercombat on the root player object
    [Preserve]
    public class PlayerAnimationEventRelay : MonoBehaviour
    {
        private PlayerCombat _playerCombat;

        public void Configure(PlayerCombat playerCombat)
        {
            _playerCombat = playerCombat;
        }

        // animation event hook place this on hit frame in attack clips
        [Preserve]
        public void AttackImpact()
        {
            if (_playerCombat == null)
            {
                _playerCombat = GetComponentInParent<PlayerCombat>();
            }

            if (_playerCombat != null)
            {
                _playerCombat.OnAttackImpactAnimationEvent();
            }
        }

        // animation event hook opens the melee chain window
        [Preserve]
        public void AttackChainWindowOpen()
        {
            if (_playerCombat == null)
            {
                _playerCombat = GetComponentInParent<PlayerCombat>();
            }

            if (_playerCombat != null)
            {
                _playerCombat.OnAttackChainWindowOpenAnimationEvent();
            }
        }

        // animation event hook closes the melee chain window
        [Preserve]
        public void AttackChainWindowClose()
        {
            if (_playerCombat == null)
            {
                _playerCombat = GetComponentInParent<PlayerCombat>();
            }

            if (_playerCombat != null)
            {
                _playerCombat.OnAttackChainWindowCloseAnimationEvent();
            }
        }
    }
}
