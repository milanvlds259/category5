using System;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using Category5.Core;
using Category5.Enemies;
using Category5.Player.Van;
using Unity.AI.Navigation;

namespace Category5.Map
{
    // networked component attached to each hand-crafted room prefab
    // holds room identity, state, and spawner reference
    // RoomManager instantiates one room at a time and configures it at runtime
    [RequireComponent(typeof(NetworkObject))]
    public class StormRoom : NetworkBehaviour
    {
        [Header("entity spawn points (boss, etc.)")]
        [SerializeField] private Transform[] entitySpawnPoints;

        [Header("room reference")]
        [SerializeField] private EnemySpawner roomSpawner;

        // identity — set by RoomManager at runtime
        private int _roomIndex = -1;
        private int _eyewallIndex = -1;
        private RoomTaskType _taskType = RoomTaskType.EnemyWave;

        // server-authoritative state
        private NetworkVariable<StormRoomState> _currentState = new NetworkVariable<StormRoomState>(
            StormRoomState.Active,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // public accessors
        public int RoomIndex => _roomIndex;
        public int EyewallIndex => _eyewallIndex;
        public RoomTaskType TaskType => _taskType;
        public StormRoomState CurrentState => _currentState.Value;
        public Transform[] EntitySpawnPoints => entitySpawnPoints;
        public EnemySpawner RoomSpawner => roomSpawner;

        // events
        public static event Action<StormRoom> OnRoomCleared;

        public override void OnNetworkSpawn()
        {
            _currentState.OnValueChanged += OnStateChanged;
            HandleStateVisuals(_currentState.Value);
        }

        public override void OnNetworkDespawn()
        {
            _currentState.OnValueChanged -= OnStateChanged;
        }

        // configures this room's identity
        // called by RoomManager on the server after instantiation
        public void Configure(int roomIndex, int eyewallIndex, RoomTaskType taskType)
        {
            _roomIndex = roomIndex;
            _eyewallIndex = eyewallIndex;
            _taskType = taskType;

            // subscribe to spawner completion if we have one
            if (roomSpawner != null)
            {
                // spawner is controlled by van exit event, not auto-start
                roomSpawner.autoStartOnSpawn = false;
                EnemySpawner.OnAllEnemiesDefeated += HandleSpawnerCleared;
            }
            else
            {
                Debug.LogWarning($"[StormRoom] room {roomIndex} has no EnemySpawner assigned!");
            }
        }

        // =====================================
        // state management (server only)
        // =====================================

        // marks room as active — called when players enter
        public void SetActive()
        {
            if (!IsServer) return;
            if (_currentState.Value == StormRoomState.Cleared) return;

            _currentState.Value = StormRoomState.Active;

            // build the navmesh at runtime so enemies can pathfind on the room geometry
            NavMeshSurface navMesh = GetComponentInChildren<NavMeshSurface>();
            if (navMesh != null)
            {
                navMesh.BuildNavMesh();
            }
            else
            {
                Debug.LogWarning($"[StormRoom] room {_roomIndex} has no NavMeshSurface — enemies won't be able to navigate");
            }

            // auto-size spawner bounds to cover the room's walkable area
            if (roomSpawner != null)
            {
                roomSpawner.spawnBounds = CalculateRoomBounds();
            }

            // wait for a player to exit the van before spawning enemies
            VanExitController.OnPlayerExitedVan += HandlePlayerExitedVan;
        }

        private void HandlePlayerExitedVan()
        {
            VanExitController.OnPlayerExitedVan -= HandlePlayerExitedVan;

            if (!IsServer) return;
            if (_currentState.Value == StormRoomState.Cleared) return;

            if (roomSpawner != null)
            {
                roomSpawner.StartSpawning();
            }
        }

        // calculates the spawn bounds from the room's child renderers
        // so enemies spread across the entire room without manual configuration
        private Vector3 CalculateRoomBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[StormRoom] room {_roomIndex} has no renderers — using default bounds");
                return new Vector3(20f, 5f, 20f);
            }

            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combined.Encapsulate(renderers[i].bounds);
            }

            // use the horizontal extents as spawn bounds, center on the room
            Vector3 size = combined.size;
            Vector3 center = combined.center;

            // offset the spawner position so bounds are centered on the room geometry
            roomSpawner.transform.position = center;

            // add a small margin so enemies can spawn near edges
            return new Vector3(size.x + 2f, size.y + 2f, size.z + 2f);
        }

        // marks room as cleared — called when spawner completes
        public void SetCleared()
        {
            if (!IsServer) return;

            _currentState.Value = StormRoomState.Cleared;
        }

        // =====================================
        // spawner callback
        // =====================================

        private void HandleSpawnerCleared(EnemySpawner spawner)
        {
            if (!IsServer) return;

            SetCleared();
            OnRoomCleared?.Invoke(this);
        }

        private void OnDestroy()
        {
            VanExitController.OnPlayerExitedVan -= HandlePlayerExitedVan;

            if (roomSpawner != null)
            {
                EnemySpawner.OnAllEnemiesDefeated -= HandleSpawnerCleared;
            }
        }

        // =====================================
        // state change callbacks
        // =====================================

        private void OnStateChanged(StormRoomState previousValue, StormRoomState newValue)
        {
            HandleStateVisuals(newValue);
        }

        private void HandleStateVisuals(StormRoomState state)
        {
            // room is always active in the new flow — only the current room is instantiated
            // cleared rooms are despawned, so we don't need to dim them
            gameObject.SetActive(true);
        }

        // =====================================
        // queries
        // =====================================

        // returns an entity spawn point transform for a given index
        // used for boss spawning in the eye room, wraps around if more indices than points
        public Transform GetEntitySpawnPoint(int index)
        {
            if (entitySpawnPoints == null || entitySpawnPoints.Length == 0)
            {
                return transform;
            }
            return entitySpawnPoints[index % entitySpawnPoints.Length];
        }
    }
}
