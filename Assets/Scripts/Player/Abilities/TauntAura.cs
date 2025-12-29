using UnityEngine;
using Unity.Netcode;
using Category5.Enemies;
using Category5.Player;
using System.Collections.Generic;

namespace Category5
{
    // taunt aura that forces nearby enemies to target the owner
    // instantiated by FighterR ability, runs on owner only
    public class TauntAura : MonoBehaviour
    {
        [SerializeField] private float detectionRadius = 8f;
        [SerializeField] private float detectionInterval = 0.2f;
        
        private PlayerController playerOwner;
        private float detectionTimer;
        private List<EnemyBase> affectedEnemies = new List<EnemyBase>();
        
        public void Initialize(PlayerController owner)
        {
            playerOwner = owner;
            detectionTimer = 0f;
        }
        
        private void Update()
        {
            if (playerOwner == null) return;
            
            // follow player position (aura moves with player)
            transform.position = playerOwner.transform.position;
            
            detectionTimer -= Time.deltaTime;
            if (detectionTimer <= 0)
            {
                UpdateTauntTargets();
                detectionTimer = detectionInterval;
            }
        }
        
        private void UpdateTauntTargets()
        {
            // find all enemies in radius
            Collider[] hitColliders = Physics.OverlapSphere(playerOwner.transform.position, detectionRadius);
            
            // clear old list
            affectedEnemies.Clear();
            
            foreach (Collider collider in hitColliders)
            {
                var enemy = collider.GetComponent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    affectedEnemies.Add(enemy);
                    
                    // set this player as the forced target for the enemy
                    // enemies will prioritize this target while in the aura
                    SetEnemyTauntTarget(enemy, playerOwner.transform);
                }
            }
        }
        
        // helper to force an enemy to target this player
        private void SetEnemyTauntTarget(EnemyBase enemy, Transform targetTransform)
        {
            // enemies check a public field called tauntSourceTransform
            // this is a simple way to force targeting without modifying enemy AI too much
            if (enemy.TryGetComponent<BasicEnemy>(out var basicEnemy))
            {
                // we'll use reflection or add a public field in the enemy
                // for now, we'll use a simple interface pattern
                var tauntable = enemy as ICanBeTaunted;
                tauntable?.SetTauntTarget(targetTransform);
            }
        }
        
        private void OnDestroy()
        {
            // clear all taunt targets when aura ends
            foreach (var enemy in affectedEnemies)
            {
                if (enemy != null)
                {
                    var tauntable = enemy as ICanBeTaunted;
                    tauntable?.ClearTauntTarget();
                }
            }
            
            affectedEnemies.Clear();
        }
        
        // optional: visualize detection radius in editor
        private void OnDrawGizmosSelected()
        {
            if (playerOwner != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(playerOwner.transform.position, detectionRadius);
            }
        }
    }
    
    // interface for enemies that can be taunted
    public interface ICanBeTaunted
    {
        void SetTauntTarget(Transform target);
        void ClearTauntTarget();
    }
}
