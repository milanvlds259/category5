using System.Collections;
using UnityEngine;
using Category5.Core;

namespace Category5.Items
{
    // backup plan: intercepts death once per round, revives the player and grants brief invulnerability
    // resets at the start of every new round so it's always ready
    public class BackupPlanBehaviour : ItemBehaviour
    {
        // invulnerability window after proc, per tier
        [SerializeField] private float[] invulnerabilityDuration = { 0.5f, 0.65f, 0.8f, 1.0f, 1.25f };

        // fraction of max HP to restore on proc per tier
        [SerializeField] private float[] reviveHealthFraction = { 0.40f, 0.45f, 0.50f, 0.55f, 0.60f };

        private bool _usedThisRound;

        protected override void OnInitialize()
        {
            if (!IsServer) return;
            _usedThisRound = false;
            PlayerController.OnPlayerAboutToDie += OnAboutToDie;
            GameFlowManager.OnRoundStarted += OnRoundChanged;
        }

        protected override void OnTierChanged(int oldTier, int newTier)
        {
            // upgrading while used mid-round resets it immediately
            _usedThisRound = false;
        }

        public override void OnRemoved()
        {
            if (PlayerController != null)
                PlayerController.OnPlayerAboutToDie -= OnAboutToDie;

            GameFlowManager.OnRoundStarted -= OnRoundChanged;
        }

        private void OnRoundChanged(int round)
        {
            _usedThisRound = false;
        }

        private void OnAboutToDie(Player.PlayerController player, ref bool preventDeath)
        {
            if (_usedThisRound) return;
            if (player != PlayerController) return;

            _usedThisRound = true;
            preventDeath = true;

            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);

            int healAmount = Mathf.Max(1, Mathf.RoundToInt(PlayerController.MaxHealth * reviveHealthFraction[idx]));
            PlayerController.Heal(healAmount);

            StartCoroutine(InvulnerabilityWindow(invulnerabilityDuration[idx]));

            // notify the owning client for vfx feedback
            NotifyProcClientRpc(new Unity.Netcode.ClientRpcParams
            {
                Send = new Unity.Netcode.ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { OwnerClientId }
                }
            });
        }

        private IEnumerator InvulnerabilityWindow(float duration)
        {
            PlayerController.IsInvulnerable = true;
            yield return new WaitForSeconds(duration);
            PlayerController.IsInvulnerable = false;
        }

        [Unity.Netcode.ClientRpc]
        private void NotifyProcClientRpc(Unity.Netcode.ClientRpcParams clientRpcParams = default)
        {
            OnBackupPlanProc?.Invoke(PlayerController != null ? PlayerController.transform.position : Vector3.zero);
        }

        // vfx/sfx hook — fires on the saved player's client
        public static event System.Action<Vector3> OnBackupPlanProc;

        public override object[] GetFormatValues(int tier)
        {
            int idx = Mathf.Clamp(tier - 1, 0, 4);
            return new object[]
            {
                Mathf.RoundToInt(reviveHealthFraction[idx] * 100f),
                invulnerabilityDuration[idx]
            };
        }
    }
}

