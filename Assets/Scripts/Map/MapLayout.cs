using System.Collections.Generic;
using UnityEngine;
using Category5.Core;

namespace Category5.Map
{
    // pure data class holding the full graph of a storm instance
    // created by MapLayoutGenerator, read by RoomManager and UI
    // not a MonoBehaviour — just a data container
    // does NOT instantiate rooms — only stores positions and connections
    public class MapLayout
    {
        private List<StormRoomData> _allRooms;
        private List<List<int>> _rings;
        private int _eyeRoomIndex;
        private int _startingRoomIndex;

        public int EyeRoomIndex => _eyeRoomIndex;
        public int StartingRoomIndex => _startingRoomIndex;
        public int TotalRooms => _allRooms.Count;
        public int RingCount => _rings.Count;
        public int RoomCount => _allRooms.Count;
        public IReadOnlyList<StormRoomData> AllRooms => _allRooms;

        public MapLayout()
        {
            _allRooms = new List<StormRoomData>();
            _rings = new List<List<int>>();
        }

        // =====================================
        // construction
        // =====================================

        public void AddRoom(StormRoomData room)
        {
            _allRooms.Add(room);
        }

        public void UpdateRoom(int index, StormRoomData data)
        {
            if (index < 0 || index >= _allRooms.Count)
            {
                Debug.LogError($"[MapLayout] cannot update room index {index} out of range");
                return;
            }
            _allRooms[index] = data;
        }

        public int CreateRing()
        {
            int ringIndex = _rings.Count;
            _rings.Add(new List<int>());
            return ringIndex;
        }

        public void AddRoomToRing(int ringIndex, int roomIndex)
        {
            if (ringIndex >= 0 && ringIndex < _rings.Count)
            {
                _rings[ringIndex].Add(roomIndex);
            }
        }

        public void SetEyeRoom(int roomIndex)
        {
            _eyeRoomIndex = roomIndex;
        }

        public void SetStartingRoom(int roomIndex)
        {
            _startingRoomIndex = roomIndex;
        }

        // =====================================
        // queries
        // =====================================

        public StormRoomData GetRoom(int index)
        {
            if (index < 0 || index >= _allRooms.Count)
            {
                Debug.LogError($"[MapLayout] room index {index} out of range (0-{_allRooms.Count - 1})");
                return default;
            }
            return _allRooms[index];
        }

        public List<int> GetRingRooms(int ringIndex)
        {
            if (ringIndex < 0 || ringIndex >= _rings.Count)
            {
                Debug.LogError($"[MapLayout] ring index {ringIndex} out of range (0-{_rings.Count - 1})");
                return new List<int>();
            }
            return _rings[ringIndex];
        }

        // returns all rooms connected to the given room (left, right, inward)
        public List<int> GetConnectedRooms(int roomIndex)
        {
            var room = GetRoom(roomIndex);
            var connected = new List<int>();

            if (room.leftRoomIndex >= 0) connected.Add(room.leftRoomIndex);
            if (room.rightRoomIndex >= 0) connected.Add(room.rightRoomIndex);
            if (room.inwardRoomIndex >= 0) connected.Add(room.inwardRoomIndex);

            return connected;
        }

        // returns the ring index for a given room index
        // -1 if not found
        public int GetRingForRoom(int roomIndex)
        {
            for (int i = 0; i < _rings.Count; i++)
            {
                if (_rings[i].Contains(roomIndex))
                    return i;
            }
            return -1;
        }

        // returns the world position for a given room index
        public Vector3 GetRoomPosition(int roomIndex)
        {
            return GetRoom(roomIndex).worldPosition;
        }
    }
}
