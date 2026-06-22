using System.Collections.Generic;
using Category5.Enemies;
using UnityEngine;
using Category5.MapEnums;
using UnityEngine.AI;

public class Island : MonoBehaviour
{
    [System.Serializable]
    public struct SpawnerData
    {
        public Transform spawnerMarker; // The position and rotation of the spawner, set in the inspector
        public Vector3 spawnerBounds;
        public TriggerVolume trigger; // Placed on the island prefab and set in the inspector
    };
    [SerializeField] public SpawnerData[] spawnerDataArray;

    [SerializeField] public IslandTag[] islandTags;

    [SerializeField] int numberOfEdgePoints = 10;
    // List of the island's perimeter points, set in the inspector. These are used for pathfinding and wind tunnel generation
    public List<Transform> edgePoints = new List<Transform>();


    // Get the edge point on this island that is facing the other island
    public Vector3 GetPointFacing(Island other)
    {
        // Get the direction from this island to the other island
        Vector3 dir =
            (other.transform.position -
            transform.position).normalized;

        Transform best = null;
        float bestDot = -999f;
        // Loop through all the edge points and find the one whose direction is closest to the direction to the other island
        foreach (Transform p in edgePoints)
        {
            Vector3 pointDir =
                (p.position - transform.position).normalized;

            float dot =
                Vector3.Dot(pointDir, dir);

            if (dot > bestDot)
            {
                bestDot = dot;
                best = p;
            }
        }

        // Use the closest point to sample the navmesh and get a valid walkable point
        NavMeshHit hit;
        if (NavMesh.SamplePosition(best.position, out hit, 1.0f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        // If the navmesh didn't work, return the best point for now (MAKE THIS BETTER LATER)
        return best.position;
    }

     private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        foreach (Transform point in edgePoints)
        {
            Gizmos.DrawWireSphere(point.position, 1f);
        }
    }
}
