using UnityEngine;
using UnityEditor;
using System;

[CustomEditor(typeof(Island))]
public class IslandEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Island island = (Island)target;

        if(GUILayout.Button("Generate Edge Points"))
        {
            GeneratePoints(island);
        }
    }

    static void GeneratePoints(Island island)
    {
        // Grab components we need
        MeshRenderer renderer = island.GetComponentInChildren<MeshRenderer>();
        Collider collider = island.GetComponentInChildren<Collider>();

        if (renderer == null || collider == null) return;

        // If there's already a child object named "EdgePoints", delete it before generating new points
        Transform parent = island.transform.Find("EdgePoints");

        if (parent != null) UnityEngine.Object.DestroyImmediate(parent.gameObject);
        // Create a new parent object to hold the edge points
        parent = new GameObject("EdgePoints").transform;
        parent.SetParent(island.transform);

        Bounds bounds = renderer.bounds;

        Vector3 center = bounds.center;

        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.5f;

        // Number of points that will be generated around the perimeter
        int samples = island.numberOfEdgePoints;
        // Loop through each point, get the direction from the center
        for (int i = 0; i < samples; i++)
        {
            float angle =
                i * Mathf.PI * 2f / samples;

            Vector3 dir = new Vector3(
                Mathf.Cos(angle),
                0,
                Mathf.Sin(angle));

            Vector3 previousValid = center;

            // Until we hit the end of the bounds, ray cast down over and over to check if there's ground. Once there isn't break out
            for (float d = 0; d < radius; d += radius/50f)
            {
                Vector3 testPos =
                    center + dir * d + Vector3.up * 100f;
                
                if (Physics.Raycast(
                    testPos,
                    Vector3.down,
                    out RaycastHit hit,
                    200f,
                    LayerMask.GetMask("Default"),
                    QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider == collider)
                    {
                        previousValid = hit.point;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            // Move slightly inward from cliff edge to get the perimeter point
            Vector3 edgePoint =
                previousValid - dir * 3f;

            GameObject marker =
                new GameObject($"EdgePoint_{i}");

            marker.transform.position = edgePoint;
            marker.transform.SetParent(parent);
        }
        // Now that we have the points, clear the island script's edge points list and add the points to it
        ApplyPoints(island);
        Debug.Log("Generated edge points.");
    }

    static void ApplyPoints(Island island)
    {
        // If there's already a child object named "EdgePoints", delete it before generating new points
        Transform parent = island.transform.Find("EdgePoints");

        if (parent == null) return;

        // Clear the island script's edge points list and add the points to it
        island.edgePoints.Clear();
        foreach (Transform child in parent)
        {
            island.edgePoints.Add(child);
        }
    }
}
