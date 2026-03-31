using UnityEngine;
using Unity.Netcode;
using Category5.Player;
using Category5.Items;

namespace Category5
{
    // ranger r ability - instant piercing arrow that only stops on bosses
    public class RangerR : AbilityBase
    {
        [Header("Critshot Settings")]
        [SerializeField] private float damageMultiplier = 3f; // 3x damage
        [SerializeField] private ProjectileData arrowData;
        [SerializeField] private Transform projectileSpawnPoint;
        
        private PlayerCombat playerCombat;
        private PlayerController playerControllerRef;
        
        public override void Initialize(PlayerController player, PlayerStats stats, PlayerAbilityManager manager)
        {
            base.Initialize(player, stats, manager);
            playerCombat = player.GetComponent<PlayerCombat>();
            playerControllerRef = player;
            
            // try to find projectile spawn point if not assigned
            if (projectileSpawnPoint == null)
            {
                projectileSpawnPoint = player.transform.Find("ProjectileSpawnPoint");
            }
        }
        
        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            if (playerCombat == null) return false;
            if (playerCombat.CurrentCombatClass != CombatClass.Ranged) return false;
            if (arrowData == null)
            {
                Debug.LogWarning("RangerR: No arrow data assigned!");
                return false;
            }
            
            return true;
        }
        
        public override void Execute()
        {
            // Debug.Log("RangerR.Execute() called");
            
            // get spawn position
            Vector3 spawnPos = projectileSpawnPoint != null 
                ? projectileSpawnPoint.position 
                : playerControllerRef.transform.position + playerControllerRef.transform.forward * 0.5f + Vector3.up * 1.5f;
            
            // apply forward offset
            spawnPos += (projectileSpawnPoint != null ? projectileSpawnPoint.forward : playerControllerRef.transform.forward) * arrowData.SpawnForwardOffset;
            
            // get aim direction from camera
            Vector3 direction = GetAimDirection(spawnPos);
            
            // send request to ability manager to spawn piercing arrow on server
            // pass all necessary data directly to avoid dependency on ability state
            abilityManager.RequestSpawnNetworkProjectileServerRpc(spawnPos, direction, damageMultiplier);
            
            // play vfx and audio directly (client-side)
            SpawnVfx(spawnPos);
            PlayAudio(spawnPos);
        }

        
        private Vector3 GetAimDirection(Vector3 spawnPos)
        {
            if (Camera.main == null)
            {
                return playerControllerRef.transform.forward;
            }
            
            // raycast from screen center (same logic as normal arrows)
            Ray aimRay = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float maxRange = 1000f; // very long range for critshot
            
            if (Physics.Raycast(aimRay, out RaycastHit hit, maxRange))
            {
                return (hit.point - spawnPos).normalized;
            }
            else
            {
                return (aimRay.GetPoint(maxRange) - spawnPos).normalized;
            }
        }

        // getters for PlayerAbilityManager RPC
        public ProjectileData GetArrowData() => arrowData;
        public float GetDamageMultiplier() => damageMultiplier;
    }
}
