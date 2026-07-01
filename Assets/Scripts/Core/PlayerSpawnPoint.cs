using UnityEngine;
using System.Collections.Generic;

namespace Category5.Core
{
    public class PlayerSpawnPoint : MonoBehaviour
    {
        public enum SpawnType { Van, Island }

        private static List<PlayerSpawnPoint> vanSpawnPoints = new List<PlayerSpawnPoint>();
        private static List<PlayerSpawnPoint> islandSpawnPoints = new List<PlayerSpawnPoint>();
        private static int nextVanIndex = 0;
        private static int nextIslandIndex = 0;
        
        [SerializeField] private int spawnIndex = -1;
        [SerializeField] private SpawnType spawnType = SpawnType.Island;
        
        public SpawnType Type => spawnType;
        
        private void OnEnable()
        {
            var list = spawnType == SpawnType.Van ? vanSpawnPoints : islandSpawnPoints;
            list.Add(this);
            list.Sort((a, b) => a.GetSpawnIndex().CompareTo(b.GetSpawnIndex()));
        }
        
        private void OnDisable()
        {
            var list = spawnType == SpawnType.Van ? vanSpawnPoints : islandSpawnPoints;
            list.Remove(this);
        }
        
        private int GetSpawnIndex()
        {
            return spawnIndex >= 0 ? spawnIndex : transform.GetSiblingIndex();
        }
        
        private static void EnsurePopulated(SpawnType type)
        {
            var list = type == SpawnType.Van ? vanSpawnPoints : islandSpawnPoints;
            if (list.Count == 0)
            {
                PlayerSpawnPoint[] found = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
                foreach (var sp in found)
                {
                    if (sp.spawnType == type && !list.Contains(sp))
                    {
                        list.Add(sp);
                    }
                }
                list.Sort((a, b) => a.GetSpawnIndex().CompareTo(b.GetSpawnIndex()));
            }
        }
        
        public static PlayerSpawnPoint GetNextVanSpawnPoint()
        {
            EnsurePopulated(SpawnType.Van);
            if (vanSpawnPoints.Count == 0)
            {
                return null;
            }
            PlayerSpawnPoint point = vanSpawnPoints[nextVanIndex % vanSpawnPoints.Count];
            nextVanIndex++;
            return point;
        }

        public static PlayerSpawnPoint GetNextIslandSpawnPoint()
        {
            EnsurePopulated(SpawnType.Island);
            if (islandSpawnPoints.Count == 0)
            {
                Debug.LogWarning("PlayerSpawnPoint: No island spawn points found in scene");
                return null;
            }
            PlayerSpawnPoint point = islandSpawnPoints[nextIslandIndex % islandSpawnPoints.Count];
            nextIslandIndex++;
            return point;
        }
        
        public static void ResetSpawnIndex()
        {
            nextVanIndex = 0;
            nextIslandIndex = 0;
            vanSpawnPoints.Clear();
            islandSpawnPoints.Clear();
        }

        public static PlayerSpawnPoint GetVanSpawnPoint(int index)
        {
            EnsurePopulated(SpawnType.Van);
            if (vanSpawnPoints.Count == 0)
            {
                Debug.LogWarning("PlayerSpawnPoint: No van spawn points found in scene");
                return null;
            }
            return vanSpawnPoints[index % vanSpawnPoints.Count];
        }
        
        public static PlayerSpawnPoint GetIslandSpawnPoint(int index)
        {
            EnsurePopulated(SpawnType.Island);
            if (islandSpawnPoints.Count == 0)
            {
                Debug.LogWarning("PlayerSpawnPoint: No island spawn points found in scene");
                return null;
            }
            return islandSpawnPoints[index % islandSpawnPoints.Count];
        }
        
        public static List<PlayerSpawnPoint> GetAllVanSpawnPoints()
        {
            EnsurePopulated(SpawnType.Van);
            return new List<PlayerSpawnPoint>(vanSpawnPoints);
        }
        
        public static List<PlayerSpawnPoint> GetAllIslandSpawnPoints()
        {
            EnsurePopulated(SpawnType.Island);
            return new List<PlayerSpawnPoint>(islandSpawnPoints);
        }
        
        private void OnDrawGizmos()
        {
            if (spawnType == SpawnType.Van)
            {
                Gizmos.color = Color.green;
            }
            else
            {
                Gizmos.color = Color.blue;
            }
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
            
#if UNITY_EDITOR
            string label = spawnType == SpawnType.Van ? "Van" : "Island";
            UnityEditor.Handles.Label(transform.position + Vector3.up, $"{label} Spawn {GetSpawnIndex()}");
#endif
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            vanSpawnPoints = new List<PlayerSpawnPoint>();
            islandSpawnPoints = new List<PlayerSpawnPoint>();
            nextVanIndex = 0;
            nextIslandIndex = 0;
        }
    }
}
