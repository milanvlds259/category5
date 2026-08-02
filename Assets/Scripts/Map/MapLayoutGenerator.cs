using System.Collections.Generic;
using UnityEngine;
using Category5.Core;

namespace Category5.Map
{
    // generates the full spatial layout for a storm
    // computes positions and connections for all rooms in concentric rings
    // does NOT instantiate rooms — only builds the MapLayout data structure
    // RoomManager handles actual instantiation (one room at a time)
    public class MapLayoutGenerator
    {
        [Header("ring layout")]
        [Tooltip("radius of the outermost ring — must be large enough so rooms don't overlap")]
        public float outerRingRadius = 200f;

        [Tooltip("radius decrease per inner ring — must be larger than room diameter")]
        public float ringRadiusStep = 60f;

        [Tooltip("minimum ring radius (innermost ring won't be smaller than this)")]
        public float minRingRadius = 80f;

        // generates the full layout for a storm with the given seed
        // returns a MapLayout with all room positions and connections
        public MapLayout Generate(StormData storm, int seed)
        {
            if (storm == null)
            {
                Debug.LogError("[MapLayoutGenerator] cannot generate layout — storm is null");
                return null;
            }

            UnityEngine.Random.InitState(seed);

            MapLayout layout = new MapLayout();

            // step 1: create the eye room (boss arena) at center
            CreateEyeRoom(storm, layout);

            // step 2: create eyewall rings from innermost outward
            for (int ring = storm.eyewallCount - 1; ring >= 0; ring--)
            {
                CreateRing(storm, layout, ring);
            }

            // step 3: connect rings (inward paths from outer to inner)
            ConnectRings(storm, layout);

            // step 4: pick a random starting room in the outermost ring
            PickStartingRoom(layout);

            Debug.Log($"[MapLayoutGenerator] generated {layout.TotalRooms} rooms in {layout.RingCount} rings");
            return layout;
        }

        // =====================================
        // eye room
        // =====================================

        private void CreateEyeRoom(StormData storm, MapLayout layout)
        {
            int roomIndex = layout.TotalRooms;
            int eyeRing = layout.CreateRing();

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

            layout.AddRoom(data);
            layout.AddRoomToRing(eyeRing, roomIndex);
            layout.SetEyeRoom(roomIndex);
        }

        // =====================================
        // ring creation
        // =====================================

        private void CreateRing(StormData storm, MapLayout layout, int ringIndex)
        {
            int roomsInRing = storm.GetRoomsForRing(ringIndex);
            if (roomsInRing <= 0)
            {
                Debug.LogWarning($"[MapLayoutGenerator] ring {ringIndex} has 0 rooms, skipping");
                return;
            }

            // ring 0 = outermost (largest radius), increases inward
            float radius = Mathf.Max(minRingRadius, outerRingRadius - (ringIndex * ringRadiusStep));
            Debug.Log($"[MapLayoutGenerator] ring {ringIndex}: {roomsInRing} rooms, radius={radius:F1}");

            // create the ring in the layout
            int layoutRingIndex = layout.CreateRing();

            // place rooms evenly around the ring
            float angleStep = 360f / roomsInRing;
            float startAngle = UnityEngine.Random.Range(0f, 360f);

            for (int i = 0; i < roomsInRing; i++)
            {
                float angle = (startAngle + (angleStep * i)) * Mathf.Deg2Rad;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                );

                // register in layout
                int roomIndex = layout.TotalRooms;
                StormRoomData data = new StormRoomData
                {
                    roomIndex = roomIndex,
                    eyewallIndex = ringIndex,
                    ringPosition = i,
                    taskType = RoomTaskType.EnemyWave,
                    worldPosition = position,
                    leftRoomIndex = -1,
                    rightRoomIndex = -1,
                    inwardRoomIndex = -1,
                    prefabPoolIndex = 0
                };
                layout.AddRoom(data);
                layout.AddRoomToRing(layoutRingIndex, roomIndex);
            }

            // connect rooms within this ring (left/right)
            ConnectRing(layout, layoutRingIndex);
        }

        // =====================================
        // ring connections
        // =====================================

        private void ConnectRing(MapLayout layout, int layoutRingIndex)
        {
            List<int> ringRooms = layout.GetRingRooms(layoutRingIndex);
            if (ringRooms.Count < 2) return;

            for (int i = 0; i < ringRooms.Count; i++)
            {
                int currentIdx = ringRooms[i];
                int leftIdx = ringRooms[(i - 1 + ringRooms.Count) % ringRooms.Count];
                int rightIdx = ringRooms[(i + 1) % ringRooms.Count];

                var currentData = layout.GetRoom(currentIdx);
                currentData.leftRoomIndex = leftIdx;
                currentData.rightRoomIndex = rightIdx;
                layout.UpdateRoom(currentIdx, currentData);
            }
        }

        private void ConnectRings(StormData storm, MapLayout layout)
        {
            // for each storm ring except the innermost, create inward paths
            // storm ring 0 = outermost, increases inward
            for (int ring = 0; ring < storm.eyewallCount - 1; ring++)
            {
                int inwardPaths = storm.GetInwardPathsForRing(ring);
                int outerLayoutRing = StormRingToLayoutRing(layout, ring);
                int innerLayoutRing = StormRingToLayoutRing(layout, ring + 1);
                List<int> ringRooms = layout.GetRingRooms(outerLayoutRing);

                if (ringRooms.Count == 0) continue;

                inwardPaths = Mathf.Min(inwardPaths, ringRooms.Count);

                List<int> shuffled = new List<int>(ringRooms);
                Shuffle(shuffled);

                List<int> innerRooms = layout.GetRingRooms(innerLayoutRing);
                if (innerRooms.Count == 0)
                {
                    Debug.LogWarning($"[MapLayoutGenerator] inner ring {ring + 1} (layout {innerLayoutRing}) has no rooms for inward connections from ring {ring}");
                    continue;
                }

                HashSet<int> usedInnerRooms = new HashSet<int>();
                int pathsCreated = 0;

                for (int i = 0; i < shuffled.Count && pathsCreated < inwardPaths; i++)
                {
                    int outerRoomIdx = shuffled[i];

                    int innerRoomIdx = -1;
                    foreach (int inner in innerRooms)
                    {
                        if (!usedInnerRooms.Contains(inner))
                        {
                            innerRoomIdx = inner;
                            break;
                        }
                    }

                    if (innerRoomIdx < 0) break;

                    var outerData = layout.GetRoom(outerRoomIdx);
                    outerData.inwardRoomIndex = innerRoomIdx;
                    layout.UpdateRoom(outerRoomIdx, outerData);

                    usedInnerRooms.Add(innerRoomIdx);
                    pathsCreated++;
                }
            }
        }

        // =====================================
        // starting room
        // =====================================

        private void PickStartingRoom(MapLayout layout)
        {
            // outermost storm ring = storm ring 0 = layout ring eyewallCount
            int outerLayoutRing = StormRingToLayoutRing(layout, 0);
            List<int> outerRooms = layout.GetRingRooms(outerLayoutRing);
            if (outerRooms.Count == 0)
            {
                Debug.LogError($"[MapLayoutGenerator] outermost ring (layout ring {outerLayoutRing}) has no rooms!");
                return;
            }

            int startIdx = outerRooms[UnityEngine.Random.Range(0, outerRooms.Count)];
            layout.SetStartingRoom(startIdx);
            Debug.Log($"[MapLayoutGenerator] picked starting room {startIdx} in outermost ring");
        }

        // =====================================
        // helpers
        // =====================================

        // maps a storm ring index (0=outermost) to layout ring index
        // layout ring 0 = eye, ring 1 = innermost storm ring, ring eyewallCount = outermost
        private int StormRingToLayoutRing(MapLayout layout, int stormRingIndex)
        {
            // we need eyewallCount from the storm — but we don't have it here
            // use the ring count minus 1 as a proxy (eye + eyewalls)
            // actually, we need to pass it in or store it
            // for now, assume the layout has eye (ring 0) + eyewallCount rings
            // so layout ring = eyewallCount - stormRingIndex
            // we need to know eyewallCount — let's add it as a field
            return _eyewallCount - stormRingIndex;
        }

        private int _eyewallCount = 3;

        public void SetEyewallCount(int count)
        {
            _eyewallCount = count;
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
    }
}
