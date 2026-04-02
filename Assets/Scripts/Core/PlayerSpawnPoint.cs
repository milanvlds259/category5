using UnityEngine;
using System.Collections.Generic;

namespace Category5.Core
{
    public class PlayerSpawnPoint : MonoBehaviour
    {
        // static list of all spawn points in the scene
        private static List<PlayerSpawnPoint> spawnPoints = new List<PlayerSpawnPoint>();
        private static int nextSpawnIndex = 0;
        
        [SerializeField] private int spawnIndex = -1; // -1 means auto-assign
        
        private void OnEnable()
        {
            spawnPoints.Add(this);
            // sort by spawn index for consistent ordering
            spawnPoints.Sort((a, b) => a.GetSpawnIndex().CompareTo(b.GetSpawnIndex()));
        }
        
        private void OnDisable()
        {
            spawnPoints.Remove(this);
        }
        
        private int GetSpawnIndex()
        {
            return spawnIndex >= 0 ? spawnIndex : transform.GetSiblingIndex();
        }
        
        // ensures spawn points are populated, finding them if needed
        private static void EnsureSpawnPointsPopulated()
        {
            if (spawnPoints.Count == 0)
            {
                // fallback: find all spawn points in the scene
                PlayerSpawnPoint[] found = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
                foreach (var sp in found)
                {
                    if (!spawnPoints.Contains(sp))
                    {
                        spawnPoints.Add(sp);
                    }
                }
                
                // sort by spawn index for consistent ordering
                spawnPoints.Sort((a, b) => a.GetSpawnIndex().CompareTo(b.GetSpawnIndex()));
                
                // Debug.Log($"PlayerSpawnPoint: Found {spawnPoints.Count} spawn points via fallback search");
            }
        }
        
        // gets the next available spawn point
        public static PlayerSpawnPoint GetNextSpawnPoint()
        {
            EnsureSpawnPointsPopulated();
            
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning("PlayerSpawnPoint: No spawn points found in scene");
                return null;
            }
            
            PlayerSpawnPoint point = spawnPoints[nextSpawnIndex % spawnPoints.Count];
            nextSpawnIndex++;
            return point;
        }
        
        // resets the spawn index, call when starting a new game
        public static void ResetSpawnIndex()
        {
            nextSpawnIndex = 0;
        }
        
        // gets a specific spawn point by index
        public static PlayerSpawnPoint GetSpawnPoint(int index)
        {
            EnsureSpawnPointsPopulated();
            
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning("PlayerSpawnPoint: No spawn points found in scene");
                return null;
            }
            
            return spawnPoints[index % spawnPoints.Count];
        }
        
        // gets all spawn points
        public static List<PlayerSpawnPoint> GetAllSpawnPoints()
        {
            return new List<PlayerSpawnPoint>(spawnPoints);
        }
        
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
            
            // draw spawn index label
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up, $"Spawn {GetSpawnIndex()}");
#endif
        }
        
        // reset statics on domain reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            spawnPoints = new List<PlayerSpawnPoint>();
            nextSpawnIndex = 0;
        }
    }
}
