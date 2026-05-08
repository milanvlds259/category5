using UnityEngine;
using Unity.Netcode;

namespace Category5.Items
{

    // Networked prefab spawned at enemy spawner positions when all enemies are defeated.
    // Server-authoritative: only the server processes logic.
    // Collision detection and item selection are handled by neighbouring stories.

    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(SphereCollider))]
    public class ItemDrop : NetworkBehaviour
    {
        private void Awake()
        {
            // configure sphere collider as trigger for player collision detection
            var collider = GetComponent<SphereCollider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            // verify network object is present (should be guaranteed by RequireComponent)
            if (GetComponent<NetworkObject>() == null)
            {
                Debug.LogError("ItemDrop: NetworkObject component is missing! Prefab must have NetworkObject.", this);
            }
        }
    }
}
