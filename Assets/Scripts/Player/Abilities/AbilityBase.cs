using Unity.Netcode;
using UnityEngine;
using Category5.Player;
using Category5.PowerUps;

namespace Category5
{
    // abstract base class for all player abilities
    public abstract class AbilityBase : NetworkBehaviour
    {
        [SerializeField] protected AbilityData abilityData;

        protected PlayerController playerController;
        protected PlayerStats playerStats;
        protected PlayerAbilityManager abilityManager;
        
        private bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        public AbilityData Data => abilityData;

        public virtual void Initialize(PlayerController player, PlayerStats stats, PlayerAbilityManager manager)
        {
            playerController = player;
            playerStats = stats;
            abilityManager = manager;
            _isInitialized = true;
        }

        // check if the ability can be used right now
        public virtual bool CanUse()
        {
            if (abilityData == null) return false;
            if (playerController == null) return false;
            if (playerController.IsDead.Value) return false;

            return true;
        }

        // execute the ability (server-side logic)
        public abstract void Execute();

        // calculate damage with power-up scaling
        protected float CalculateDamage()
        {
            if (playerStats != null)
            {
                return playerStats.CalculateDamage((int)abilityData.baseDamage);
            }
            return abilityData.baseDamage;
        }

        // helper to spawn vfx at position (client-side)
        protected void SpawnVfx(Vector3 position)
        {
            if (abilityData.vfxPrefab != null)
            {
                Instantiate(abilityData.vfxPrefab, position, Quaternion.identity);
            }
        }

        // helper to play audio at position (client-side)
        protected void PlayAudio(Vector3 position)
        {
            if (abilityData.sfxClip != null)
            {
                AudioSource.PlayClipAtPoint(abilityData.sfxClip, position);
            }
        }
    }
}
