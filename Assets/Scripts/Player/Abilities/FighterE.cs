using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Audio;
using Category5.Enemies;
using Category5.Player;
using System.Linq;

namespace Category5
{
    // fighter e ability - grappling hook that pulls player toward boss or pulls enemy toward player
    public class FighterE : AbilityBase
    {
        [SerializeField] private GameObject hookProjectilePrefab;
        [SerializeField] private float grapplePullForce = 15f;
        [SerializeField] private Transform projectileSpawnPoint;
        
        private bool isGrappling;
        private float grappleTimer;
        
        // events for vfx/sfx
        public static event System.Action<Vector3> OnHookFire;
        public static event System.Action<Vector3> OnHookHit;
        
        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            if (isGrappling) return false; // can't use while grappling
            
            // check ability manager cooldown
            if (abilityManager.ability2Cooldown.Value > 0) return false;
            
            if (hookProjectilePrefab == null)
            {
                Debug.LogError("FighterE: Hook projectile prefab is not assigned!");
                return false;
            }
            
            return true;
        }

        public override void Execute()
        {
            if (!CanUse()) return;
            
            // fire hook from spawn point (or player position if not set)
            Vector3 spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : playerController.transform.position;
            Vector3 fireDirection = playerController.GetAimDirection();
            
            FireHookServerRpc(spawnPos, fireDirection);
        }

        [Rpc(SendTo.Server)]
        private void FireHookServerRpc(Vector3 spawnPosition, Vector3 fireDirection)
        {
            if (!IsServer) return;
            
            // spawn hook projectile
            var hookObj = Instantiate(hookProjectilePrefab, spawnPosition, Quaternion.identity);
            var networkObj = hookObj.GetComponent<NetworkObject>();
            
            if (networkObj != null)
            {
                networkObj.Spawn();
            }
            
            // add velocity
            var rb = hookObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = fireDirection * 20f; // hook speed 20
            }
            
            // notify clients for vfx/sfx
            TriggerHookFiredClientRpc(spawnPosition, fireDirection);
            
            // set cooldown
            abilityManager.ability2Cooldown.Value = abilityData.cooldownDuration;
        }
        
        [ClientRpc]
        private void TriggerHookFiredClientRpc(Vector3 position, Vector3 direction)
        {
            OnHookFire?.Invoke(position);
            
            if (IsOwner && HitFeedbackManager.Instance != null)
            {
                HitFeedbackManager.Instance.TriggerLightHit(position);
            }
        }
        
        // called by hook projectile when it hits something
        public void OnHookHitTarget(Vector3 hitPosition, GameObject hitTarget)
        {
            if (!IsOwner) return;
            
            OnHookHit?.Invoke(hitPosition);
            
            // determine if target is regular enemy
            var enemy = hitTarget.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                // pull enemy toward player
                PullEnemyServerRpc(enemy.NetworkObjectId);
            }
        }
        
        [Rpc(SendTo.Server)]
        private void PullEnemyServerRpc(ulong enemyNetworkId)
        {
            if (!IsServer) return;
            
            // find the enemy by network id
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkId, out var networkObject))
            {
                return;
            }
            
            var enemy = networkObject.GetComponent<EnemyBase>();
            if (enemy != null && !enemy.IsDead)
            {
                // apply force toward player
                Vector3 pullDirection = (playerController.transform.position - enemy.transform.position).normalized;
                enemy.ApplyKnockback(pullDirection * 15f); // grapple pull force
            }
        }

        // note: cooldowns are managed by PlayerAbilityManager
    }
}
