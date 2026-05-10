using UnityEngine;
using Unity.Netcode;
using Category5.Enemies;

namespace Category5.Items
{
    /// <summary>
    /// Networked prefab spawned at enemy spawner positions when all enemies are defeated.
    /// Server-authoritative: only the server processes logic.
    /// Collision detection triggers item selection for the collecting player.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(SphereCollider))]
    public class ItemDrop : NetworkBehaviour
    {
        private EnemySpawner _spawner;
        private bool _hasDespawned;

        [Header("Timeout")]
        [Tooltip("Time in seconds before this ItemDrop despawns if not collected")]
        [SerializeField] private float _timeoutDuration = 60f;

        private float _timeoutTimer;

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
            if (NetworkObject == null)
            {
                Debug.LogError("ItemDrop: NetworkObject component is missing! Prefab must have NetworkObject.", this);
            }
        }

        private void Update()
        {
            if (!IsServer) return;
            UpdateTimer(Time.deltaTime);
        }

        private void UpdateTimer(float deltaTime)
        {
            if (_hasDespawned) return;
            _timeoutTimer += deltaTime;
            if (_timeoutTimer >= _timeoutDuration && NetworkObject != null)
            {
                _hasDespawned = true;
                NetworkObject.Despawn();
            }
        }

        /// <summary>
        /// Sets the spawner that spawned this item drop, used for per-spawner collection tracking.
        /// </summary>
        /// <param name="spawner">The EnemySpawner that owns this ItemDrop.</param>
        public void SetSpawner(EnemySpawner spawner)
        {
            _spawner = spawner;
        }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsSpawned) return;

        NetworkObject playerNetObj = other.GetComponentInParent<NetworkObject>();
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

            if (ItemManager.Instance == null)
            {
                Debug.LogError("ItemDrop: ItemManager instance not found. Ensure ItemManager is in the scene.", this);
                return;
            }

            ItemManager.Instance.RegisterIslandSelectionSpawner(clientId, _spawner);
            ItemManager.Instance.StartItemSelectionForPlayer(clientId);

            // despawn the drop after successful collection
            if (NetworkObject != null)
            {
                NetworkObject.Despawn();
            }
        }
    }
}
