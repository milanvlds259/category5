using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Player;
using Category5.Audio;
using NUnit.Framework;

namespace Category5.Enemies
{
    public class SpawnerEnemy : EnemyBase
    {

        [Header("Spawner Enemy Settings")]
        [SerializeField] private GameObject spawnedEnemyPrefab;
        [SerializeField] private int enemiesPerSpawn = 3;
        [SerializeField] private float spawnRadius = 2f;
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

            SpawnWave();

            // base.ExecuteAttack() is empty in EnemyBase, so we just call it or skip
            base.ExecuteAttack();
        }

        [ClientRpc]
        private void NotifyAttackClientRpc(Vector3 position)
        {
            EnemyEvents.InvokeAttack(position, elementType);
        }


        private void SpawnWave()
        {

            
            if(spawnedEnemyPrefab == null)
            {
                Debug.LogWarning("SpawnerEnemy: No spawnedEnemyPrefab assigned.");
                return;
            }

            for(int i=0; i < enemiesPerSpawn; i++)
            {
                Vector3 spawnPos = transform.position + Random.insideUnitSphere * spawnRadius;
                spawnPos.y = transform.position.y; // keep on same height
                GameObject spawnedEnemy = Instantiate(spawnedEnemyPrefab, spawnPos, Quaternion.identity);
                NetworkObject netObj = spawnedEnemy.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                }
            }
        }

       

    }
}


