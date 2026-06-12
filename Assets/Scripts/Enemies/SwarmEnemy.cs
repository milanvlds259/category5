using UnityEngine;
using Unity.Netcode;
using Category5.Audio;

namespace Category5.Enemies
{
    public class SwarmEnemy : EnemyBase
    {
        protected override void MoveTowardTarget()
        {
            if (currentTarget == null) return;

            // Use NetworkObjectId to assign a unique "slot" around the player
            // This ensures they attempt to surround the player rather than clumping at the front
            // We use modulo 8 to create 8 distinct slots (45 degrees apart)
            float angleOffset = (NetworkObjectId % 8) * 45f;
            
            // Calculate target position on a circle around the player
            // We target slightly inside the attack range to ensure we can hit
            float targetRadius = attackRange * 0.8f;
            Vector3 offset = Quaternion.Euler(0, angleOffset, 0) * Vector3.forward * targetRadius;
            Vector3 targetPos = currentTarget.position + offset;

            MoveTowardPosition(targetPos);
        }

        protected override void ExecuteAttack()
        {
            // Fire the attack event so audio and vfx systems can react
            NotifyAttackClientRpc(transform.position);
        }

        [ClientRpc]
        private void NotifyAttackClientRpc(Vector3 position)
        {
            EnemyEvents.InvokeAttack(position, elementType);
        }

        protected override void OnAttackUpdate()
        {
            // Rotate toward target during attack
            RotateTowardTarget();

            base.OnAttackUpdate();
        }
    }
}
