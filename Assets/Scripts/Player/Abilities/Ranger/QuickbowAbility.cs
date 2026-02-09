using UnityEngine;
using System.Collections;
using Unity.Netcode;
using Category5.Player;
using Category5.PowerUps;
using Category5.Items;

namespace Category5
{
    // ranger q ability - attack speed buff + burst fire mode
    public class QuickbowAbility : AbilityBase
    {
        [Header("Quickbow Settings")]
        [SerializeField] private float buffDuration = 5f;
        [SerializeField] private float attackSpeedMultiplier = 0.5f; // 50% faster (cooldown is halved)
        [SerializeField] private float chargeSpeedMultiplier = 0.6f; // 40% faster charging
        [SerializeField] private int burstArrowCount = 5;
        [SerializeField] private float burstInterval = 0.1f; // time between burst arrows
        [SerializeField] private float burstDamageMultiplier = 0.6f; // each arrow does 60% damage
        
        private PlayerCombat playerCombat;
        private Coroutine buffCoroutine;
        
        public override void Initialize(PlayerController player, PlayerStats stats, PlayerAbilityManager manager)
        {
            base.Initialize(player, stats, manager);
            playerCombat = player.GetComponent<PlayerCombat>();
        }
        
        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            if (playerCombat == null) return false;
            if (playerCombat.CurrentCombatClass != CombatClass.Ranged) return false;
            
            return true;
        }
        
        public override void Execute()
        {
            Debug.Log("QuickbowAbility.Execute() called");
            
            // start the buff
            if (buffCoroutine != null)
            {
                StopCoroutine(buffCoroutine);
            }
            buffCoroutine = StartCoroutine(QuickbowBuffCoroutine());
            
            // play vfx and audio directly (no need for RPC since we're owner)
            SpawnVfx(playerController.transform.position);
            PlayAudio(playerController.transform.position);
        }
        
        private IEnumerator QuickbowBuffCoroutine()
        {
            // apply the buff
            playerCombat.ApplyQuickbowBuff(attackSpeedMultiplier, chargeSpeedMultiplier, burstArrowCount, burstInterval, burstDamageMultiplier);
            
            // wait for buff duration
            yield return new WaitForSeconds(buffDuration);
            
            // remove the buff
            playerCombat.RemoveQuickbowBuff();
        }
    }
}
