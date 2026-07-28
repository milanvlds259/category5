using System.Collections.Generic;
using UnityEngine;
using Category5.Core;

namespace Category5.Map
{
    // pure data class holding the full graph of a storm instance
    // created by MapGenerator, read by GameFlowManager and UI
    // not a MonoBehaviour — just a data container
    public class StormMapLayout
    {
        private List<StormRoomData> _allRooms;
        private List<List<int>> _rings;
        private int _eyeRoomIndex;
        private int _startingRoomIndex;

        public int EyeRoomIndex => _eyeRoomIndex;
        public int StartingRoomIndex => _startingRoomIndex;
        public int TotalRooms => _allRooms.Count;
        public int RingCount => _rings.Count;

        public StormMapLayout()
        {
            _allRooms = new List<StormRoomData>();
            _rings = new List<List<int>>();
        }

        // =====================================
        // construction
        // =====================================

        /// <summary>
        /// adds a room to the layout
        /// </summary>
        public void AddRoom(StormRoomData room)
        {
            _allRooms.Add(room);
        }

        /// <summary>
        /// updates an existing room's data at the given index
        /// </summary>
        public void UpdateRoom(int index, StormRoomData data)
        {
            if (index < 0 || index >= _allRooms.Count)
            {
                Debug.LogError($"[StormMapLayout] cannot update room index {index} out of range");
                return;
            }
            _allRooms[index] = data;
        }

        /// <summary>
        /// creates a new ring and returns its index
        /// </summary>
        public int CreateRing()
        {
            int ringIndex = _rings.Count;
            _rings.Add(new List<int>());
            return ringIndex;
        }

        /// <summary>
        /// adds a room index to a specific ring
        /// </summary>
        public void AddRoomToRing(int ringIndex, int roomIndex)
        {
            if (ringIndex >= 0 && ringIndex < _rings.Count)
            {
                _rings[ringIndex].Add(roomIndex);
            }
        }

        /// <summary>
        /// sets the eye room index
        /// </summary>
        public void SetEyeRoom(int roomIndex)
        {
            _eyeRoomIndex = roomIndex;
        }

        /// <summary>
        /// sets the starting room index
        /// </summary>
        public void SetStartingRoom(int roomIndex)
        {
            _startingRoomIndex = roomIndex;
        }

        // =====================================
        // queries
        // =====================================

        /// <summary>
        /// returns the room data for a given index
        /// </summary>
        public StormRoomData GetRoom(int index)
        {
            if (index < 0 || index >= _allRooms.Count)
            {
                Debug.LogError($"[StormMapLayout] room index {index} out of range (0-{_allRooms.Count - 1})");
                return default;
            }
            return _allRooms[index];
        }

        /// <summary>
        /// returns all room indices in a given ring
        /// </summary>
        public List<int> GetRingRooms(int ringIndex)
        {
            if (ringIndex < 0 || ringIndex >= _rings.Count)
            {
                Debug.LogError($"[StormMapLayout] ring index {ringIndex} out of range (0-{_rings.Count - 1})");
                return new List<int>();
            }
            return _rings[ringIndex];
        }

        /// <summary>
        /// returns all rooms connected to the given room (left, right, inward)
        /// </summary>
        public List<int> GetConnectedRooms(int roomIndex)
        {
            var room = GetRoom(roomIndex);
            var connected = new List<int>();

            if (room.leftRoomIndex >= 0) connected.Add(room.leftRoomIndex);
            if (room.rightRoomIndex >= 0) connected.Add(room.rightRoomIndex);
            if (room.inwardRoomIndex >= 0) connected.Add(room.inwardRoomIndex);

            return connected;
        }

        /// <summary>
        /// returns the ring index for a given room index
        /// -1 if not found (shouldn't happen)
        /// </summary>
        public int GetRingForRoom(int roomIndex)
        {
            for (int i = 0; i < _rings.Count; i++)
            {
                if (_rings[i].Contains(roomIndex))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// returns the total number of rooms
        /// </summary>
        public int RoomCount => _allRooms.Count;

        /// <summary>
        /// returns all room data (read-only access for iteration)
        /// </summary>
        public IReadOnlyList<StormRoomData> AllRooms => _allRooms;
    }

    // data struct for a single room in the storm layout
    // used by StormMapLayout and StormRoom
    [System.Serializable]
    public struct StormRoomData
    {
        public int roomIndex;
        public int eyewallIndex;            // -1 = eye
        public int ringPosition;            // position within the ring (0 to N-1)
        public RoomTaskType taskType;
        public Vector3 worldPosition;
        public int leftRoomIndex;           // adjacent room (counter-clockwise)
        public int rightRoomIndex;          // adjacent room (clockwise)
        public int inwardRoomIndex;         // inner ring connection (-1 = none, max 1 per room)
        public int prefabPoolIndex;         // which prefab was used from the pool
    }
}
