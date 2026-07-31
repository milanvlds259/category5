using UnityEngine;

namespace Category5.Core
{
    // pure data struct for a single room in the storm layout
    // used by MapLayout, MapLayoutGenerator, RoomManager, and UI
    [System.Serializable]
    public struct StormRoomData
    {
        // identity
        public int roomIndex;
        public int eyewallIndex;    // -1 = eye (boss room), 0 = outermost ring
        public int ringPosition;    // position within the ring

        // task
        public RoomTaskType taskType;

        // layout
        public Vector3 worldPosition;
        public int leftRoomIndex;       // -1 if none
        public int rightRoomIndex;      // -1 if none
        public int inwardRoomIndex;     // -1 if none
        public int prefabPoolIndex;     // index into StormData.prefabPools
    }
}
