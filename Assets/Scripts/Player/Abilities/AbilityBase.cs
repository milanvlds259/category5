using UnityEngine;
using Category5.Player;
using Category5.Items;

namespace Category5
{
    // abstract base class for all player abilities
    // inherits from MonoBehaviour (not NetworkBehaviour) since abilities call RPCs on the player, not on themselves
    public abstract class AbilityBase : MonoBehaviour
    {
        [SerializeField] protected AbilityData abilityData;

        protected PlayerController playerController;
        protected PlayerStats playerStats;
        protected PlayerAbilityManager abilityManager;
        
        private bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        public AbilityData Data => abilityData;

        public virtual bool ConsumeCostOnExecute => true;
        public virtual bool StartCooldownOnExecute => true;

        // delegate to ability manager for network checks
        protected bool IsServer => abilityManager != null && abilityManager.IsServer;
        protected bool IsOwner => abilityManager != null && abilityManager.IsOwner;
        protected ulong OwnerClientId => abilityManager != null ? abilityManager.OwnerClientId : 0;

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
            
            // check mana cost
            if (playerController.CurrentMana.Value < abilityData.manaCost) return false;

            return true;
        }

        // execute the ability (server-side logic)
        public abstract void Execute();

        // optional input release handler for charge-style abilities
        public virtual void OnReleased() { }

        // calculate damage with item scaling
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
