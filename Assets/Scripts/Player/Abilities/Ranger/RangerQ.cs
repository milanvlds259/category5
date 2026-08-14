using UnityEngine;
using System.Collections;
using Unity.Netcode;
using Category5.Player;
using Category5.Items;

namespace Category5
{
    // ranger q ability - attack speed buff + burst fire mode
    public class RangerQ : AbilityBase
    {
        [Header("Quickbow Settings")]
        [SerializeField] private float buffDuration = 5f;
        [SerializeField] private float attackSpeedMultiplier = 0.5f; // 50% faster (cooldown is halved)
        [SerializeField] private float chargeSpeedMultiplier = 0.6f; // 40% faster charging
        [SerializeField] private int burstArrowCount = 5;
        [SerializeField] private float burstInterval = 0.1f; // time between burst arrows
        [SerializeField] private float burstDamageMultiplier = 0.6f; // each arrow does 60% damage

        [Header("vfx")]
        [Tooltip("spawned once when the quickbow buff activates")]
        [SerializeField] private GameObject buffActivateVfxPrefab;
        [Tooltip("spawned once when the quickbow buff expires")]
        [SerializeField] private GameObject buffExpireVfxPrefab;

        // events for vfx/sfx hooks
        public static event System.Action<Vector3> OnQuickbowActivated;
        public static event System.Action<Vector3> OnQuickbowExpired;

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
            // start the buff
            if (buffCoroutine != null)
            {
                StopCoroutine(buffCoroutine);
            }
            buffCoroutine = StartCoroutine(QuickbowBuffCoroutine());

            // play vfx and audio directly (no need for RPC since we're owner)
            SpawnVfx(playerController.transform.position);
            PlayAudio(playerController.transform.position);

            // fire event + spawn buff activate vfx
            Vector3 pos = playerController.transform.position;
            OnQuickbowActivated?.Invoke(pos);
            if (buffActivateVfxPrefab != null)
                Instantiate(buffActivateVfxPrefab, pos, Quaternion.identity);
        }

        private IEnumerator QuickbowBuffCoroutine()
        {
            // apply the buff
            playerCombat.ApplyRangerQBuff(attackSpeedMultiplier, chargeSpeedMultiplier, burstArrowCount, burstInterval, burstDamageMultiplier);

            // wait for buff duration
            yield return new WaitForSeconds(buffDuration);

            // remove the buff
            playerCombat.RemoveRangerQBuff();

            // fire event + spawn buff expire vfx
            Vector3 pos = playerController != null ? playerController.transform.position : Vector3.zero;
            OnQuickbowExpired?.Invoke(pos);
            if (buffExpireVfxPrefab != null)
                Instantiate(buffExpireVfxPrefab, pos, Quaternion.identity);
        }
    }
}
