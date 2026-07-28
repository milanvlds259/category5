using System.Collections;
using UnityEngine;
using Category5.Core;
using Category5.Map;

namespace Category5.Items
{
    // backup plan: intercepts death once per room, revives the player and grants brief invulnerability
    // resets at the start of every new room so it's always ready
    public class BackupPlanBehaviour : ItemBehaviour
    {
        // invulnerability window after proc, per tier
        [SerializeField] private float[] invulnerabilityDuration = { 0.5f, 0.65f, 0.8f, 1.0f, 1.25f };

        // fraction of max HP to restore on proc per tier
        [SerializeField] private float[] reviveHealthFraction = { 0.40f, 0.45f, 0.50f, 0.55f, 0.60f };

        private bool _usedThisRoom;

        protected override void OnInitialize()
        {
            if (!IsServer) return;
            _usedThisRoom = false;
            PlayerController.OnPlayerAboutToDie += OnAboutToDie;
            RoomTransitionManager.OnRoomEntered += OnRoomEntered;
        }

        protected override void OnTierChanged(int oldTier, int newTier)
        {
            // upgrading while used mid-room resets it immediately
            _usedThisRoom = false;
        }

        public override void OnRemoved()
        {
            if (PlayerController != null)
                PlayerController.OnPlayerAboutToDie -= OnAboutToDie;

            RoomTransitionManager.OnRoomEntered -= OnRoomEntered;
        }

        private void OnRoomEntered(StormRoom room)
        {
            _usedThisRoom = false;
        }

        private void OnAboutToDie(Player.PlayerController player, ref bool preventDeath)
        {
            if (_usedThisRoom) return;
            if (player != PlayerController) return;

            _usedThisRoom = true;
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

