using UnityEngine;
using Category5.Player;

namespace Category5
{
    // dispatcher that sits on the "Ability2" slot and delegates to the correct elemental E ability
    // based on the current element selected by ElementalistQ
    public class ElementalistE_Dispatcher : AbilityBase
    {
        [Header("sub-ability references")]
        [SerializeField] private ElementalistE_Fire fireAbility;
        [SerializeField] private ElementalistE_Ice iceAbility;
        [SerializeField] private ElementalistE_Thunder thunderAbility;

        // cached reference to Q ability for reading current element
        private ElementalistQ _elementCycler;

        // the dispatcher plays a cast animation and defers to the active sub-ability on the CastImpact event
        public override bool HasCastAnimation => true;

        // the dispatcher can be held to aim - delegates the aim type to the active sub-ability
        public override bool CanHoldToAim => true;

        // expose the active sub-ability's data so cooldown/mana use the current element's values
        public override AbilityData Data => ActiveAbilityData;

        // delegate aim direction to the active sub-ability (fire=screen-center, ice=flat forward, thunder=forward)
        public override Vector3 GetAimDirection(Vector3 spawnPos)
        {
            AbilityBase active = GetActiveAbility();
            if (active == null) return base.GetAimDirection(spawnPos);
            return active.GetAimDirection(spawnPos);
        }

        public override void Initialize(PlayerController player, PlayerStats stats, PlayerAbilityManager manager)
        {
            base.Initialize(player, stats, manager);

            // initialize sub-abilities
            if (fireAbility != null) fireAbility.Initialize(player, stats, manager);
            if (iceAbility != null) iceAbility.Initialize(player, stats, manager);
            if (thunderAbility != null) thunderAbility.Initialize(player, stats, manager);

            // find the ElementalistQ (Ability1) on sibling objects
            FindElementCycler();
        }

        private void FindElementCycler()
        {
            if (abilityManager == null) return;

            // search siblings (children of the player) for ElementalistQ
            _elementCycler = abilityManager.GetComponentInChildren<ElementalistQ>();

            if (_elementCycler == null)
            {
                Debug.LogWarning("[ElementalistE_Dispatcher] could not find ElementalistQ on player");
            }
        }

        // return the active sub-ability based on current element
        public AbilityBase GetActiveAbility()
        {
            if (_elementCycler == null) FindElementCycler();
            if (_elementCycler == null) return fireAbility; // fallback

            return _elementCycler.CurrentElement switch
            {
                ElementMode.Fire => fireAbility,
                ElementMode.Ice => iceAbility,
                ElementMode.Thunder => thunderAbility,
                _ => fireAbility
            };
        }

        public override bool CanUse()
        {
            // delegate to the active sub-ability's CanUse
            AbilityBase active = GetActiveAbility();
            if (active == null) return false;

            // check base conditions (mana from dispatcher's own abilityData, or from sub-ability)
            // use the sub-ability's data for mana cost check
            if (playerController == null) return false;
            if (playerController.IsDead.Value) return false;

            if (active.Data != null && playerController.CurrentMana.Value < active.Data.manaCost)
                return false;

            return true;
        }

        public override void Execute()
        {
            AbilityBase active = GetActiveAbility();
            if (active == null)
            {
                Debug.LogError("[ElementalistE_Dispatcher] no active sub-ability found");
                return;
            }

            // Debug.Log($"[ElementalistE_Dispatcher] delegating to {active.GetType().Name} (element: {_elementCycler?.CurrentElement})");
            active.Execute();
        }

        // expose the active sub-ability's data for cooldown/ui purposes
        public AbilityData ActiveAbilityData
        {
            get
            {
                AbilityBase active = GetActiveAbility();
                return active != null ? active.Data : abilityData;
            }
        }
    }
}
