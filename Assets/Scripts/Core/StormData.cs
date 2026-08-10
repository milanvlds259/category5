using UnityEngine;
using Category5.Boss;

namespace Category5.Core
{
    // defines a storm's structure: how many eyewalls, rooms per wall, difficulty scaling
    // one per storm category/difficulty — assigned to MapGenerator via StormCategoryData
    [CreateAssetMenu(menuName = "Category5/Storm Data")]
    public class StormData : ScriptableObject
    {
        [Header("storm identity")]
        public string stormName;

        [Header("ring structure")]
        [Tooltip("number of eyewalls (concentric rings) in this storm")]
        public int eyewallCount = 3;

        [Tooltip("number of rooms per eyewall (outermost first). length must match eyewallCount")]
        public int[] roomsPerEyewall = { 8, 5, 3 };

        [Tooltip("how many rooms per ring have inward paths to the next inner ring")]
        public int[] inwardPathsPerRing = { 2, 2, 1 };

        [Header("difficulty scaling")]
        [Tooltip("enemy count multiplier per eyewall ring (inner = harder)")]
        public float enemyCountMultiplier = 1.2f;

        [Tooltip("enemy health multiplier per eyewall ring (inner = harder)")]
        public float enemyHealthMultiplier = 1.15f;

        [Tooltip("extra difficulty ramp applied to inner rings")]
        public float innerRingDifficultyRamp = 0.1f;

        [Header("boss")]
        [Tooltip("boss to spawn in the eye of this storm")]
        public BossData bossForEye;

        [Header("room prefab pools")]
        [Tooltip("prefab pool for outermost ring rooms")]
        public RoomPrefabPool outerRoomPool;

        [Tooltip("optional per-ring prefab pools (null = reuse outer pool for that ring)")]
        public RoomPrefabPool[] innerRoomPools;

        [Tooltip("prefab pool for the eye room (boss arena)")]
        public RoomPrefabPool eyeRoomPool;

        [Header("blueprint")]
        [Tooltip("optional artist-authored blueprint — overrides procedural ring generation when assigned")]
        public StormBlueprint blueprint;

        // true when a blueprint is assigned and ready to use
        public bool HasBlueprint => blueprint != null;

        [Header("transition timers")]
        [Tooltip("seconds after room clear before players are recalled to the van")]
        public float recallTimer = 5f;

        [Tooltip("seconds players wait in the van before the next room spawns")]
        public float prepTimer = 30f;

	    // returns the prefab pool for a given eyewall index
        // 0 = outermost, increases inward. null pool = eye.
        public RoomPrefabPool GetPoolForRing(int ringIndex)
        {
            // innermost ring before the eye — check inner pools
            if (innerRoomPools != null && ringIndex < innerRoomPools.Length)
            {
                var pool = innerRoomPools[ringIndex];
                if (pool != null) return pool;
            }

            // fallback to outer pool
            return outerRoomPool;
        }

        // calculates the difficulty multiplier for a given ring
        // 0 = outermost, higher = inner rings
        public float GetDifficultyMultiplier(int ringIndex)
        {
            float base_mult = Mathf.Pow(enemyCountMultiplier, ringIndex);
            float ramp = 1f + (innerRingDifficultyRamp * ringIndex);
            return base_mult * ramp;
        }

        // returns the number of inward paths for a given ring
        // clamps to valid range
        public int GetInwardPathsForRing(int ringIndex)
        {
            if (inwardPathsPerRing == null || inwardPathsPerRing.Length == 0) return 0;
            if (ringIndex < 0 || ringIndex >= inwardPathsPerRing.Length) return 0;
            return inwardPathsPerRing[ringIndex];
        }

        // returns number of rooms for a given ring index
        public int GetRoomsForRing(int ringIndex)
        {
            if (roomsPerEyewall == null || roomsPerEyewall.Length == 0) return 0;
            if (ringIndex < 0 || ringIndex >= roomsPerEyewall.Length) return 0;
            return roomsPerEyewall[ringIndex];
        }

        private void OnValidate()
        {
            // keep array lengths in sync with eyewall count
            if (roomsPerEyewall != null && roomsPerEyewall.Length != eyewallCount)
            {
                Debug.LogWarning($"[StormData] roomsPerEyewall length ({roomsPerEyewall.Length}) doesn't match eyewallCount ({eyewallCount}). adjust manually.");
            }
            if (inwardPathsPerRing != null && inwardPathsPerRing.Length != eyewallCount)
            {
                Debug.LogWarning($"[StormData] inwardPathsPerRing length ({inwardPathsPerRing.Length}) doesn't match eyewallCount ({eyewallCount}). adjust manually.");
            }
        }
    }
}
