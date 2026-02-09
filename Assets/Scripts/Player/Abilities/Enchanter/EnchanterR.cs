using UnityEngine;
using System;
using Category5.Player;
using Category5.Core;

namespace Category5
{

    // enchanter r - lightning bolt that buffs allies in an area, radius scales with charges
    public class EnchanterR : AbilityBase
    {
        public static event Action<Vector3, float, int> OnLightningStrike;

        public override void Execute()
        {
            if (!CanUse()) return;

            Vector3 position = transform.position;
            abilityManager.ExecuteEnchanterRBuffServerRpc(position);
        }

        public static void InvokeLightningStrike(Vector3 position, float radius, int alliesBuffed)
        {
            OnLightningStrike?.Invoke(position, radius, alliesBuffed);
        }
    }
}
