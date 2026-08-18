using UnityEngine;

namespace Category5
{
    // elementalist thunder e ability - 360 degree aoe around maia that damages, pushes back, and stuns enemies
    // designed as an escape/crowd control tool when surrounded
    public class ElementalistE_Thunder : AbilityBase
    {
        [Header("thunder aoe settings")]
        [SerializeField] private float arcRadius = 5f;
        [SerializeField] private float knockbackForce = 15f;
        [SerializeField] private float stunDuration = 1f;
        [SerializeField] private float stunDelay = 0.12f;
        [SerializeField] private LayerMask enemyLayers = 1 << 6;

        // exposes the aoe radius for the aim indicator ring
        public float ArcRadius => arcRadius;

        // events for vfx/sfx hooks (position, radius)
        public static event System.Action<Vector3, float> OnThunderArcExecute;

        // public method to invoke event from PlayerAbilityManager rpcs
        public static void InvokeThunderArcExecute(Vector3 position, float radius)
        {
            OnThunderArcExecute?.Invoke(position, radius);
        }

        // plays a cast animation and fires on the CastImpact animation event
        public override bool HasCastAnimation => true;

        // can be held to aim (shows the ground ring indicator before firing)
        public override bool CanHoldToAim => true;

        // thunder is a 360 aoe - no aim direction needed, but the aim indicator uses the ground center
        public override Vector3 GetAimDirection(Vector3 spawnPos)
        {
            return playerController != null ? playerController.transform.forward : Vector3.forward;
        }

        public override void Execute()
        {
            // position at ground level under the player (the aoe is centered on the player)
            Vector3 position = playerController.transform.position + Vector3.up * 0.1f;

            // notify listeners for vfx/sfx (no forward needed since it's a circle)
            OnThunderArcExecute?.Invoke(position, arcRadius);
            SpawnVfx(position);
            PlayAudio(position);

            // send coefficient to server, server calculates damage
            float coefficient = abilityData.damageCoefficient;

            // request server to execute the aoe damage
            abilityManager.ExecuteThunderArcServerRpc(
                position, coefficient, arcRadius, knockbackForce, stunDuration, stunDelay, enemyLayers.value
            );
        }

        // gizmos showing the aoe circle on the ground
        private void OnDrawGizmosSelected()
        {
            if (playerController == null) return;

            Vector3 origin = playerController.transform.position + Vector3.up * 0.1f;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(origin, arcRadius);

            // draw ground ring (flat circle)
            Gizmos.color = Color.blue;
            int segments = 24;
            float angleStep = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;
                Vector3 p1 = origin + (Quaternion.Euler(0, angle1, 0) * Vector3.forward) * arcRadius;
                Vector3 p2 = origin + (Quaternion.Euler(0, angle2, 0) * Vector3.forward) * arcRadius;
                Gizmos.DrawLine(p1, p2);
            }
        }
    }
}
