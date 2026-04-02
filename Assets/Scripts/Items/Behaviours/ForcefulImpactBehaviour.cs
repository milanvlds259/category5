using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Category5.Player;
using Category5.Core;

namespace Category5.Items
{
    // forceful impact: sprinting or dodging into an enemy deals impact damage
    // per-target cooldown prevents the same enemy being multi-hit in one pass
    public class ForcefulImpactBehaviour : ItemBehaviour
    {
        [SerializeField] private float[] impactDamage = { 20f, 25f, 30f, 40f, 55f };
        [SerializeField] private float perTargetCooldown = 1.5f; // seconds before same target can be hit again

        // owner-side hit ids — stops us spamming the server RPC every frame during contact
        private readonly HashSet<ulong> _ownerCooldowns = new HashSet<ulong>();
        // server-side authoritative — prevents any exploit padding damage
        private readonly HashSet<ulong> _serverCooldowns = new HashSet<ulong>();

        protected override void OnInitialize()
        {
            // only the owner runs OnControllerColliderHit, so subscribe owner-side
            if (IsOwner)
                PlayerController.OnBodyContact += OnBodyContact;
        }

        protected override void OnTierChanged(int oldTier, int newTier) { }

        public override void OnRemoved()
        {
            PlayerController.OnBodyContact -= OnBodyContact;
            _ownerCooldowns.Clear();
            _serverCooldowns.Clear();
        }

        // called on the owner's machine when the CharacterController hits something during sprint/dodge
        private void OnBodyContact(PlayerController player, GameObject hitObject)
        {
            if (player != PlayerController) return;

            // must be a networked object to relay to server
            var netObj = hitObject.GetComponentInParent<NetworkObject>();
            if (netObj == null) return;

            // must be an enemy or boss, not world geometry
            if (hitObject.GetComponentInParent<Enemies.EnemyBase>() == null &&
                hitObject.GetComponentInParent<Boss.BossBase>() == null) return;

            // owner-side cooldown — skip if we recently hit this target
            if (_ownerCooldowns.Contains(netObj.NetworkObjectId)) return;
            _ownerCooldowns.Add(netObj.NetworkObjectId);
            StartCoroutine(OwnerCooldownExpire(netObj.NetworkObjectId));

            // relay to server to apply damage authoritatively
            manager.ForcefulImpactContactServerRpc(netObj.NetworkObjectId);
        }

        // called by ItemBehaviourManager RPC on the server
        public void OnServerBodyContact(GameObject target)
        {
            var netObj = target.GetComponentInParent<NetworkObject>();
            ulong id = netObj != null ? netObj.NetworkObjectId : (ulong)(uint)target.GetInstanceID();

            // server-side cooldown — authoritative guard against any client tricks
            if (_serverCooldowns.Contains(id)) return;
            _serverCooldowns.Add(id);
            StartCoroutine(ServerCooldownExpire(id));

            var damageable = target.GetComponentInParent<IDamageable>();
            if (damageable == null) return;

            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);
            int dmg = PlayerStats != null
                ? PlayerStats.ApplyBeforeDamageMultiplier(Mathf.RoundToInt(impactDamage[idx]), target)
                : Mathf.RoundToInt(impactDamage[idx]);

            // spiritual well cross-synergy: if the player has mana, boost this hit and spend mana
            var spiritWell = manager.GetItemBehaviour<SpiritualWellBehaviour>();
            dmg = spiritWell != null ? spiritWell.ApplyManaBonus(dmg) : dmg;

            damageable.TakeDamage(dmg);
        }

        private IEnumerator OwnerCooldownExpire(ulong id)
        {
            yield return new WaitForSeconds(perTargetCooldown);
            _ownerCooldowns.Remove(id);
        }

        private IEnumerator ServerCooldownExpire(ulong id)
        {
            yield return new WaitForSeconds(perTargetCooldown);
            _serverCooldowns.Remove(id);
        }

        public override object[] GetFormatValues(int tier)
        {
            int idx = Mathf.Clamp(tier - 1, 0, 4);
            return new object[]
            {
                Mathf.RoundToInt(impactDamage[idx]),
                perTargetCooldown
            };
        }
    }
}
