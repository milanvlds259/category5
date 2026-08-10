using UnityEngine;

namespace Category5.Core
{
    // a pool of hand-crafted room prefabs that MapGenerator randomly picks from
    // create via right-click > create > category5 > room prefab pool
    [CreateAssetMenu(menuName = "Category5/Room Prefab Pool")]
    public class RoomPrefabPool : ScriptableObject
    {
        [Tooltip("human-readable name for this pool (shown in inspector)")]
        public string poolName;

        [Tooltip("hand-crafted room prefabs to randomly pick from")]
        public GameObject[] roomPrefabs;

        // each prefab in this pool must have:
        //   - StormRoom component
        //   - EnemySpawner component
        //   - TriggerVolume for room entry detection
        //   - Child Transforms: LeftExit, RightExit, InwardExit (optional)
        //   - Child Transform: SpawnPoints/Spawn0, SpawnPoints/Spawn1, etc.
        //   - A ground surface for player movement
        //   - Cloud boundary visuals (optional, can be baked into prefab)

        // returns a random prefab from this pool
        public GameObject GetRandomPrefab()
        {
            if (roomPrefabs == null || roomPrefabs.Length == 0)
            {
                Debug.LogError($"[RoomPrefabPool] '{poolName}' has no prefabs assigned!");
                return null;
            }

            return roomPrefabs[Random.Range(0, roomPrefabs.Length)];
        }

        // returns the prefab at the given index (wraps around)
        public GameObject GetPrefabAtIndex(int index)
        {
            if (roomPrefabs == null || roomPrefabs.Length == 0) return null;
            return roomPrefabs[index % roomPrefabs.Length];
        }

        // returns the number of prefabs in this pool
        public int PrefabCount => roomPrefabs != null ? roomPrefabs.Length : 0;
    }
}
