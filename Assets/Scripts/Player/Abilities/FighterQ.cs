using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Audio;
using Category5.Enemies;
using Category5.Player;

namespace Category5
{
    // fighter q ability - overhead smash that deals aoe damage and stuns nearby regular enemies
    public class FighterQ : AbilityBase
    {
        [Header("Configuration")]
        [SerializeField] private float forwardDistance = 2.5f; // distance in front of player to target
        [SerializeField] private float aoeRadius = 5f; // damage radius
        [SerializeField] private float stunDuration = 0.75f; // stun duration
        [SerializeField] private LayerMask enemyLayers = 1 << 6; // default to layer 6 (enemy layer)
        
        // events for vfx/sfx
        public static event System.Action<Vector3, int> OnSmashExecute;
        public static event System.Action<Vector3> OnSmashHit;
        
        // cached for gizmos
        private Vector3 lastTargetPosition;
        
        // public methods to invoke events (called from PlayerAbilityManager)
        public static void InvokeSmashExecute(Vector3 position, int enemiesHit)
        {
            OnSmashExecute?.Invoke(position, enemiesHit);
        }
        
        public static void InvokeSmashHit(Vector3 position)
        {
            OnSmashHit?.Invoke(position);
        }
        
        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            
            // check ability manager cooldown
            if (abilityManager.ability1Cooldown.Value > 0) return false;
            
            return true;
        }

        public override void Execute()
        {
            if (!CanUse()) return;
            
            // calculate ground target position in front of player
            Vector3 targetPosition = GetGroundTargetPosition(forwardDistance);
            lastTargetPosition = targetPosition;
            
            // apply damage modifier from player inventory
            int adjustedDamage = playerInventory.CalculateDamage((int)abilityData.baseDamage);
            
            // request server to execute through ability manager (networkbehaviour)
            abilityManager.ExecuteFighterQSmashServerRpc(targetPosition, adjustedDamage, aoeRadius, stunDuration, enemyLayers.value);
        }

        // calculate ground target position in front of player
        private Vector3 GetGroundTargetPosition(float distance)
        {
            // start from player position
            Vector3 startPos = playerController.transform.position;
            
            // move forward by distance
            Vector3 forwardPos = startPos + playerController.transform.forward * distance;
            
            // raycast down to find ground (start slightly above to ensure we hit ground)
            if (Physics.Raycast(forwardPos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 10f))
            {
                return groundHit.point;
            }
            
            // fallback: use forward position at player's Y level
            forwardPos.y = startPos.y;
            return forwardPos;
        }
        
        // gizmos for debugging impact area
        private void OnDrawGizmosSelected()
        {
            if (playerController == null) return;
            
            // draw forward direction ray
            Gizmos.color = Color.yellow;
            Vector3 startPos = playerController.transform.position;
            Vector3 forwardEnd = startPos + playerController.transform.forward * forwardDistance;
            Gizmos.DrawLine(startPos, forwardEnd);
            
            // draw ground target point
            Vector3 targetPos = GetGroundTargetPosition(forwardDistance);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(targetPos, 0.3f);
            
            // draw damage radius
            Gizmos.color = Color.red;
            DrawWireSphere(targetPos, aoeRadius);
        }
        
        // helper to draw wire sphere with more segments
        private void DrawWireSphere(Vector3 center, float radius)
        {
            int segments = 16;
            float angleStep = 360f / segments;
            
            // draw horizontal circle
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep * Mathf.Deg2Rad;
                float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;
                
                Vector3 point1 = center + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
                Vector3 point2 = center + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);
                
                Gizmos.DrawLine(point1, point2);
            }
            
            // draw vertical circles (x-y and z-y planes)
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep * Mathf.Deg2Rad;
                float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;
                
                // x-y plane
                Vector3 point1a = center + new Vector3(Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius, 0);
                Vector3 point2a = center + new Vector3(Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius, 0);
                Gizmos.DrawLine(point1a, point2a);
                
                // z-y plane
                Vector3 point1b = center + new Vector3(0, Mathf.Sin(angle1) * radius, Mathf.Cos(angle1) * radius);
                Vector3 point2b = center + new Vector3(0, Mathf.Sin(angle2) * radius, Mathf.Cos(angle2) * radius);
                Gizmos.DrawLine(point1b, point2b);
            }
        }

        // note: cooldowns are managed by PlayerAbilityManager
        // note: actual damage execution happens in PlayerAbilityManager.ExecuteFighterQSmashServerRpc
    }
}
