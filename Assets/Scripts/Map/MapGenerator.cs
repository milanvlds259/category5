using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.Splines;

public class MapGenerator : MonoBehaviour
{
    // Number of arenas that will be created
    public int numberOfArenas;

    // Number of arenas that are eyes
    public int numberOfEyes;

    // min and max positions, all arenas will be spawned at random
    // positions between these Vector3s
    public Vector3 minimumPosition;
    public Vector3 maximumPosition;

    // List to hold references to created arenas/eyes
    private List<Arena> arenas = new List<Arena>();

    // List to hold references to created paths
    private List<Path> paths = new List<Path>();

    class Arena
    {
        public Vector3 position;
        public GameObject gameObjectRef;

        // Stores if this arena is an eye or not, players can drop into eyes
        public bool isEye;
        // Stores if the arena is a "hidden" arena. Hidden arenas will be initially inaccessible, and
        // paths connected to them will also be hidden
        public bool isHidden;

        public Arena(Vector3 pos, GameObject objRef)
        {
            position = pos;
            gameObjectRef = objRef;
            // Arenas are not eyes by default. If they are set to true it's in GenerateMap()
            isEye = false;
        }
    }

    class Path
    {
        public GameObject arenaA;
        public GameObject arenaB;
        public GameObject gameObjectRef;

        public bool isHidden;

        public Path(GameObject a, GameObject b, GameObject objRef)
        {
            arenaA = a;
            arenaB = b;
            gameObjectRef = objRef;
            // Paths are only hidden if they are connected to a hidden arena
            isHidden = false;
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
        // The main boss arena will always be created in the center of the map
        CreateArena(Vector3.zero, "boss", 2f);

        // Create arenas at random positions between the input Vector3s for storm eyes
        for (int i = 0; i < numberOfArenas; i++)
        {
            // Store a boolean for if an arena was successfully created and create an arena
            bool arenaCreated = CreateArena(minimumPosition, maximumPosition, i.ToString());

            int maxIterations = 100; // Prevent infinite loops
            // As long as the arena wasn't created (overlaps), try again
            while (!arenaCreated)
            {
                // (will only try this 100 times before giving up)
                maxIterations--;
                if (maxIterations <= 0)
                {
                    UnityEngine.Debug.LogWarning("Max iterations reached while trying to place an arena. Some arenas may overlap.");
                    break; // break out of the while loop
                }

                // Try creating the arena again at another random pos
                arenaCreated = CreateArena(minimumPosition, maximumPosition, i.ToString());
            }
        }

        // Number of eyes cannot exceed number of arenas, and cannot be < 0
        numberOfEyes = Math.Clamp(numberOfEyes, 0, arenas.Count);
        // Assign arenas to be the storm's eyes (points where players can drop in)
        for (int i = 0; i < numberOfEyes; i++)
        {
            arenas[i].isEye = true;
        }
        

        // Create paths between arenas
        int pathCount = 0;
        // Loop through each arena here
        for (int i = 0; i < arenas.Count; i++)
        {
            Arena closestArena = null;
            Arena secondClosestArena = null;
            // Loop through all arenas again here
            for (int j = 0; j < arenas.Count; j++)
            {
                if (i == j) continue; // Skip if it's the same arena

                // Check for the arenas with the shortest distance between them, 
                // and create paths between them until each arena has at least 2 paths, 
                // or there are no more arenas within a certain distance threshold
                // Get distance between the two arenas
                float distance = Vector3.Distance(arenas[i].position, arenas[j].position);
                if (closestArena == null || distance < Vector3.Distance(arenas[i].position, closestArena.position))
                {
                    secondClosestArena = closestArena;
                    closestArena = arenas[j];
                }
                else if (secondClosestArena == null || distance < Vector3.Distance(arenas[i].position, secondClosestArena.position))
                {
                    secondClosestArena = arenas[j];
                }
                
            }
            CreatePath(arenas[i].gameObjectRef, closestArena.gameObjectRef, pathCount.ToString());
            pathCount++;
            CreatePath(arenas[i].gameObjectRef, secondClosestArena.gameObjectRef, pathCount.ToString());
            pathCount++;
        }

    }

    // Creates an arena at the specified location, specific location version!
    // Overload below that does a random position
    bool CreateArena(Vector3 inputPos, String numberforname = "", float scaleFactor=1f)
    {
        // TEMPORARY! Replace basic shapes with prefabs of premade arenas and stuff
        
        // Create a new arena as a cube primitive GameObject
        GameObject arena = GameObject.CreatePrimitive(PrimitiveType.Cube);

        // Set transform
        arena.transform.position = inputPos;
        arena.transform.localScale = new Vector3(
                                            60*scaleFactor,
                                            2*scaleFactor,
                                            60*scaleFactor
                                            );
        
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

            // Return false, the arena wasn't created
            return false;
        }
        else
        {
            // Add a capsule collider to define the bounds of the arena
            CapsuleCollider collider = arena.AddComponent<CapsuleCollider>();
            collider.radius = arena.transform.localScale.x / (scaleFactor * 60); // Set the radius
            collider.height = 100f; // Set the height
            collider.center = new Vector3(0, 10, 0); // Center the collider on the arena
            collider.isTrigger = true; // Set the collider to be a trigger so players can fall through
            
            if (!string.IsNullOrEmpty(numberforname))
            {
                arena.name = "Arena_" + numberforname;
            }
            // Create an Arena instance to hold the arena's data
            Arena arenaData = new Arena(arena.transform.position, arena);
            arenas.Add(arenaData); // Store reference to the created arena
            return true; // No overlap, return true
        }
    }

    // Overload of CreateArena that takes in Vector3 min and max for a random position,
    // Then calls the original version on a random position within the box created by the min and max
    bool CreateArena(Vector3 min, Vector3 max, String numberForName)
    {
        // Create the arena, and check if the arena was successfully created
        if ( CreateArena(
            new Vector3(Random.Range(min.x, max.x), Random.Range(min.y, max.y), Random.Range(min.z, max.z)),
            numberForName
            ) )
        {
            // If it was return true
            return true;
        }
        else
        {
            // If not return false
            return false;
        }
    }

    void CreatePath(GameObject arenaA, GameObject arenaB, String numberforname = "")
    {
        // Checks if the path is valid
        // Path to same arena?
        if (arenaA == arenaB)
        {
            UnityEngine.Debug.LogWarning("Attempted to create a path between the same arena. Path creation aborted.");
            return; // Do not create a path between the same arena
        }
        // Path to null arena?
        if (arenaA == null || arenaB == null)
        {
            UnityEngine.Debug.LogWarning("Attempted to create a path with a null arena reference. Path creation aborted.");
            return; // Do not create a path if either arena reference is null
        }
        // Path already exists?
        foreach (Path path in paths)
        {
            if ((path.arenaA == arenaA && path.arenaB == arenaB) || (path.arenaA == arenaB && path.arenaB == arenaA))
            {
                UnityEngine.Debug.LogWarning("Attempted to create a duplicate path between " + arenaA.name + " and " + arenaB.name + ". Path creation aborted.");
                return; // Do not create a duplicate path
            }
        }

        /*
            Make spline container
            Add spline component to it
            Add two spline points to the spline component, set their positions to the centers of the two arenas
        */
        SplineContainer splinePath = new GameObject("Path_" + numberforname).AddComponent<SplineContainer>();
        Spline spline = splinePath.Spline;
        spline.Add(new BezierKnot(arenaA.transform.position));
        spline.Add(new BezierKnot(arenaB.transform.position));

        /*
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
        */
        // Give the path a Path tag
        splinePath.tag = "Path";

        // Create a Path instance to hold the path's data
        Path pathData = new Path(arenaA, arenaB, splinePath.gameObject);

        paths.Add(pathData); // Store reference to the created path
    }

    void OnDrawGizmos()
    {
        
    }
}
