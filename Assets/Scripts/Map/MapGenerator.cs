using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class MapGenerator : MonoBehaviour
{
    public int numberOfEyes;

    // List to hold references to created arenas/eyes
    private List<StormEye> arenas = new List<StormEye>();

    // List to hold references to created paths
    private List<Path> paths = new List<Path>();

    class StormEye
    {
        public Vector3 position;
        public GameObject gameObjectRef;

        public StormEye(Vector3 pos, GameObject objRef)
        {
            position = pos;
            gameObjectRef = objRef;
        }
    }

    class Path
    {
        public GameObject arenaA;
        public GameObject arenaB;
        public GameObject gameObjectRef;

        public Path(GameObject a, GameObject b, GameObject objRef)
        {
            arenaA = a;
            arenaB = b;
            gameObjectRef = objRef;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GenerateMap();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void GenerateMap()
    {
        // Create arenas at random positions between 0,0,0 and 500,30,500 for storm eyes
        for (int i = 0; i < numberOfEyes; i++)
        {
            CreateArena(Vector3.zero, new Vector3(500,30,500), i.ToString());
        }

        // Create paths between arenas
        int pathCount = 0;
        for (int i = 0; i < arenas.Count; i++)
        {
            for (int j = i + 1; j < arenas.Count; j++)
            {
                CreatePath(arenas[i].gameObjectRef, arenas[j].gameObjectRef, pathCount.ToString());
                pathCount++;
            }
        }

    }

    // Creates an arena at the specified location, specific version!
    // Overload below that does a random position
    void CreateArena(Vector3 inputPos, String numberforname = "")
    {
        // TEMPORARY! Replace cylinders with prefabs of premade arenas and stuff
        

        // Make sure the arena does not overlap with existing arenas
        int maxIterations = 100; // Prevent infinite loops
        while (true) {
            // Create a new arena as a cube primitive GameObject
            GameObject arena = GameObject.CreatePrimitive(PrimitiveType.Cube);

            // Set transform
            arena.transform.position = inputPos;
            arena.transform.localScale = new Vector3(30, 1, 30);
            
            // Add a Rigidbody to make it interact with physics
            arena.AddComponent<Rigidbody>();
            arena.GetComponent<Rigidbody>().isKinematic = true;
            
            // Check if the new arena is too close to a previous one
            // Use an OverlapBox to detect collisions
            // Do not let arenas spawn on top of each other
            Collider[] colliders = Physics.OverlapBox(arena.transform.position, new Vector3(arena.transform.localScale.x, arena.transform.localScale.y * 100, arena.transform.localScale.z), arena.transform.rotation);
            if (colliders.Length > 1) // More than one collider means overlap
            {
                Destroy(arena); // Remove the overlapping arena
                // Generate a new random position
                inputPos = new Vector3(Random.Range(0, 500), Random.Range(0, 0), Random.Range(0, 500));

                maxIterations--;
                if (maxIterations <= 0)
                {
                    Debug.LogWarning("Max iterations reached while trying to place an arena. Some arenas may overlap.");
                    return;
                }
                
                continue; // Retry with the new position
            }
            else
            {
                if (!string.IsNullOrEmpty(numberforname))
                {
                    arena.name = "Arena_" + numberforname;
                }
                // Create a StormEye instance to hold the arena's data
                StormEye arenaData = new StormEye(arena.transform.position, arena);
                arenas.Add(arenaData); // Store reference to the created arena
                return; // No overlap, exit the function
            }
        }
    }

    // Overload of CreateArena that takes in Vector3 min and max for a random position,
    // Then calls the original version on a random position within the box created by the min and max
    void CreateArena(Vector3 min, Vector3 max, String numberForName)
    {
        CreateArena(
            new Vector3(Random.Range(min.x, max.x), Random.Range(min.y, max.y), Random.Range(min.z, max.z)),
            numberForName
            );
    }

    void CreatePath(GameObject arenaA, GameObject arenaB, String numberforname = "")
    {
        // Store a vector between the two arenas
        Vector3 pathVector = arenaB.transform.position - arenaA.transform.position;

        // Make a primitive cube to represent the path
        GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
        path.transform.position = arenaA.transform.position + pathVector / 2; // Position it halfway between the two arenas
        path.transform.localScale = new Vector3(5, 1, pathVector.magnitude); // Scale it to the distance between arenas
        path.transform.rotation = Quaternion.LookRotation(pathVector); // Rotate it to face the other arena

        // Add a Rigidbody to make it interact with physics
        path.AddComponent<Rigidbody>();
        path.GetComponent<Rigidbody>().isKinematic = true;

        // Give the path a Path tag
        path.tag = "Path";

        if (!string.IsNullOrEmpty(numberforname))
        {
            path.name = "Path_" + numberforname;
        }

        // Check if the new path overlaps with any existing arenas, if so remove it
        // Let the localscale x be wider so that paths dont get too close to arenas
        Collider[] colliders = Physics.OverlapBox(path.transform.position, new Vector3(path.transform.localScale.x, path.transform.localScale.y * 100, path.transform.localScale.z / 2), path.transform.rotation);
        for (int i = 0; i < colliders.Length; i++)
        {
            // Check if the collider belongs to an arena
            for (int j = 0; j < arenas.Count; j++)
            {
                if (colliders[i].gameObject == arenas[j].gameObjectRef)
                {
                    // Check that the overlapped arena is not one of the two this path connects
                    if (arenas[j].gameObjectRef == arenaA || arenas[j].gameObjectRef == arenaB)
                    {
                        continue; // It's one of the connected arenas, so it's fine
                    }
                    
                    Destroy(path); // Remove the overlapping path
                    return; // Exit the function, this path is invalid
                }
            }

            // If it's a path overlapping with another path, it's also invalid
            if (colliders[i].gameObject.tag == "Path")
            {
                // Check that the overlapped path doesn't share an arena with this path
                Path overlappedPath = null;
                foreach (Path p in paths)
                {
                    if (p.gameObjectRef == colliders[i].gameObject)
                    {
                        overlappedPath = p;
                        break;
                    }
                }

                if (overlappedPath != null)
                {
                    if (overlappedPath.arenaA == arenaA || overlappedPath.arenaA == arenaB ||
                        overlappedPath.arenaB == arenaA || overlappedPath.arenaB == arenaB)
                    {
                        continue; // It's connected to one of the same arenas, so it's fine
                    }
                    else
                    {
                        // Prioritize shorter paths
                        float thisPathLength = path.transform.localScale.z;
                        float otherPathLength = overlappedPath.gameObjectRef.transform.localScale.z;

                        if (thisPathLength < otherPathLength)
                        {
                            Destroy(overlappedPath.gameObjectRef);
                            paths.Remove(overlappedPath);
                        }
                        else
                        {
                            Destroy(path);
                            return;
                        }
                    }
                }
            }
        }

        // Create a Path instance to hold the path's data
        Path pathData = new Path(arenaA, arenaB, path);

        paths.Add(pathData); // Store reference to the created path
    }
}
