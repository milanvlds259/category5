using UnityEngine;
using Unity.Netcode;
using Category5.Enemies;

namespace Category5.Items
{

    // Networked prefab spawned at enemy spawner positions when all enemies are defeated.
    // Server-authoritative: only the server processes logic.fic
    // Collision detection and item selection are handled by neighbouring stories.

    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(SphereCollider))]
    public class ItemDrop : NetworkBehaviour
    {
        private EnemySpawner _spawner;

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

        // sets the spawner that spawned this item drop, used for per-spawner collection tracking

        public void SetSpawner(EnemySpawner spawner)
        {
            _spawner = spawner;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsSpawned) return;

            NetworkObject playerNetObj = other.GetComponent<NetworkObject>();
            if (playerNetObj == null) return;

            if (IsServer)
            {
                TryCollect(playerNetObj.OwnerClientId);
            }
            else if (IsClient)
            {
                RequestCollectServerRpc(playerNetObj.OwnerClientId);
            }
        }


        // ServerRpc called by clients to request collection of this item drop.
        // RequireOwnership is false because the ItemDrop is server-owned.
        [ServerRpc(RequireOwnership = false)]
        private void RequestCollectServerRpc(ulong clientId)
        {
            TryCollect(clientId);
        }


        // validates collection server-side via the spawner's tracking.
        // if not yet collected marks it and proceeds
        private void TryCollect(ulong clientId)
        {
            if (_spawner == null) return;
            if (_spawner.HasPlayerCollected(clientId)) return;

            _spawner.MarkCollected(clientId);
            // Story 003: trigger per-player item selection UI here
        }
    }
}
