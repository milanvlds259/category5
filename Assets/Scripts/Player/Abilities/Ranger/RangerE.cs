using UnityEngine;
using Category5.Player;

namespace Category5
{
    // ranger e ability shoots an arrow that spawns a dot zone on impact
    public class RangerE : AbilityBase
    {
        [Header("ranger e settings")]
        [SerializeField] private float zoneRadius = 5f;
        [SerializeField] private float zoneDuration = 6f;
        [SerializeField] private float damageTickInterval = 0.5f;
        [SerializeField] private float slowMultiplier = 0.6f;
        [SerializeField] private float arrowSpeed = 20f;
        [SerializeField] private float arrowLifetime = 5f;
        
        private PlayerCombat playerCombat;
        
        public override void Initialize(PlayerController player, PlayerStats stats, PlayerAbilityManager manager)
        {
            base.Initialize(player, stats, manager);
            playerCombat = player.GetComponent<PlayerCombat>();
        }
        
        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            if (playerCombat == null) return false;
            if (playerCombat.CurrentCombatClass != CombatClass.Ranged) return false;
            
            return true;
        }
        
        public override void Execute()
        {
            Vector3 spawnPos = playerController.transform.position + Vector3.up * 1.5f + playerController.transform.forward * 0.5f;
            Vector3 direction = GetAimDirection();

            SpawnVfx(spawnPos);
            PlayAudio(spawnPos);

            abilityManager.SpawnRangerEArrowServerRpc(
                spawnPos,
                direction,
                abilityData.damageCoefficient,
                arrowSpeed,
                arrowLifetime,
                zoneRadius,
                zoneDuration,
                damageTickInterval,
                slowMultiplier
            );
        }
        
        private Vector3 GetAimDirection()
        {
            if (Camera.main == null)
            {
                return playerController.transform.forward;
            }
            
            Ray aimRay = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 spawnPos = playerController.transform.position + Vector3.up * 1.5f + playerController.transform.forward * 0.5f;
            
            if (Physics.Raycast(aimRay, out RaycastHit hit, 100f))
            {
                return (hit.point - spawnPos).normalized;
            }
            return (aimRay.GetPoint(100f) - spawnPos).normalized;
        }
    }
}
