using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Audio;
using Category5.Player;

namespace Category5.Enemies
{
    // basic melee enemy that chases and attacks players
    // all attack timing and damage is handled in EnemyBase using EnemyAttackData
    // this class only adds the taunt system so FighterR can redirect this enemy
    public class BasicEnemy : EnemyBase, ICanBeTaunted
    {
        // taunt system
        private Transform tauntSourceTransform;
        private float tauntEndTime;

        // =====================================
        // attack implementation
        // =====================================

        protected override void ExecuteAttack()
        {
            // fire the attack event so audio and vfx systems can react
            // base already set stateTimer, _damageDelayTimer, and _currentAttack before calling this
            NotifyAttackClientRpc(transform.position);
        }

        [ClientRpc]
        private void NotifyAttackClientRpc(Vector3 position)
        {
            EnemyEvents.InvokeAttack(position, elementType);
        }

        protected override void OnAttackUpdate()
        {
            // rotate toward target during attack
            RotateTowardTarget();

            base.OnAttackUpdate();
        }

        // =====================================
        // gizmos
        // =====================================

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // draw attack direction
            if (currentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, transform.position + transform.forward * attackRange);
            }
        }

        // =====================================
        // taunt system (ICanBeTaunted)
        // =====================================

        public void SetTauntTarget(Transform target)
        {
            tauntSourceTransform = target;
            tauntEndTime = Time.time + 4f; // taunt for 4 seconds
        }

        public void ClearTauntTarget()
        {
            tauntSourceTransform = null;
        }

        // override GetEffectiveTarget to prioritize taunt
        protected override Transform GetEffectiveTarget()
        {
            // if taunted and taunt is still active, return taunt source
            if (tauntSourceTransform != null && Time.time < tauntEndTime)
            {
                return tauntSourceTransform;
            }

            // otherwise return the normal target
            return currentTarget;
        }
    }
}
