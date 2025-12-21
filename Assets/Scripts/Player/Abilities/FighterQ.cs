using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Audio;
using Category5.Enemies;
using Category5.Player;

namespace Category5
{
    // fighter q ability - overhead smash that deals aoe damage and stuns nearby regular enemies
    public class FighterQ : AbilityBase
    {
        private float executionTimer;
        
        // events for vfx/sfx
        public static event System.Action<Vector3, int> OnSmashExecute;
        public static event System.Action<Vector3> OnSmashHit;
        
        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            
            // check ability manager cooldown
            if (abilityManager.ability1Cooldown.Value > 0) return false;
            
            return true;
        }

        public override void Execute()
        {
            if (!CanUse()) return;
            
            // request server to execute
            ExecuteSmashServerRpc(playerController.transform.position);
        }

        [Rpc(SendTo.Server)]
        private void ExecuteSmashServerRpc(Vector3 executePosition)
        {
            if (!IsServer) return;
            
            // apply damage modifier from player stats
            int adjustedDamage = playerStats.CalculateDamage((int)abilityData.baseDamage);
            
            // find enemies in aoe (aoe radius 5m)
            Collider[] hitColliders = Physics.OverlapSphere(executePosition, 5f);
            
            int enemiesHit = 0;
            foreach (Collider collider in hitColliders)
            {
                var enemy = collider.GetComponent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    // apply stun (0.75 seconds)
                    enemy.ApplyStun(0.75f);
                    
                    // apply damage
                    enemy.TakeDamage(adjustedDamage);
                    enemiesHit++;
                }
            }
            
            // notify clients for vfx/sfx
            if (enemiesHit > 0)
            {
                TriggerSmashEffectsClientRpc(executePosition, enemiesHit);
            }
            
            // set cooldown
            abilityManager.ability1Cooldown.Value = abilityData.cooldownDuration;
        }
        
        [ClientRpc]
        private void TriggerSmashEffectsClientRpc(Vector3 position, int enemiesHit)
        {
            OnSmashExecute?.Invoke(position, enemiesHit);
            OnSmashHit?.Invoke(position);
            
            // trigger hit feedback
            if (IsOwner && HitFeedbackManager.Instance != null)
            {
                HitFeedbackManager.Instance.TriggerHeavyHit(position);
            }
        }

        // note: cooldowns are managed by PlayerAbilityManager
    }
}
