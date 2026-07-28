using System;
using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Enemies;

namespace Category5.Map
{
    // networked component attached to each hand-crafted room prefab
    // holds room identity, state, and connection points
    // MapGenerator configures it at runtime after instantiation
    public class StormRoom : NetworkBehaviour
    {
        [Header("connection points (set from prefab children)")]
        [SerializeField] private Transform leftExitPoint;
        [SerializeField] private Transform rightExitPoint;
        [SerializeField] private Transform inwardExitPoint;
        [SerializeField] private Transform[] playerSpawnPoints;

        [Header("room reference")]
        [SerializeField] private EnemySpawner roomSpawner;
        [SerializeField] private TriggerVolume entryTrigger;

        // identity — set by MapGenerator at runtime
        private int _roomIndex = -1;
        private int _eyewallIndex = -1;
        private RoomTaskType _taskType = RoomTaskType.EnemyWave;

        // connection indices — set by MapGenerator
        private int _leftRoomIndex = -1;
        private int _rightRoomIndex = -1;
        private int _inwardRoomIndex = -1;

        // server-authoritative state
        private NetworkVariable<StormRoomState> _currentState = new NetworkVariable<StormRoomState>(
            StormRoomState.Hidden,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // networked task type so clients know what this room is
        private NetworkVariable<int> _taskTypeNet = new NetworkVariable<int>(
            (int)RoomTaskType.EnemyWave,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // public accessors
        public int RoomIndex => _roomIndex;
        public int EyewallIndex => _eyewallIndex;
        public RoomTaskType TaskType => _taskType;
        public StormRoomState CurrentState => _currentState.Value;
        public int LeftRoomIndex => _leftRoomIndex;
        public int RightRoomIndex => _rightRoomIndex;
        public int InwardRoomIndex => _inwardRoomIndex;
        public bool HasInwardPath => _inwardRoomIndex >= 0;
        public Transform LeftExitPoint => leftExitPoint;
        public Transform RightExitPoint => rightExitPoint;
        public Transform InwardExitPoint => inwardExitPoint;
        public Transform[] PlayerSpawnPoints => playerSpawnPoints;
        public EnemySpawner RoomSpawner => roomSpawner;

        // events — fire on all clients for UI updates
        public static event Action<StormRoom> OnRoomCleared;
        public static event Action<StormRoom> OnRoomActivated;
        public static event Action<StormRoom> OnRoomDiscovered;

        public override void OnNetworkSpawn()
        {
            _currentState.OnValueChanged += OnStateChanged;
            HandleStateVisuals(_currentState.Value);
        }

        public override void OnNetworkDespawn()
        {
            _currentState.OnValueChanged -= OnStateChanged;
        }

        // configures this room's identity and connections
        // called by MapGenerator on the server after instantiation
        public void Configure(int roomIndex, int eyewallIndex, RoomTaskType taskType,
                              int leftRoom, int rightRoom, int inwardRoom)
        {
            _roomIndex = roomIndex;
            _eyewallIndex = eyewallIndex;
            _taskType = taskType;
            _leftRoomIndex = leftRoom;
            _rightRoomIndex = rightRoom;
            _inwardRoomIndex = inwardRoom;

            // sync task type to clients
            _taskTypeNet.Value = (int)taskType;

            // subscribe to spawner completion if we have one
            if (roomSpawner != null)
            {
                EnemySpawner.OnAllEnemiesDefeated += HandleSpawnerCleared;
            }
            else
            {
                Debug.LogWarning($"[StormRoom] room {roomIndex} has no EnemySpawner assigned!");
            }
        }


        // overrides the prefab's default exit points with generated positions
        // used when the MapGenerator needs to reposition exits for tunnel connections
        public void SetExitPoints(Transform left, Transform right, Transform inward)
        {
            if (left != null) leftExitPoint = left;
            if (right != null) rightExitPoint = right;
            if (inward != null) inwardExitPoint = inward;
        }

        // =====================================
        // state management (server only)
        // =====================================

        // marks room as visible — called when adjacent room is cleared
        public void SetVisible()
        {
            if (!IsServer) return;
            if (_currentState.Value == StormRoomState.Cleared) return;
            if (_currentState.Value == StormRoomState.Visible) return;

            _currentState.Value = StormRoomState.Visible;
        }

        // marks room as active — called when players enter
        public void SetActive()
        {
            if (!IsServer) return;
            if (_currentState.Value == StormRoomState.Cleared) return;

            _currentState.Value = StormRoomState.Active;

            // activate the spawner
            if (roomSpawner != null)
            {
                roomSpawner.StartSpawning();
            }
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

        // =====================================
        // entry trigger
        // =====================================

        private void Start()
        {
            if (entryTrigger != null)
            {
                entryTrigger.OnTriggerVolumeEnter += HandlePlayerEntered;
            }
        }

        private void OnDestroy()
        {
            if (entryTrigger != null)
            {
                entryTrigger.OnTriggerVolumeEnter -= HandlePlayerEntered;
            }

            if (roomSpawner != null)
            {
                EnemySpawner.OnAllEnemiesDefeated -= HandleSpawnerCleared;
            }
        }

        private void HandlePlayerEntered()
        {
            if (!IsServer) return;

            // only activate if room is Visible (not already active or cleared)
            if (_currentState.Value == StormRoomState.Visible)
            {
                SetActive();
                OnRoomActivated?.Invoke(this);
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
            // reveal/hide room visuals based on state
            // Hidden = everything off, Visible = geometry on but no spawner, Active = full active, Cleared = dimmed
            gameObject.SetActive(state != StormRoomState.Hidden);

            if (state == StormRoomState.Visible)
            {
                OnRoomDiscovered?.Invoke(this);
            }
        }

        // =====================================
        // queries
        // =====================================

        // returns the spawn point transform for a given player index
        // wraps around if more players than spawn points
        public Transform GetSpawnPoint(int playerIndex)
        {
            if (playerSpawnPoints == null || playerSpawnPoints.Length == 0)
            {
                // fallback to room center
                return transform;
            }
            return playerSpawnPoints[playerIndex % playerSpawnPoints.Length];
        }

        // returns the exit transform for a given connected room index
        public Transform GetExitForRoom(int targetRoomIndex)
        {
            if (targetRoomIndex == _leftRoomIndex) return leftExitPoint;
            if (targetRoomIndex == _rightRoomIndex) return rightExitPoint;
            if (targetRoomIndex == _inwardRoomIndex) return inwardExitPoint;
            return null;
        }
    }
}
