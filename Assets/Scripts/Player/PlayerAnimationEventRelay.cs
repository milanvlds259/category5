using UnityEngine;
using UnityEngine.Scripting;

// NOTE: the preserve things are to prevent the methods from being stripped out during build
// since they do for some reason which messes up the builds.
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
