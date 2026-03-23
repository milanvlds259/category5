using UnityEngine;
using Category5.Boss;
using Category5.Player;
using Category5.Player.Abilities;

namespace Category5
{
    // fighter e - magnetic grapple
    // fires a hook projectile: small enemies are dragged to the player; bosses drag the player to them
    // networking follows the modern pattern: all rpcs delegated to PlayerAbilityManager
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

        // public invoke helpers called from PlayerAbilityManager clientrpcs
        public static void OnHookFireInvoke(Vector3 pos) => OnHookFire?.Invoke(pos);
        public static void OnHookHitInvoke(Vector3 pos) => OnHookHit?.Invoke(pos);

        public override void Initialize(PlayerController player, PlayerStats stats, PlayerAbilityManager manager)
        {
            base.Initialize(player, stats, manager);
            playerCharacterController = player.GetComponent<CharacterController>();
        }

        private void Update()
        {
            // owner drives the pull on the client that owns the player object
            if (!IsOwner) return;

            if (isGrappling && grappleTarget == null)
            {
                StopGrapple();
                return;
            }

            if (!isGrappling || grappleTarget == null) return;

            Vector3 pullDirection = (grappleTarget.position - playerController.transform.position).normalized;
            float pullAmount = playerPullSpeed * Time.deltaTime;

            if (playerCharacterController != null)
                playerCharacterController.Move(pullDirection * pullAmount);
            else
                playerController.transform.position += pullDirection * pullAmount;
        }

        // called by PlayerController.OnControllerColliderHit when the player physically touches something while grappling
        public void OnPlayerCollision(GameObject hitObject)
        {
            if (!isGrappling) return;
            StopGrapple();
        }

        // start pulling the player toward a boss (called by PlayerAbilityManager on server + routed to owner)
        public void StartGrapplePull(Transform bossTransform)
        {
            isGrappling = true;
            grappleTarget = bossTransform;
            OnPlayerPulled?.Invoke(playerController.transform.position, bossTransform.position);
        }

        public void StopGrapple()
        {
            if (!isGrappling) return;
            isGrappling = false;
            grappleTarget = null;
        }

        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            if (isGrappling) return false;
            if (abilityManager.ability2Cooldown.Value > 0) return false;

            if (hookProjectilePrefab == null)
            {
                Debug.LogError("FighterE: hook projectile prefab not assigned");
                return false;
            }

            return true;
        }

        public override void Execute()
        {
            if (!CanUse()) return;

            Vector3 spawnPos = playerController.transform.position + playerController.transform.forward * 0.5f + Vector3.up * 1f;
            Vector3 aimDir = GetCameraAimDirection(spawnPos);

            SpawnVfx(spawnPos);
            PlayAudio(spawnPos);
            OnHookFire?.Invoke(spawnPos);

            // read tunable data locally (owner has abilities instantiated) and pass to server via rpc
            abilityManager.FireMagneticGrappleServerRpc(spawnPos, aimDir, hookSpeed, hookLifetime, grapplePullForce, hookProjectilePrefab.name);
        }

        private Vector3 GetCameraAimDirection(Vector3 spawnPos)
        {
            if (Camera.main == null) return playerController.transform.forward;

            Ray aimRay = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(aimRay, out RaycastHit hit, 100f))
                return (hit.point - spawnPos).normalized;

            return (aimRay.GetPoint(100f) - spawnPos).normalized;
        }

        // note: cooldowns managed by PlayerAbilityManager
    }
}
