using System;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Linq;

public class MapGenerator : MonoBehaviour
{
    // CURRENT ISSUES!
    /*
        - Arenas can still be generated with some overlap. Like two might be in a row from another arena, but they're still
        the closest 2, so the paths end up kind of overlapping (Check for minimum distance between arenas, redo the random generation if they're too close)
        - Paths Can get too close together (REJECTION CHECK. KEEP ALL OTHER POINTS TO CHECK FOR DISTANCE)
        - Path entrances can get crowded, and maybe want some random not quite closest point entrances (Add random offset within += angle range of the closest point, then use raycast from inside the arena to the collider to get a point)
    */


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

    // Mesh to generate along paths
    [SerializeField] Mesh pathMesh;

    class Arena
    {
        public Vector3 position;
        public GameObject gameObjectRef;

        // Stores if this arena is an eye or not, players can drop into eyes
        public bool isEye;
        // Stores if the arena is a "hidden" arena. Hidden arenas will be initially inaccessible, and
        // paths connected to them will also be hidden
        public bool isHidden;
        // Arenas have a capsule collider surrounding them that defines
        // the arena's boundaries (The storm cloud walls)
        public Collider arenaBounds;

        public Arena(Vector3 pos, GameObject objRef)
        {
            position = pos;
            gameObjectRef = objRef;
            // Arenas are not eyes by default. If they are set to true it's in GenerateMap()
            isEye = false;

            arenaBounds = gameObjectRef.GetComponent<CapsuleCollider>();
        }
    }

    class Path
    {
        public Arena arenaA;
        public Arena arenaB;
        public GameObject gameObjectRef;

        public bool isHidden;

        public Path(Arena a, Arena b, GameObject objRef)
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
            CreatePath(arenas[i], closestArena, pathCount.ToString());
            pathCount++;
            CreatePath(arenas[i], secondClosestArena, pathCount.ToString());
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

    // Creates a path between two given arenas
    void CreatePath(Arena arenaA, Arena arenaB, String numberforname = "")
    {
        // Checks if the path is valid
        // Path to same arena?
        if (arenaA == arenaB)
        {
            Debug.LogWarning("Attempted to create a path between the same arena. Path creation aborted.");
            return; // Do not create a path between the same arena
        }
        // Path to null arena?
        if (arenaA == null || arenaB == null)
        {
            Debug.LogWarning("Attempted to create a path with a null arena reference. Path creation aborted.");
            return; // Do not create a path if either arena reference is null
        }
        // Path already exists?
        foreach (Path path in paths)
        {
            if ((path.arenaA == arenaA && path.arenaB == arenaB) || (path.arenaA == arenaB && path.arenaB == arenaA))
            {
                Debug.LogWarning("Attempted to create a duplicate path between " + arenaA.gameObjectRef.name + " and " + arenaB.gameObjectRef.name + ". Path creation aborted.");
                return; // Do not create a duplicate path
            }
        }

        /*
            Make spline container
            Add spline component to it
            Add two spline points to the spline component, set their positions to the centers of the two arenas
        */
        // Create a gameobject with a splinecontainer component
        SplineContainer splineContainer = new GameObject("Path_" + numberforname).AddComponent<SplineContainer>();

        // Give the path a Path tag
        splineContainer.tag = "Path";
        if (!string.IsNullOrEmpty(numberforname))
        {
            splineContainer.name = "Path_" + numberforname;
        }

        // Create a spline to be held by the container
        Spline spline = splineContainer.Spline;

        // Get the points on each arena's bounds collider closest to the other arena
        // These will be the start and end points of the path
        Vector3 pointOnA = arenaA.arenaBounds.ClosestPoint(arenaB.position);
        Vector3 pointOnB = arenaB.arenaBounds.ClosestPoint(pointOnA);
        // Make sure the points are at the level of the arena
        pointOnA = new Vector3(pointOnA.x, arenaA.position.y, pointOnA.z);
        pointOnB = new Vector3(pointOnB.x, arenaB.position.y, pointOnB.z);

        // Add points to the spline at the positions of the two arenas
        spline.Add(new BezierKnot(pointOnA), TangentMode.AutoSmooth); // Start pos
        spline.Add(new BezierKnot(pointOnB), TangentMode.AutoSmooth); // End pos
        // Save points to be added later after random curves
        Vector3 pointBeforeA = splineContainer.EvaluatePosition(spline, .13f);
        Vector3 pointBeforeB = splineContainer.EvaluatePosition(spline, .87f);

        // Get the vector from arena to arena
        Vector3 betweenArenaVector = pointOnB - pointOnA;

        Debug.Log(betweenArenaVector.magnitude + " " + splineContainer.name);

        // the max number of bends/curves in the path
        int maxCurves = 0;

        if (betweenArenaVector.magnitude <= 50) maxCurves = 1;
        else if (betweenArenaVector.magnitude <= 100) maxCurves = 2;
        else if (betweenArenaVector.magnitude <= 150) maxCurves = 4;
        else maxCurves = 5;

        // Array that stores a tuple of the percentage along the spline and the position given to that knot
        // The length of this array decides how many random curves are added
        (float placeOnSpline, Vector3 position)[] knotPositions = new (float, Vector3)[Random.Range(1, maxCurves)];

        // Add some random curves
        for (int i = 0; i < knotPositions.Length; i++)
        {
            // A percentage of the spline, used by EvaluatePosition
            // to get the position of where that point is along the spline
            float place = Random.Range(0.125f, 0.865f);
            // The position on the spline based on the place value
            Vector3 midPos = splineContainer.EvaluatePosition(spline, place);

            // Get a vector perpedicular to the spline's x and z
            Vector3 moveVector = Vector3.zero;
            int tempRand = Random.Range(0, 2);
            if (tempRand == 0)
            {
                moveVector = new Vector3(-betweenArenaVector.z, 0, betweenArenaVector.x).normalized;   
            }
            else
            {
                moveVector = new Vector3(betweenArenaVector.z, 0, -betweenArenaVector.x).normalized;
            }

            // Move the position using the vector, random magnitude
            midPos += moveVector * Random.Range(30, 50);

            // Add the place on spline and the position into the newKnotPositions array
            knotPositions[i] = (place, midPos);
        }

        // Sort the knot positions by their place along the spline
        knotPositions = knotPositions.OrderBy(p => p.placeOnSpline).ToArray();
        
        // Since inserting the knots into the spline changes it's shape, we put them in after so that
        // we can get a path without too crazy of a shape
        for (int i = 0; i < knotPositions.Length; i++)
        {
            // Insert the new knot on the spline
            spline.Insert(spline.Count - 1, knotPositions[i].position, TangentMode.Mirrored);
        }

        // Add points to the spline before the end points to point the entrances to the
        // path at the arenas
        spline.Insert(1, new BezierKnot(pointBeforeA), TangentMode.AutoSmooth); // Start pos
        spline.Insert(spline.Count-1, new BezierKnot(pointBeforeB), TangentMode.AutoSmooth); // End pos
        
        // Clean up knots that are too sharp
        // Shouldn't take more than 50 tries
        int attempts = 0;
        bool noProblemKnots = false;
        // Also check if there were no problem knots, just exit if there weren't
        while (attempts < 50 && noProblemKnots == false)
        {
            // Set this to true before checking
            noProblemKnots = true;

            // Skip the end point knots, start at 1 and end at Count-1
            for (int i = 1; i < spline.Count-1; i++)
            {
                // Get how small the knot's angle is, it's a magnitude though not an angle
                float tangentLength = math.length(spline[i].TangentOut);
                if (tangentLength <= 12f)
                {
                    // If we find a problem knot
                    Debug.Log("REMOVING " + spline[i] + " AT " + spline[i].Position + " ON " + splineContainer.name);
                    // There ARE problem knots
                    noProblemKnots = false;
                    // Remove the problem knot
                    spline.RemoveAt(i);
                    // Exit this loop since we changed the thing being iterated over
                    break;
                }
            }
            attempts++;
        }
        
        
        // Add a mesh to this path
        CreatePathMesh(splineContainer);

        // Create a Path instance to hold the path's data
        Path pathData = new Path(arenaA, arenaB, splineContainer.gameObject);

        paths.Add(pathData); // Store reference to the created path
    }


    // Adds the mesh to the input path game object
    void CreatePathMesh(SplineContainer container)
    {
        // Temporary implementation, just used to make a visible path rn!!

        SplineExtrude splineExtrude = container.gameObject.AddComponent<SplineExtrude>();
        splineExtrude.Container = container;

        var hasMeshFilter = container.gameObject.TryGetComponent<MeshFilter>(out var meshFilter);
        if (hasMeshFilter)
        {
            if (meshFilter.sharedMesh == null)
            {
                var extrudeMesh = new Mesh();
                extrudeMesh.name = "Spline Extrude Mesh";
                meshFilter.sharedMesh = extrudeMesh;
            }
            // Set the mesh variables
            splineExtrude.Radius = 10;
            splineExtrude.FlipNormals = true;

            var hasMeshRenderer = container.gameObject.TryGetComponent<MeshRenderer>(out var meshRenderer);
            if (hasMeshRenderer)
                meshRenderer.material = new Material(Shader.Find("Standard"));
        }

        // For some reason the mesh doesn't show unless you mess with the component in the editor,
        // or if you turn it off and on here, so that's what this is for
        splineExtrude.enabled = false;
        splineExtrude.enabled = true;
    }

    void OnDrawGizmos()
    {
        
    }
}
