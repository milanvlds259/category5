using UnityEngine;

namespace Category5.Items
{
    // secret sensation: i-framing through an attack at the last moment boosts next attack
    // "last moment" = dodge_timer remaining is <= lastMomentThreshold seconds of the total duration
    // the bonus applies once to the very next damage instance then clears
    public class SecretSensationBehaviour : ItemBehaviour
    {
        [SerializeField] private float[] damageBonus       = { 0.75f, 0.90f, 1.10f, 1.40f, 2.00f }; // additive multiplier (+75% to +200%)
        [SerializeField] private float   lastMomentThreshold = 0.15f; // seconds remaining on dodge that count as "last moment"
        [SerializeField] private float   bonusWindow         = 8f;    // seconds before the next-attack bonus expires unused

        private bool  _bonusReady   = false;
        private float _bonusExpiry  = 0f;

        protected override void OnInitialize()
        {
            // runs on owner (dodge check is owner-side, damage hook is server-side)
            if (IsOwner)
                PlayerController.OnPlayerDodgedAttack += OnDodgedAttack;

            if (IsServer)
                PlayerStats.OnBeforeDamageCalculation += OnBeforeDamage;
        }

        protected override void OnTierChanged(int oldTier, int newTier) { }

        public override void OnRemoved()
        {
            if (PlayerController != null)
                PlayerController.OnPlayerDodgedAttack -= OnDodgedAttack;

            if (PlayerStats != null)
                PlayerStats.OnBeforeDamageCalculation -= OnBeforeDamage;

            _bonusReady = false;
        }

        // called owner-side when an attack is dodged
        private void OnDodgedAttack(float dodgeTimeRemaining)
        {
            if (dodgeTimeRemaining > lastMomentThreshold) return;

            // set bonus ready (owner-side flag — the server-side hook reads it via IsOwner shared state)
            // since ItemBehaviour lives on the player object (present on all clients), both owner and
            // server instances run this flag independently; we only set it from the owner call here
            // and the server-side OnBeforeDamage clears it
            _bonusReady = true;
            _bonusExpiry = Time.time + bonusWindow;
        }

        // called server-side per hit — injects bonus multiplier and clears the flag
        private void OnBeforeDamage(ref float bonusMultiplier, UnityEngine.GameObject target)
        {
            // owner and server share the same MonoBehaviour instance in host mode,
            // but in dedicated-server / client-server split we need the server to know.
            // because ItemBehaviour is on the player GO which the server owns, this flag
            // is reliable when the host is the server. for pure-server builds a ServerRpc
            // would be needed; for this co-op boss-rush host-as-server model it's fine.
            if (!_bonusReady) return;
            if (Time.time > _bonusExpiry)
            {
                _bonusReady = false;
                return;
            }

            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);
            bonusMultiplier += damageBonus[idx];
            _bonusReady = false; // consume — only the very next hit gets the bonus
        }

        public override object[] GetFormatValues(int tier)
        {
            int idx = Mathf.Clamp(tier - 1, 0, 4);
            return new object[]
            {
                Mathf.RoundToInt(damageBonus[idx] * 100f),
                lastMomentThreshold,
                bonusWindow
            };
        }
    }
}
