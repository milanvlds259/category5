using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Player;
using Category5.Audio;

namespace Category5.Enemies
{
    // ranged enemy that maintains distance and fires projectiles
    public class RangedEnemy : EnemyBase, ICanBeTaunted
    {
        // taunt system
        private Transform tauntSourceTransform;
        private float tauntEndTime;

        // =====================================
        // movement implementation
        // =====================================

        protected override void OnChaseUpdate()
        {
            Transform effectiveTarget = GetEffectiveTarget();
            
            if (effectiveTarget == null)
            {
                TransitionToIdle();
                return;
            }
            
            // update currentTarget for distance calculations
            currentTarget = effectiveTarget;
            float distance = GetDistanceToTarget();
            
            // check leash range
            if (distance > leashRange)
            {
                currentTarget = null;
                currentTargetController = null;
                TransitionToIdle();
                return;
            }
            
            // check attack range
            // only attack if NOT too close (helpless at close range)
            float preferred = enemyData.preferredRangedDistance;
            if (distance <= attackRange && distance >= preferred * 0.4f && attackCooldownTimer <= 0f)
            {
                TransitionToAttack();
                return;
            }
            
            // ranged engagement logic: maintain preferred distance

            if (distance > preferred + 1f)
            {
                // too far: move toward target
                MoveTowardTarget();
            }
            else if (distance < preferred - 1f)
            {
                // too close: flee from target
                Vector3 fleeDir = (transform.position - effectiveTarget.position).normalized;
                MoveTowardPosition(transform.position + fleeDir * 3f);
            }
            else
            {
                // within sweet spot: stop moving
                MoveTowardPosition(transform.position);
            }

            // manual rotation toward target for aiming while chasing
            RotateTowardTarget();
        }

        // =====================================
        // attack implementation
        // =====================================

        protected override void ExecuteAttack()
        {
            // fire the attack event for audio/vfx
            NotifyAttackClientRpc(transform.position);
            
            // base.ExecuteAttack() is empty in EnemyBase, so we just call it or skip
            base.ExecuteAttack();
        }

        [ClientRpc]
        private void NotifyAttackClientRpc(Vector3 position)
        {
            EnemyEvents.InvokeAttack(position, elementType);
        }

        protected override void DealDamageToTarget()
        {
            if (_currentAttack != null && _currentAttack.projectilePrefab != null)
            {
                // spawn the projectile at the damage delay point
                Vector3 spawnPos = transform.position + transform.forward + Vector3.up;
                
                GameObject projObj = Instantiate(_currentAttack.projectilePrefab, spawnPos, transform.rotation);
                
                // CRITICAL: Initialize BEFORE Spawn so OnNetworkSpawn has the correct speed
                if (projObj.TryGetComponent<EnemyProjectile>(out var projectile))
                {
                    float multiplier = _currentAttack.damageMultiplier;
                    int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * multiplier * DamageOutputMultiplier));
                    projectile.Initialize(finalDamage, _currentAttack.projectileSpeed, 5f);
                }

                NetworkObject netObj = projObj.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                }
            }
            else
            {
                // if no projectile, fall back to base melee behavior
                base.DealDamageToTarget();
            }
        }

        protected override void OnAttackUpdate()
        {
            // rotate toward target during attack windup/execution
            RotateTowardTarget();
            base.OnAttackUpdate();
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
