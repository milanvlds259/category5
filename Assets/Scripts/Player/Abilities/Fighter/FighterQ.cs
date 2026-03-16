using UnityEngine;
using Category5.Player;

namespace Category5
{
    // fighter q - dual mode slam
    // grounded: box hitbox in front, launches enemies forward and up
    // airborne: sphere hitbox around player, launches enemies up and also launches the player
    public class FighterQ : AbilityBase
    {
        [Header("grounded slam")]
        [SerializeField] private float groundedBoxWidth = 3f;
        [SerializeField] private float groundedBoxHeight = 2.5f;
        [SerializeField] private float groundedBoxDepth = 3.5f;
        [SerializeField] private float groundedBoxForwardOffset = 2f;
        [SerializeField] private float groundedLaunchForceUp = 8f;
        [SerializeField] private float groundedLaunchForceForward = 6f;

        [Header("airborne slam")]
        [SerializeField] private float airborneRadius = 3.5f;
        [SerializeField] private Vector3 airborneOffset = new Vector3(0f, -0.5f, 0.5f);
        [SerializeField] private float airborneLaunchForceUp = 10f;
        [SerializeField] private float selfLaunchForceUp = 12f;
        [SerializeField] private float selfLaunchForceForward = 5f;

        [Header("shared")]
        [SerializeField] private LayerMask enemyLayers = 1 << 6;

        // events for vfx/sfx
        public static event System.Action<Vector3> OnSlamGrounded;
        public static event System.Action<Vector3> OnSlamAirborne;

        // called from PlayerAbilityManager clientrpcs
        public static void InvokeSlamGrounded(Vector3 position) => OnSlamGrounded?.Invoke(position);
        public static void InvokeSlamAirborne(Vector3 position) => OnSlamAirborne?.Invoke(position);

        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            if (abilityManager.ability1Cooldown.Value > 0) return false;
            return true;
        }

        public override void Execute()
        {
            if (!CanUse()) return;

            // calculate damage based on player stats
            int damage = playerStats.CalculateDamage((int)abilityData.baseDamage);
            Vector3 pos = playerController.transform.position;
            Vector3 forward = playerController.transform.forward;

            // execute slam based on grounded/airborne state
            if (playerController.IsGrounded)
            {
                abilityManager.ExecuteFighterQSlamGroundedServerRpc(
                    pos, forward, damage,
                    groundedBoxWidth, groundedBoxHeight, groundedBoxDepth, groundedBoxForwardOffset,
                    groundedLaunchForceUp, groundedLaunchForceForward, enemyLayers.value
                );
            }
            else
            {
                abilityManager.ExecuteFighterQSlamAirborneServerRpc(
                    pos, forward, damage,
                    airborneRadius, airborneOffset, airborneLaunchForceUp,
                    selfLaunchForceUp, selfLaunchForceForward, enemyLayers.value
                );
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (playerController == null) return;

            Vector3 pos = playerController.transform.position;
            Vector3 forward = playerController.transform.forward;
            Quaternion rot = Quaternion.LookRotation(forward);

            // grounded box (yellow)
            Gizmos.color = Color.yellow;
            Vector3 boxCenter = pos + forward * groundedBoxForwardOffset + Vector3.up * (groundedBoxHeight * 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(boxCenter, rot, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(groundedBoxWidth, groundedBoxHeight, groundedBoxDepth));
            Gizmos.matrix = Matrix4x4.identity;

            // airborne sphere (cyan)
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pos + airborneOffset, airborneRadius);
        }

        // note: cooldowns managed by PlayerAbilityManager
        // note: damage and launch executed in PlayerAbilityManager server rpcs
    }
}
