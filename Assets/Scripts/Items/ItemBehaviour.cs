using UnityEngine;
using Category5.Player;

namespace Category5.Items
{
    // abstract base class for items with unique triggered/conditional effects
    // inherits from MonoBehaviour (not NetworkBehaviour) - delegates network checks to ItemBehaviourManager
    // pattern mirrors AbilityBase: mono behaviour managed by a network behaviour parent
    public abstract class ItemBehaviour : MonoBehaviour
    {
        protected ItemBehaviourManager manager;
        public int CurrentTier { get; protected set; } = 1;

        // convenience accessors via manager
        protected PlayerController PlayerController => manager?.PlayerController;
        protected PlayerStats PlayerStats => manager?.PlayerStats;
        protected PlayerInventory PlayerInventory => manager?.PlayerInventory;
        protected PlayerCombat PlayerCombat => manager?.PlayerCombat;
        protected PlayerAbilityManager AbilityManager => manager?.AbilityManager;
        protected bool IsServer => manager != null && manager.IsServer;
        protected bool IsOwner => manager != null && manager.IsOwner;
        protected ulong OwnerClientId => manager != null ? manager.OwnerClientId : 0;

        // called when item is first acquired
        public void Initialize(ItemBehaviourManager behaviourManager, int tier)
        {
            manager = behaviourManager;
            CurrentTier = tier;
            OnInitialize();
        }

        // called when item tier is upgraded (picked the same item again)
        public void SetTier(int newTier)
        {
            int oldTier = CurrentTier;
            CurrentTier = newTier;
            OnTierChanged(oldTier, newTier);
        }

        // subclasses implement these

        // setup logic, subscribe to events, apply initial effects
        protected abstract void OnInitialize();

        // called when tier changes, update effect values
        protected virtual void OnTierChanged(int oldTier, int newTier) { }

        // cleanup when item is removed (unsubscribe events, restore values)
        public virtual void OnRemoved() { }

        // return a human-readable description of this item's effect at a given tier
        // used by the selection ui to preview upgrade stats
        public abstract string GetEffectDescription(int tier);

        // helper to get a tier-scaled value using a base and per-tier increment
        // e.g. TierScale(25f, 10f) at tier 3 = 25 + 10*(3-1) = 45
        protected float TierScale(float baseValue, float perTierIncrease)
        {
            return baseValue + perTierIncrease * (CurrentTier - 1);
        }

        // same helper but for a specific tier (used by GetEffectDescription)
        protected float TierScaleAt(float baseValue, float perTierIncrease, int tier)
        {
            return baseValue + perTierIncrease * (tier - 1);
        }
    }
}
