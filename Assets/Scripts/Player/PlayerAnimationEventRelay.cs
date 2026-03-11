using UnityEngine;

namespace Category5.Player
{
    // relays animation events from the model animator object to playercombat on the root player object
    public class PlayerAnimationEventRelay : MonoBehaviour
    {
        private PlayerCombat _playerCombat;

        public void Configure(PlayerCombat playerCombat)
        {
            _playerCombat = playerCombat;
        }

        // animation event hook place this on hit frame in attack clips
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
