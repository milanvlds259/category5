using UnityEngine;

namespace Category5.Interactions
{
    public abstract class HubInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string interactPrompt = "Interact";
        [SerializeField] private float interactRange = 3f;
        [SerializeField] private float lookAngleThreshold = 45f; // Threshold in degrees

        public virtual string GetInteractPrompt() => $"[F] {interactPrompt}";

        public abstract void Interact(GameObject player);

        public virtual bool CanInteract(GameObject player)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist > interactRange) return false;

            // Check if player is looking at the interactable
            Vector3 directionToInteractable = (transform.position - player.transform.position).normalized;
            directionToInteractable.y = 0; // Only check horizontal alignment
            
            Vector3 playerForward = player.transform.forward;
            playerForward.y = 0;
            
            float angle = Vector3.Angle(playerForward, directionToInteractable);

            return angle <= lookAngleThreshold;
        }

        protected virtual void Start()
        {
            // Ensure there is a trigger collider
            var colliders = GetComponents<SphereCollider>();
            bool hasTrigger = false;
            foreach (var col in colliders)
            {
                if (col.isTrigger)
                {
                    hasTrigger = true;
                    break;
                }
            }

            if (!hasTrigger)
            {
                var trigger = gameObject.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = interactRange;
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRange);
            
            // Draw look cone
            Gizmos.color = new Color(1, 1, 0, 0.2f);
            Vector3 forward = transform.forward; // This isn't quite right for the gizmo but helps visualize
        }
    }
}
