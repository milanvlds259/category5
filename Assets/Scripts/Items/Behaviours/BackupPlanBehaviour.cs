using System.Collections;
using UnityEngine;

namespace Category5.Items
{
    // backup plan: intercepts death once, restores the player to half HP, grants brief invulnerability
    // consumed on proc — disappears from inventory after use
    public class BackupPlanBehaviour : ItemBehaviour
    {
        // invulnerability window after proc, per tier
        [SerializeField] private float[] invulnerabilityDuration = { 0.5f, 0.65f, 0.8f, 1.0f, 1.25f };

        // fraction of max HP to restore on proc (0.5 = 50%)
        [SerializeField] private float reviveHealthFraction = 0.5f;

        private bool _consumed;

        protected override void OnInitialize()
        {
            if (!IsServer) return;
            _consumed = false;
            PlayerController.OnPlayerAboutToDie += OnAboutToDie;
        }

        protected override void OnTierChanged(int oldTier, int newTier)
        {
            // reset if upgraded (picked again while it was already consumed — unlikely but safe)
            _consumed = false;
        }

        public override void OnRemoved()
        {
            if (PlayerController != null)
                PlayerController.OnPlayerAboutToDie -= OnAboutToDie;
        }

        private void OnAboutToDie(Player.PlayerController player, ref bool preventDeath)
        {
            if (_consumed) return;
            if (player != PlayerController) return;

            _consumed = true;
            preventDeath = true;

            // restore half HP and start the invulnerability window
            int healAmount = Mathf.Max(1, Mathf.RoundToInt(PlayerController.MaxHealth * reviveHealthFraction));
            PlayerController.Heal(healAmount);

            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);
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
            // piggyback on the existing dodge i-frame flag via PlayerController property
            PlayerController.IsInvulnerable = true;
            yield return new WaitForSeconds(duration);
            PlayerController.IsInvulnerable = false;
        }

        // hook for vfx/sfx on the client that got saved
        [Unity.Netcode.ClientRpc]
        private void NotifyProcClientRpc(Unity.Netcode.ClientRpcParams clientRpcParams = default)
        {
            // artists: subscribe to BackupPlanBehaviour.OnBackupPlanProc for vfx here
            OnBackupPlanProc?.Invoke(PlayerController != null ? PlayerController.transform.position : Vector3.zero);
        }

        // vfx/sfx hook — fires on the saved player's client
        public static event System.Action<Vector3> OnBackupPlanProc;
    }
}
