using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using Category5.Player.WindRiding;
using Category5.Enemies;
using Category5.Core;
using Unity.AI.Navigation;
using Unity.Netcode;

namespace Category5.Map
{
    // generates storm maps from hand-crafted room prefabs arranged in concentric rings
    // replaces the old procedural arena system with prefab-based ring layouts
    public class MapGenerator : NetworkBehaviour
    {
        [Header("storm configuration")]
        [Tooltip("the storm to generate â€” set by NetworkMenu before scene load, or assign in inspector for testing")]
        [SerializeField] private StormData defaultStorm;

        [Header("ring layout")]
        [Tooltip("radius of the outermost ring — must be large enough so rooms don't overlap. rule of thumb: roomsPerRing × minRoomSpacing / (2π)")]
        [SerializeField] private float outerRingRadius = 200f;

        [Tooltip("radius decrease per inner ring — must be larger than room diameter to prevent interring overlap")]
        [SerializeField] private float ringRadiusStep = 60f;

        [Tooltip("minimum ring radius (innermost ring won't be smaller than this)")]
        [SerializeField] private float minRingRadius = 80f;

        [Header("wind tunnel visuals")]
        [SerializeField] private Material cloudWallMaterial;
        [SerializeField] private float tunnelRadius = 5f;

        [Header("navmesh")]
        [SerializeField] private LayerMask navMeshLayer = -1;

        // seed sync
        public NetworkVariable<int> Seed = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // map root
        private GameObject _mapParent;

        // current storm data
        private StormData _currentStorm;

        // layout built during generation
        private StormMapLayout _layout;

        // all spawned room instances
        private List<StormRoom> _spawnedRooms = new List<StormRoom>();

        // all generated wind tunnel objects
        private List<GameObject> _windTunnels = new List<GameObject>();

        private bool IsServerAuthority => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        // =====================================
        // lifecycle
        // =====================================

        public override void OnNetworkSpawn()
        {
            Seed.OnValueChanged += OnSeedChanged;

            // start generation if we have a storm set and are the server
            if (IsServerAuthority)
            {
                // fall back to defaultStorm if SetStorm wasn't called
                if (_currentStorm == null)
                {
                    _currentStorm = defaultStorm;
                }

                if (_currentStorm != null)
                {
                    StartGeneration();
                }
                else
                {
                    Debug.LogError("[MapGenerator] no storm data assigned — cannot generate map");
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            Seed.OnValueChanged -= OnSeedChanged;
        }

        private void OnSeedChanged(int previousValue, int newValue)
        {
            if (!IsServerAuthority && newValue != 0)
            {
                DeleteMap();
                GenerateStormMap(newValue);
            }
        }

        // =====================================
        // public API
        // =====================================

        /// <summary>
        /// sets the storm data and starts generation (called by NetworkMenu before scene load)
        /// </summary>
        public void SetStorm(StormData storm)
        {
            _currentStorm = storm;
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.SetStormData(storm);
            }
        }

        /// <summary>
        /// called by GameFlowManager after scene load to kick off generation
        /// </summary>
        public void StartGeneration()
        {
            if (!IsServerAuthority) return;
            if (_currentStorm == null)
            {
                // fall back to default storm for testing
                _currentStorm = defaultStorm;
                if (_currentStorm == null)
                {
                    Debug.LogError("[MapGenerator] no storm data assigned â€” cannot generate map");
                    return;
                }
            }

            // pick a random seed and sync it
            Seed.Value = UnityEngine.Random.Range(-99999, 99999);
            GenerateStormMap(Seed.Value);
        }

        /// <summary>
        /// legacy entry point â€” called by old code paths
        /// </summary>
        public void StartRound()
        {
            if (IsServerAuthority)
            {
                StartGeneration();
            }
        }

        // =====================================
        // map generation
        // =====================================

        private void GenerateStormMap(int seed)
        {
            UnityEngine.Random.InitState(seed);
            DeleteMap();

            _mapParent = new GameObject("StormMap");
            _layout = new StormMapLayout();

            // step 1: create the eye room (boss arena) at center
            CreateEyeRoom();

            // step 2: create eyewall rings from innermost outward
            for (int ring = _currentStorm.eyewallCount - 1; ring >= 0; ring--)
            {
                CreateRing(ring);
            }

            // step 3: connect rings (inward paths from outer to inner)
            ConnectRings();

            // step 4: pick a random starting room in the outermost ring
            PickStartingRoom();

            // step 5: configure all StormRoom components
            ConfigureAllRooms();

            // step 6: build navmesh
            if (Application.isPlaying)
            {
                StartCoroutine(BuildNavMesh());
            }

            // step 7: notify GameFlowManager
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.SetLayout(_layout);
            }
        }

        // =====================================
        // eye room
        // =====================================

        private void CreateEyeRoom()
        {
            if (_currentStorm.eyeRoomPool == null || _currentStorm.eyeRoomPool.PrefabCount == 0)
            {
                Debug.LogError("[MapGenerator] no eye room pool or pool is empty");
                return;
            }

            GameObject prefab = _currentStorm.eyeRoomPool.GetRandomPrefab();
            if (prefab == null)
            {
                Debug.LogError("[MapGenerator] eye room pool returned null prefab");
                return;
            }

            GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            instance.transform.SetParent(_mapParent.transform);
            instance.name = "EyeRoom";

            StormRoom room = instance.GetComponent<StormRoom>();
            if (room == null)
            {
                Debug.LogError($"[MapGenerator] eye room prefab '{prefab.name}' is missing StormRoom component");
                Destroy(instance);
                return;
            }

            _spawnedRooms.Add(room);

            // register in layout
            int roomIndex = _layout.TotalRooms;
            int eyeRing = _layout.CreateRing(); // ring index for the eye
            StormRoomData data = new StormRoomData
            {
                roomIndex = roomIndex,
                eyewallIndex = -1, // -1 = eye
                ringPosition = 0,
                taskType = RoomTaskType.EnemyWave,
                worldPosition = Vector3.zero,
                leftRoomIndex = -1,
                rightRoomIndex = -1,
                inwardRoomIndex = -1,
                prefabPoolIndex = 0
            };
            _layout.AddRoom(data);
            _layout.AddRoomToRing(eyeRing, roomIndex);
            _layout.SetEyeRoom(roomIndex);
        }

        // =====================================
        // ring creation
        // =====================================

        private void CreateRing(int ringIndex)
        {
            int roomsInRing = _currentStorm.GetRoomsForRing(ringIndex);
            if (roomsInRing <= 0)
            {
                Debug.LogWarning($"[MapGenerator] ring {ringIndex} has 0 rooms, skipping");
                return;
            }

            // ring 0 = outermost (largest radius), increases inward
            float radius = Mathf.Max(minRingRadius, outerRingRadius - (ringIndex * ringRadiusStep));
            Debug.Log($"[MapGenerator] ring {ringIndex}: {roomsInRing} rooms, radius={radius:F1}");

            // get the prefab pool for this ring
            RoomPrefabPool pool = _currentStorm.GetPoolForRing(ringIndex);
            if (pool == null || pool.PrefabCount == 0)
            {
                Debug.LogError($"[MapGenerator] no valid prefab pool for ring {ringIndex}");
                return;
            }

            // create the ring in the layout
            int layoutRingIndex = _layout.CreateRing();

            // place rooms evenly around the ring
            float angleStep = 360f / roomsInRing;
            float startAngle = UnityEngine.Random.Range(0f, 360f); // random rotation for variety

            for (int i = 0; i < roomsInRing; i++)
            {
                float angle = (startAngle + (angleStep * i)) * Mathf.Deg2Rad;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                );

                // pick a random prefab from the pool
                GameObject prefab = pool.GetRandomPrefab();
                if (prefab == null)
                {
                    Debug.LogWarning($"[MapGenerator] pool returned null for ring {ringIndex} room {i}");
                    continue;
                }

                // instantiate and parent
                GameObject instance = Instantiate(prefab, position, Quaternion.identity);
                instance.transform.SetParent(_mapParent.transform);
                instance.name = $"Ring{ringIndex}_Room{i}";

                StormRoom room = instance.GetComponent<StormRoom>();
                if (room == null)
                {
                    Debug.LogError($"[MapGenerator] room prefab '{prefab.name}' is missing StormRoom component");
                    Destroy(instance);
                    continue;
                }

                _spawnedRooms.Add(room);
                Debug.Log($"[MapGenerator]   room {i} at ring {ringIndex}: world pos={instance.transform.position}, angle={angle * Mathf.Rad2Deg:F0}°");

                // register in layout
                int roomIndex = _layout.TotalRooms;
                StormRoomData data = new StormRoomData
                {
                    roomIndex = roomIndex,
                    eyewallIndex = ringIndex,
                    ringPosition = i,
                    taskType = RoomTaskType.EnemyWave,
                    worldPosition = position,
                    leftRoomIndex = -1,   // set in ConnectRing
                    rightRoomIndex = -1,  // set in ConnectRing
                    inwardRoomIndex = -1, // set in ConnectRings
                    prefabPoolIndex = UnityEngine.Random.Range(0, pool.PrefabCount)
                };
                _layout.AddRoom(data);
                _layout.AddRoomToRing(layoutRingIndex, roomIndex);
            }

            // connect rooms within this ring (left/right)
            ConnectRing(layoutRingIndex);
        }

        /// <summary>
        /// connects rooms within a ring via left/right adjacency and creates wind tunnels
        /// </summary>
        private void ConnectRing(int layoutRingIndex)
        {
            List<int> ringRooms = _layout.GetRingRooms(layoutRingIndex);
            if (ringRooms.Count < 2) return;

            for (int i = 0; i < ringRooms.Count; i++)
            {
                int currentIdx = ringRooms[i];
                int leftIdx = ringRooms[(i - 1 + ringRooms.Count) % ringRooms.Count];
                int rightIdx = ringRooms[(i + 1) % ringRooms.Count];

                // update layout data
                var currentData = _layout.GetRoom(currentIdx);
                currentData.leftRoomIndex = leftIdx;
                currentData.rightRoomIndex = rightIdx;

                // update the room in the layout (struct â€” need to re-add)
                _layout.UpdateRoom(currentIdx, currentData);

                // create wind tunnel to right neighbor (avoid duplicates)
                if (i < ringRooms.Count - 1 || ringRooms.Count <= 2)
                {
                    CreateWindTunnel(currentIdx, rightIdx);
                }
            }
        }

        // =====================================
        // ring connections (inward paths)
        // =====================================

        private void ConnectRings()
        {
            // for each storm ring except the innermost, create inward paths
            // storm ring 0 = outermost, increases inward
            for (int ring = 0; ring < _currentStorm.eyewallCount - 1; ring++)
            {
                int inwardPaths = _currentStorm.GetInwardPathsForRing(ring);
                int outerLayoutRing = StormRingToLayoutRing(ring);
                int innerLayoutRing = StormRingToLayoutRing(ring + 1);
                List<int> ringRooms = _layout.GetRingRooms(outerLayoutRing);

                if (ringRooms.Count == 0) continue;

                // clamp inward paths to available rooms
                inwardPaths = Mathf.Min(inwardPaths, ringRooms.Count);

                // randomly select which rooms get inward paths
                List<int> shuffled = new List<int>(ringRooms);
                Shuffle(shuffled);

                // get the next inner ring's rooms
                List<int> innerRooms = _layout.GetRingRooms(innerLayoutRing);

                if (innerRooms.Count == 0)
                {
                    Debug.LogWarning($"[MapGenerator] inner ring {ring + 1} (layout {innerLayoutRing}) has no rooms for inward connections from ring {ring}");
                    continue;
                }

                // track which inner rooms are already connected (avoid duplicates)
                HashSet<int> usedInnerRooms = new HashSet<int>();
                int pathsCreated = 0;

                for (int i = 0; i < shuffled.Count && pathsCreated < inwardPaths; i++)
                {
                    int outerRoomIdx = shuffled[i];

                    // find an unused inner room
                    int innerRoomIdx = -1;
                    foreach (int inner in innerRooms)
                    {
                        if (!usedInnerRooms.Contains(inner))
                        {
                            innerRoomIdx = inner;
                            break;
                        }
                    }

                    if (innerRoomIdx < 0) break; // all inner rooms connected

                    // connect outward room's inward exit to inner room
                    var outerData = _layout.GetRoom(outerRoomIdx);
                    outerData.inwardRoomIndex = innerRoomIdx;
                    _layout.UpdateRoom(outerRoomIdx, outerData);

                    usedInnerRooms.Add(innerRoomIdx);
                    pathsCreated++;

                    // create wind tunnel between the rooms
                    CreateWindTunnel(outerRoomIdx, innerRoomIdx);
                }
            }
        }

        // =====================================
        // wind tunnel creation
        // =====================================

        private void CreateWindTunnel(int roomAIndex, int roomBIndex)
        {
            StormRoom roomA = FindSpawnedRoom(roomAIndex);
            StormRoom roomB = FindSpawnedRoom(roomBIndex);

            if (roomA == null || roomB == null)
            {
                Debug.LogWarning($"[MapGenerator] could not find rooms for tunnel: {roomAIndex} -> {roomBIndex}");
                return;
            }

            // determine exit/entry points
            Transform exitA = GetExitPoint(roomA, roomBIndex);
            Transform exitB = GetExitPoint(roomB, roomAIndex);

            if (exitA == null || exitB == null)
            {
                // fallback to room transforms if exit points aren't set
                exitA = exitA != null ? exitA : roomA.transform;
                exitB = exitB != null ? exitB : roomB.transform;
            }

            // create wind tunnel container
            GameObject tunnelObj = new GameObject($"Tunnel_{roomAIndex}_to_{roomBIndex}");
            tunnelObj.transform.SetParent(_mapParent.transform);

            // create launch pad A (at roomA's exit)
            GameObject padAObj = new GameObject("LaunchPad_A");
            padAObj.transform.SetParent(tunnelObj.transform);
            padAObj.transform.position = exitA.position;
            padAObj.transform.LookAt(exitB.position);
            padAObj.transform.Rotate(0, -90, 0);
            WindLaunchPad padA = padAObj.AddComponent<WindLaunchPad>();

            // create launch pad B (at roomB's exit)
            GameObject padBObj = new GameObject("LaunchPad_B");
            padBObj.transform.SetParent(tunnelObj.transform);
            padBObj.transform.position = exitB.position;
            padBObj.transform.LookAt(exitA.position);
            padBObj.transform.Rotate(0, -90, 0);
            WindLaunchPad padB = padBObj.AddComponent<WindLaunchPad>();

            // build spline between the two pads
            SplineContainer splineContainer = tunnelObj.AddComponent<SplineContainer>();
            Spline spline = splineContainer.Spline;
            spline.Clear();

            // add start and end knots
            Vector3 startPos = exitA.position;
            Vector3 endPos = exitB.position;
            Vector3 midPos = Vector3.Lerp(startPos, endPos, 0.5f);
            midPos.y += 10f; // lift the middle for a nice arc

            BezierKnot startKnot = new BezierKnot(startPos);
            BezierKnot midKnot = new BezierKnot(midPos);
            BezierKnot endKnot = new BezierKnot(endPos);

            spline.Add(startKnot, TangentMode.AutoSmooth);
            spline.Add(midKnot, TangentMode.AutoSmooth);
            spline.Add(endKnot, TangentMode.AutoSmooth);

            // add wind tunnel component
            WindTunnel windTunnel = tunnelObj.AddComponent<WindTunnel>();
            windTunnel.SetTunnelRadius(tunnelRadius);
            windTunnel.RefreshSplineData();

            // add visualizer
            var visualizer = tunnelObj.AddComponent<WindTunnelVisualizer>();
            visualizer.RefreshVisuals();

            // configure launch pads
            padA.ConfigureTunnel(windTunnel, true);
            padB.ConfigureTunnel(windTunnel, false);

            // put it on the cloud surface layer
            tunnelObj.layer = 8;

            _windTunnels.Add(tunnelObj);
        }

        /// <summary>
        /// returns the appropriate exit point Transform for connecting to a target room
        /// </summary>
        private Transform GetExitPoint(StormRoom room, int targetRoomIndex)
        {
            var data = _layout.GetRoom(room.RoomIndex);

            if (data.leftRoomIndex == targetRoomIndex) return room.LeftExitPoint;
            if (data.rightRoomIndex == targetRoomIndex) return room.RightExitPoint;
            if (data.inwardRoomIndex == targetRoomIndex) return room.InwardExitPoint;

            // check if target is in an inner ring (this room connects inward to it)
            if (room.HasInwardPath && data.inwardRoomIndex == targetRoomIndex)
                return room.InwardExitPoint;

            return null;
        }

        // =====================================
        // starting room
        // =====================================

        private void PickStartingRoom()
        {
            // outermost storm ring = storm ring 0 = layout ring eyewallCount
            int outerLayoutRing = StormRingToLayoutRing(0);
            List<int> outerRooms = _layout.GetRingRooms(outerLayoutRing);
            if (outerRooms.Count == 0)
            {
                Debug.LogError($"[MapGenerator] outermost ring (layout ring {outerLayoutRing}) has no rooms!");
                return;
            }

            int startIdx = outerRooms[UnityEngine.Random.Range(0, outerRooms.Count)];
            _layout.SetStartingRoom(startIdx);
            Debug.Log($"[MapGenerator] picked starting room {startIdx} in outermost ring");
        }

        // =====================================
        // room configuration
        // =====================================

        private void ConfigureAllRooms()
        {
            foreach (var room in _spawnedRooms)
            {
                if (room == null) continue;

                var data = _layout.GetRoom(room.RoomIndex);
                room.Configure(
                    data.roomIndex,
                    data.eyewallIndex,
                    data.taskType,
                    data.leftRoomIndex,
                    data.rightRoomIndex,
                    data.inwardRoomIndex
                );

                // apply difficulty scaling to spawner
                if (room.RoomSpawner != null && data.eyewallIndex >= 0)
                {
                    float difficulty = _currentStorm.GetDifficultyMultiplier(data.eyewallIndex);
                    room.RoomSpawner.SetDifficultyMultiplier(difficulty);
                }
            }
        }

        // =====================================
        // navmesh
        // =====================================

        private IEnumerator BuildNavMesh()
        {
            yield return new WaitForEndOfFrame();

            NavMeshSurface surface = _mapParent.AddComponent<NavMeshSurface>();
            surface.layerMask = navMeshLayer;
            surface.BuildNavMesh();
        }

        // =====================================
        // cleanup
        // =====================================

        public void DeleteMap()
        {
            if (_mapParent != null)
            {
                DestroyImmediate(_mapParent);
            }

            _spawnedRooms.Clear();
            _windTunnels.Clear();
            _layout = null;
        }

        // =====================================
        // helpers
        // =====================================

        private StormRoom FindSpawnedRoom(int roomIndex)
        {
            foreach (var room in _spawnedRooms)
            {
                if (room != null && room.RoomIndex == roomIndex)
                    return room;
            }
            return null;
        }

        /// <summary>
        /// maps a storm ring index (0=outermost) to layout ring index
        /// layout ring 0 = eye, ring 1 = innermost storm ring, ring eyewallCount = outermost
        /// </summary>
        private int StormRingToLayoutRing(int stormRingIndex)
        {
            // layout ring 0 = eye room
            // storm ring (eyewallCount-1) = innermost = layout ring 1
            // storm ring 0 = outermost = layout ring eyewallCount
            return _currentStorm.eyewallCount - stormRingIndex;
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_currentStorm == null) return;

            // draw ring outlines (ring 0 = outermost = largest radius)
            for (int ring = 0; ring < _currentStorm.eyewallCount; ring++)
            {
                float radius = Mathf.Max(minRingRadius, outerRingRadius - (ring * ringRadiusStep));
                Gizmos.color = ring == 0 ? Color.yellow : Color.cyan;
                Gizmos.DrawWireCube(Vector3.up * 5f, new Vector3(radius * 2, 0.5f, radius * 2));
            }

            // draw eye
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(Vector3.up * 5f, 5f);
        }
    }
}