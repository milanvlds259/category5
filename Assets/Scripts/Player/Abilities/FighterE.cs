using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Audio;
using Category5.Enemies;
using Category5.Boss;
using Category5.Player;
using Category5.Player.Abilities;
using Category5.PowerUps;
using System.Linq;

namespace Category5
{
    // fighter e ability - grappling hook that pulls player toward boss or pulls enemy toward player
    public class FighterE : AbilityBase
    {
        [Header("hook settings")]
        [SerializeField] private GameObject hookProjectilePrefab;
        [SerializeField] private float hookSpeed = 20f;
        [SerializeField] private float hookLifetime = 3f;
        
        [Header("grapple settings")]
        [SerializeField] private float grapplePullForce = 15f;
        [SerializeField] private float playerPullSpeed = 15f; // speed when pulling player toward boss
        
        private bool isGrappling;
        private Transform grappleTarget; // the boss being grappled to
        private CharacterController playerCharacterController;
        
        // public properties for external access
        public bool IsGrappling => isGrappling;
        public Transform GrappleTarget => grappleTarget;
        
        // events for vfx/sfx
        public static event System.Action<Vector3> OnHookFire;
        public static event System.Action<Vector3> OnHookHit;
        public static event System.Action<Vector3, Vector3> OnPlayerPulled; // start pos, end pos
        
        public override void Initialize(PlayerController player, PlayerStats stats, PlayerAbilityManager manager)
        {
            base.Initialize(player, stats, manager);
            
            // cache character controller reference
            if (playerController != null)
            {
                playerCharacterController = playerController.GetComponent<CharacterController>();
            }
        }
        
        private void Update()
        {
            // continuously pull player toward grapple target
            if (isGrappling && grappleTarget != null && playerController != null)
            {
                Vector3 playerPos = playerController.transform.position;
                Vector3 targetPos = grappleTarget.position;
                
                Debug.Log($"FighterE Update: Grappling - distance to target: {Vector3.Distance(playerPos, targetPos):F2}");
                
                // pull player toward target
                Vector3 pullDirection = (targetPos - playerPos).normalized;
                float pullAmount = playerPullSpeed * Time.deltaTime;
                
                if (playerCharacterController != null)
                {
                    playerCharacterController.Move(pullDirection * pullAmount);
                }
                else
                {
                    playerController.transform.position += pullDirection * pullAmount;
                }
            }
            else if (isGrappling && grappleTarget == null)
            {
                // target was destroyed, stop grappling
                Debug.Log("FighterE Update: Target null, stopping grapple");
                StopGrapple();
            }
        }
        
        // public method that can be called by PlayerController on collision
        public void OnPlayerCollision(GameObject hitObject)
        {
            if (!isGrappling) return;
            
            Debug.Log($"FighterE: OnPlayerCollision called with {hitObject.name}");
            
            // check if we hit the grapple target (boss)
            bool hitTarget = false;
            if (grappleTarget != null)
            {
                var boss = hitObject.GetComponentInParent<BossBase>();
                if (boss != null && boss.transform == grappleTarget)
                {
                    hitTarget = true;
                    Debug.Log("FighterE: Player collided with grapple target (boss)!");
                }
            }
            
            // stop grapple on any collision (target or obstacle)
            if (hitTarget)
            {
                Debug.Log("FighterE: Reached target, stopping grapple");
            }
            else
            {
                Debug.Log($"FighterE: Collided with obstacle ({hitObject.name}), stopping grapple");
            }
            
            StopGrapple();
        }
        
        private void StopGrapple()
        {
            if (!isGrappling) return;
            
            Debug.Log("FighterE: Stopping grapple");
            isGrappling = false;
            grappleTarget = null;
            
            // notify clients that pull finished
            if (IsServer)
            {
                TriggerPlayerPullFinishedClientRpc();
            }
        }
        
        [ClientRpc]
        private void TriggerPlayerPullFinishedClientRpc()
        {
            // hook for vfx/sfx when grapple finishes
        }
        
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
            
            // fire hook from player position
            Vector3 spawnPos = playerController.transform.position;
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
            
            // initialize the hook projectile with owner info before spawning
            var hookProjectile = hookObj.GetComponent<HookProjectile>();
            if (hookProjectile != null)
            {
                // spawn first so NetworkObjectId is valid
                if (networkObj != null)
                {
                    networkObj.Spawn();
                }
                
                // then initialize with owner info and settings
                // use playerController's NetworkObjectId since FighterE is on the same gameobject
                hookProjectile.Initialize(playerController.NetworkObjectId, fireDirection, hookSpeed, hookLifetime);
            }
            else
            {
                // fallback for old prefab without HookProjectile component
                Debug.LogWarning("FighterE: Hook prefab missing HookProjectile component, using legacy behavior");
                if (networkObj != null)
                {
                    networkObj.Spawn();
                }
                
                var rb = hookObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = fireDirection * hookSpeed;
                }
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
        
        // called by HookProjectile when it hits an enemy or boss (server-side)
        public void OnHookHitTargetFromProjectile(Vector3 hitPosition, ulong targetNetworkObjectId, bool isBoss)
        {
            Debug.Log($"FighterE: OnHookHitTargetFromProjectile called. Position: {hitPosition}, Target ID: {targetNetworkObjectId}, IsBoss: {isBoss}, IsServer: {IsServer}");
            
            if (!IsServer) return;
            
            // fire event on clients
            TriggerHookHitClientRpc(hitPosition);
            
            if (isBoss)
            {
                Debug.Log("FighterE: Pulling player toward boss");
                // pull player toward boss
                PullPlayerToBoss(targetNetworkObjectId);
            }
            else
            {
                Debug.Log("FighterE: Pulling enemy toward player");
                // pull enemy toward player
                PullEnemy(targetNetworkObjectId);
            }
        }
        
        [ClientRpc]
        private void TriggerHookHitClientRpc(Vector3 hitPosition)
        {
            OnHookHit?.Invoke(hitPosition);
        }
        
        // legacy method kept for backwards compatibility
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
        
        // pull enemy toward player (server-side)
        private void PullEnemy(ulong enemyNetworkId)
        {
            if (!IsServer) return;
            
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkId, out var networkObject))
            {
                return;
            }
            
            var enemy = networkObject.GetComponent<EnemyBase>();
            if (enemy != null && !enemy.IsDead)
            {
                // start continuous grapple pull toward player
                Vector3 playerPosition = playerController.transform.position;
                Debug.Log($"FighterE: Starting enemy grapple pull from {enemy.transform.position} to {playerPosition}");
                enemy.StartGrapple(playerPosition, grapplePullForce);
            }
        }
        
        // pull player toward boss (server-side)
        private void PullPlayerToBoss(ulong bossNetworkId)
        {
            if (!IsServer) return;
            
            // check if player can be pulled (not dead, not already grappling)
            if (playerController.IsDead.Value || isGrappling)
            {
                Debug.Log($"FighterE: Cannot pull player - IsDead: {playerController.IsDead.Value}, isGrappling: {isGrappling}");
                return;
            }
            
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(bossNetworkId, out var networkObject))
            {
                Debug.LogError("FighterE: Could not find boss NetworkObject");
                return;
            }
            
            var boss = networkObject.GetComponent<BossBase>();
            if (boss == null)
            {
                Debug.LogError("FighterE: NetworkObject does not have BossBase component");
                return;
            }
            
            // start continuous grapple
            Debug.Log($"FighterE: Starting continuous grapple to {boss.gameObject.name}");
            isGrappling = true;
            grappleTarget = boss.transform;
            
            // cache character controller if not already done
            if (playerCharacterController == null)
            {
                playerCharacterController = playerController.GetComponent<CharacterController>();
            }
            
            // notify clients that pull started
            Vector3 startPos = playerController.transform.position;
            TriggerPlayerPullStartedClientRpc(startPos, boss.transform.position);
        }
        
        [ClientRpc]
        private void TriggerPlayerPullStartedClientRpc(Vector3 startPos, Vector3 targetPos)
        {
            OnPlayerPulled?.Invoke(startPos, targetPos);
            
            if (IsOwner && HitFeedbackManager.Instance != null)
            {
                HitFeedbackManager.Instance.TriggerLightHit(startPos);
            }
        }
        
        [Rpc(SendTo.Server)]
        private void PullEnemyServerRpc(ulong enemyNetworkId)
        {
            PullEnemy(enemyNetworkId);
        }

        // note: cooldowns are managed by PlayerAbilityManager
    }
}
