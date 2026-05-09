using UnityEngine;
using Unity.Netcode;
using Category5.Enemies;
using Category5.Player;

namespace Category5.Items
{
    // Networked prefab spawned at enemy spawner positions when all enemies are defeated.
    // Server-authoritative: only the server processes logic.
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

            // verify network object is present (guaranteed by RequireComponent)
            if (NetworkObject == null)
            {
                Debug.LogError("ItemDrop: NetworkObject is missing! Prefab must have NetworkObject.", this);
            }
        }

        /// <summary>
        /// Sets the spawner that spawned this item drop, used for per-spawner collection tracking.
        /// Called server-side only after spawning.
        /// </summary>
        public void SetSpawner(EnemySpawner spawner)
        {
            _spawner = spawner;
        }

        // =====================================
        // collection — server-authoritative
        // =====================================

        private void OnTriggerEnter(Collider other)
        {
            // Only the server processes collection. Clients do not send RPCs;
            // the server already runs physics and will detect the trigger.
            if (!IsServer) return;
            if (!IsSpawned) return;

            // Filter: only players can collect items.
            // We check for PlayerController to distinguish players from enemies, pets, etc.
            PlayerController playerController = other.GetComponentInParent<PlayerController>();
            if (playerController == null) return;

            NetworkObject playerNetObj = playerController.NetworkObject;
            if (playerNetObj == null) return;

            TryCollect(playerNetObj.OwnerClientId);
        }

        /// <summary>
        /// Validates collection server-side via the spawner's tracking.
        /// Uses MarkCollected's return value as an atomic check-and-set to prevent
        /// TOCTOU race conditions when multiple triggers fire simultaneously.
        /// If not yet collected, marks it and proceeds (Story 003 will add item selection UI trigger).
        /// </summary>
        private void TryCollect(ulong clientId)
        {
            if (_spawner == null) return;

            // Atomic check-and-set: returns true only if clientId was newly added.
            // This eliminates the TOCTOU race between HasPlayerCollected and MarkCollected.
            if (!_spawner.MarkCollected(clientId)) return;

            // Story 003: trigger per-player item selection UI here
        }
    }
}
