using System;
using System.Collections.Generic;
using System.Collections;
using Debug = UnityEngine.Debug;
using Vector3 = UnityEngine.Vector3;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Linq;
using System.Numerics;
using Category5.Player.WindRiding;
using Category5.Enemies;
using Unity.VisualScripting;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Unity.Netcode;

public class MapGenerator : NetworkBehaviour
{
    // CURRENT ISSUES!
    /*
        - Reposition Entrances not working
        - Paths can go over arenas
        - Path spacing not working
        - Want to make paths only move points side to side?
    */

    // Referemce to the generated map
    private GameObject mapParent;

    // Number of arenas that will be created
    public int numberOfArenas;

    // Number of arenas that are eyes
    public int numberOfEyes;

    // min and max positions, all arenas will be spawned at random
    // positions between these Vector3s
    public Vector3 minBounds;
    public Vector3 maxBounds;

    // List to hold references to created arenas/eyes
    private List<Arena> arenas = new List<Arena>();
    // Layer mask used in arena generation so that they don't generate overlapping
    [SerializeField] LayerMask arenaMask;

    // List to hold references to created paths
    private List<Path> paths = new List<Path>();

    // Keep track of all path points to make sure they're not too close together
    private List<BezierKnot> pathMidpoints = new List<BezierKnot>();

    [SerializeField] Material cloudMaterial;

    // Mesh to generate along paths
    [SerializeField] Mesh pathMesh;

    [SerializeField] GameObject enemySpawnerPrefab;

    class Arena
    {
        public Vector3 position;
        public float scaleFactor;
        public GameObject gameObjectRef;

        // Stores if this arena is an eye or not, players can drop into eyes
        public bool isEye;
        // Stores if the arena is a "hidden" arena. Hidden arenas will be initially inaccessible, and
        // paths connected to them will also be hidden
        public bool isHidden;
        // Arenas have a capsule collider surrounding them that defines
        // the arena's boundaries (The storm cloud walls)
        public CapsuleCollider arenaBounds;

        public TriggerVolume trigger;

        public Arena(Vector3 pos, GameObject objRef, float scale)
        {
            position = pos;
            gameObjectRef = objRef;
            scaleFactor = scale;

            // Arenas are not eyes by default. If they are set to true it's in GenerateMap()
            isEye = false;

            arenaBounds = gameObjectRef.GetComponent<CapsuleCollider>();
            trigger = gameObjectRef.GetComponent<TriggerVolume>();
        }
    }

    class Path
    {
        public Arena arenaA;
        public Arena arenaB;
        public GameObject gameObjectRef;

        public bool isHidden;

        // The spline that makes up the physical object of this path
        public Spline spline;

        public Path(Arena a, Arena b, GameObject objRef)
        {
            arenaA = a;
            arenaB = b;
            gameObjectRef = objRef;
            spline = gameObjectRef.GetComponent<SplineContainer>().Spline;
            // Paths are only hidden if they are connected to a hidden arena
            isHidden = false;
        }
    }

    /*
    void Start()
    {
        // Make sure there's no map existing when generating on start
        DeleteMap();
        StartCoroutine(GenerateMap());
    }*/

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Make sure there's no map existing when generating on start
        DeleteMap();
        GenerateMap();
    }

    // Deletes the current map and clears the lists
    public void DeleteMap()
    {
        if (mapParent != null) {
            DestroyImmediate(mapParent);
        }
        arenas.Clear();
        paths.Clear();
    }

    // Randomly generates a map
    public void GenerateMap()
    {
        mapParent = new GameObject("Map");


        // The main boss arena will always be created in the center of the map
        CreateArena(Vector3.zero, mapParent.transform, "boss", 2f);

        // Create arenas at random positions between the input Vector3s for storm eyes
        for (int i = 0; i < numberOfArenas; i++)
        {
            // Store a boolean for if an arena was successfully created and create an arena
            Arena arenaCreated = CreateArena(minBounds, maxBounds, i.ToString(), mapParent.transform);

            int maxIterations = 100; // Prevent infinite loops
            // As long as the arena wasn't created (overlaps), try again
            while (arenaCreated == null)
            {
                // (will only try this 100 times before giving up)
                maxIterations--;
                if (maxIterations <= 0)
                {
                    UnityEngine.Debug.LogWarning("Max iterations reached while trying to place an arena. Some arenas may overlap.");
                    break; // break out of the while loop
                }

                // Try creating the arena again at another random pos
                arenaCreated = CreateArena(minBounds, maxBounds, i.ToString(), mapParent.transform);
            }
            // Add an enemy spawner to the created arena
            AddEnemySpawnerToArena(arenaCreated);
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
            CreatePath(arenas[i], closestArena, mapParent.transform, pathCount.ToString());
            pathCount++;
            CreatePath(arenas[i], secondClosestArena, mapParent.transform, pathCount.ToString());
            pathCount++;
        }
        // Reposition the path entrances away from each
        // other to reduce crowding
        foreach (Path path in paths)
        {
            RepositionEntrance(path, "A");
            RepositionEntrance(path, "B");
        }

        foreach (Path path in paths)
        {
            AddPathMidpoints(path.gameObjectRef.GetComponent<SplineContainer>());
        }
        
        
        // Space out all the path points so they dont overlap!
        SpaceOutPaths();

        if (Application.isPlaying) {
            // Add the wind tunnel component and launch pads to each path
            AddWindTunnelToPaths();
            // Add navmesh surfaces to all arenas
            StartCoroutine(AddNavMeshSurfaceToArenas());
        }
    }


    // Creates an arena at the specified location, specific location version!
    // Overload below that does a random position
    Arena CreateArena(Vector3 inputPos, Transform parent, String numberforname = "", float scaleFactor=1f)
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
        // The radius is a little bigger than the arena's actual size to prevent them from being too close, 
        // since the paths will be generated from the edges of the arenas
        Collider[] colliders = Physics.OverlapCapsule(arena.transform.position - new Vector3(0, 100, 0), 
                                                    arena.transform.position + new Vector3(0, 100, 0), 
                                                    arena.transform.localScale.x * 2, 
                                                    arenaMask, 
                                                    QueryTriggerInteraction.Collide
                                                    );
        if (colliders.Length > 1) // More than one collider means overlap
        {
            DestroyImmediate(arena); // Remove the overlapping arena

            // Return null, the arena wasn't created
            return null;
        }
        else
        {
            // Add a capsule collider to define the bounds of the arena
            CapsuleCollider collider = arena.AddComponent<CapsuleCollider>();
            collider.radius = arena.transform.localScale.x / (scaleFactor * 60) + 0.25f; // Set the radius
            collider.height = 100f; // Set the height
            collider.center = new Vector3(0, 10, 0); // Center the collider on the arena
            collider.isTrigger = true; // Set the collider to be a trigger so players can fall through

            // Add TriggerVolume script that will invoke an event when that capsule
            // collider trigger is entered. This will automatically get the capsule trigger collider
            TriggerVolume trigger = arena.AddComponent<TriggerVolume>();
            trigger.targetLayers = LayerMask.GetMask("Player");
            trigger.targetTag = "Player";

            // Add cloud layer
            GameObject cloudLayer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(cloudLayer.GetComponent<CapsuleCollider>()); // Remove the cloud layer's collider
            cloudLayer.transform.position = new Vector3(arena.transform.position.x, arena.transform.position.y, arena.transform.position.z);
            cloudLayer.transform.localScale = new Vector3(
                                            collider.radius * 2 * arena.transform.localScale.x,
                                            1f,
                                            collider.radius * 2 * arena.transform.localScale.x
                                            );
            cloudLayer.transform.parent = arena.transform;
            cloudLayer.GetComponent<MeshRenderer>().material = cloudMaterial; // Set the cloud material
            MeshCollider cloudCollider = cloudLayer.AddComponent<MeshCollider>();
            cloudCollider.convex = true; // Set convex to true so it can be a trigger
            cloudCollider.isTrigger = true; // Add a mesh collider and set it to be a trigger so players can fall through
            cloudLayer.layer = 8;
            
            // Set the arena's name and make it a child of the parent param
            if (!string.IsNullOrEmpty(numberforname))
            {
                arena.name = "Arena_" + numberforname;
            }
            arena.transform.parent = parent;

            // Create an Arena instance to hold the arena's data
            Arena arenaData = new Arena(arena.transform.position, arena, scaleFactor);
            arenas.Add(arenaData); // Store reference to the created arena

            //arena.tag = "Arena"; // Set the tag of the arena to "Arena" for easy reference

            return arenaData; // No overlap, return the created arena data
        }
    }

    // Overload of CreateArena that takes in Vector3 min and max for a random position,
    // Then calls the original version on a random position within the box created by the min and max
    // The min and max are the bounds of the area where the arena can spawn
    Arena CreateArena(Vector3 min, Vector3 max, String numberForName, Transform parent)
    {
        // Create the arena, and check if the arena was successfully created
        // This call doesn't pass a scalefactor, so it defaults to 1f
        Arena arenaData = CreateArena(
                                    new Vector3(Random.Range(min.x, max.x), Random.Range(min.y, max.y), Random.Range(min.z, max.z)),
                                    parent,
                                    numberForName
                                 );

        if ( arenaData != null )
        {
            // If it was return the arena data
            return arenaData;
        }
        else
        {
            // If not return null
            return null;
        }
    }

    // Adds an enemy spawner to the given arena, making it a child of the arena and setting the bounds of the enemy spawns
    void AddEnemySpawnerToArena(Arena arena)
    {
        GameObject spawnerObj = Instantiate(enemySpawnerPrefab);
        
        EnemySpawner spawner = spawnerObj.GetComponent<EnemySpawner>();
        spawner.spawnBounds = new Vector3(arena.gameObjectRef.transform.localScale.x, 0, arena.gameObjectRef.transform.localScale.z);
        // Here set the spawner to only start spawning using the triggervolume on this arena
        spawner.autoStartOnSpawn = false;
        spawner.startOnTrigger = true;
        spawner.triggerVolume = arena.trigger;

        spawner.GetComponent<NetworkObject>().Spawn();

        spawnerObj.transform.parent = arena.gameObjectRef.transform;
        spawnerObj.transform.position = arena.position + new Vector3(0, 5f, 0); // Position it a little above the arena
    }

    private IEnumerator AddNavMeshSurfaceToArenas()
    {
        yield return new WaitForEndOfFrame();
       
        NavMeshSurface surface = mapParent.AddComponent<NavMeshSurface>();
        surface.layerMask = LayerMask.GetMask("Default");
        surface.BuildNavMesh();
    }

    // Creates a path between two given arenas
    void CreatePath(Arena arenaA, Arena arenaB, Transform parent, String numberforname = "")
    {
        // Checks if the path is valid
        // Path to same arena?
        if (arenaA == arenaB)
        {
            //Debug.LogWarning("Attempted to create a path between the same arena. Path creation aborted.");
            return; // Do not create a path between the same arena
        }
        // Path to null arena?
        if (arenaA == null || arenaB == null)
        {
            //Debug.LogWarning("Attempted to create a path with a null arena reference. Path creation aborted.");
            return; // Do not create a path if either arena reference is null
        }
        // Path already exists?
        foreach (Path path in paths)
        {
            if ((path.arenaA == arenaA && path.arenaB == arenaB) || (path.arenaA == arenaB && path.arenaB == arenaA))
            {
                //Debug.LogWarning("Attempted to create a duplicate path between " + arenaA.gameObjectRef.name + " and " + arenaB.gameObjectRef.name + ". Path creation aborted.");
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
        // Make sure the points are at the level of the arena (plus a little to be above cloud level)
        pointOnA = new Vector3(pointOnA.x, arenaA.position.y + + 5f, pointOnA.z);
        pointOnB = new Vector3(pointOnB.x, arenaB.position.y + 5f, pointOnB.z);

        BezierKnot Aknot = new BezierKnot(pointOnA);
        BezierKnot Bknot = new BezierKnot(pointOnB);

        // Add points to the spline at the positions of the two arenas
        spline.Add(Aknot, TangentMode.AutoSmooth); // Start pos
        spline.Add(Bknot, TangentMode.AutoSmooth); // End pos
        // Save points to be added later after random curves
        Vector3 pointBeforeA = splineContainer.EvaluatePosition(spline, .13f);
        Vector3 pointBeforeB = splineContainer.EvaluatePosition(spline, .87f);
        // Add points to list that contains all points
        //pathPoints.Add(Aknot);
        //pathPoints.Add(Bknot);

        

        // Add points to the spline before the end points to point the entrances to the
        // path at the arenas
        BezierKnot beforeAknot = new BezierKnot(pointBeforeA);
        BezierKnot beforeBknot = new BezierKnot(pointBeforeB);
        spline.Insert(1, beforeAknot, TangentMode.AutoSmooth); // Start pos
        spline.Insert(spline.Count-1, beforeBknot, TangentMode.AutoSmooth); // End pos

        // Add points to list that contains all points
        //pathPoints.Add(beforeAknot);
        //pathPoints.Add(beforeBknot);
        
        // Calls helper function that removes knots that are too sharp (not working?)
        CleanUpPath(spline, splineContainer);
        
        
        // Add a mesh to this path
        if (Application.isPlaying) CreatePathMesh(splineContainer);

        // Make path a child of the parent
        splineContainer.gameObject.transform.parent = parent;
        // Put path on the cloudSurface layer
        splineContainer.gameObject.layer = 8;

        // Create a Path instance to hold the path's data
        Path pathData = new Path(arenaA, arenaB, splineContainer.gameObject);

        paths.Add(pathData); // Store reference to the created path
    }
    void AddPathMidpoints(SplineContainer splineContainer)
    {
        // Get spline ref
        Spline spline = splineContainer.Spline;
        // Get spline endpoints
        Vector3 pointOnA = spline[0].Position;
        Vector3 pointOnB = spline[spline.Count-1].Position;

        // Get the vector from arena to arena
        Vector3 betweenArenaVector = pointOnB - pointOnA;


        // the number of bends/curves in the path
        int minCurves = 1;
        int maxCurves = 1;
        // The max amplitude of the path curves
        float maxCurveStrength = 1;

        if (betweenArenaVector.magnitude <= 50) {
            maxCurves = 0;
            minCurves = 0;
        }
        else if (betweenArenaVector.magnitude <= 100) {
            maxCurves = 1;
            maxCurveStrength = 5;
        }
        else if (betweenArenaVector.magnitude <= 150) {
            maxCurves = 2;
            maxCurveStrength = 20;
        }
        else {
            maxCurves = 3;
            maxCurveStrength = 30;
        }

        // Array that stores a tuple of the percentage along the spline and the position given to that knot
        // The length of this array decides how many random curves are added
        (float placeOnSpline, Vector3 position)[] knotPositions = new (float, Vector3)[Random.Range(minCurves, maxCurves)];

        // Add some random curves
        for (int i = 0; i < knotPositions.Length; i++)
        {
            // A percentage of the spline, used by EvaluatePosition
            // to get the position of where that point is along the spline
            // float place = Random.Range(0.125f, 0.865f);
            float place = (0.74f) / (knotPositions.Length+1) * (i+1) + 0.125f;
            // The position on the spline based on the place value
            Vector3 midPos = splineContainer.EvaluatePosition(spline, place);

            
            // Get a vector perpedicular to the spline's x and z
            // Vector3 moveVector = Vector3.Cross(Vector3.down, betweenArenaVector.normalized); IDK WHY this isn't working, it's making loops in the paths
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
            float curveStrength = Random.Range(10, maxCurveStrength);
            
            // Make it so that the knot is moved outwards less towards the ends of the path
            curveStrength *= 4f * place * (1f - place); // When place is 0.5 (middle) then the full curveStrength will be used, less towards ends
            midPos += moveVector * curveStrength;

            // Add the place on spline and the position into the newKnotPositions array
            knotPositions[i] = (place, midPos);
        }

        // Sort the knot positions by their place along the spline
        knotPositions = knotPositions.OrderBy(p => p.placeOnSpline).ToArray();
        
        // Since inserting the knots into the spline changes it's shape, we put them in after so that
        // we can get a path without too crazy of a shape
        for (int i = 0; i < knotPositions.Length; i++)
        {
            // Create BezierKnot
            BezierKnot newKnot = new BezierKnot(knotPositions[i].position);
    
            // Add it to the all points list
            pathMidpoints.Add(newKnot);
            // Insert the new knot on the spline
            spline.Insert(spline.Count - 2, newKnot, TangentMode.AutoSmooth);
        }
    }

    void AddWindTunnelToPaths()
    {
        foreach (Path path in paths)
        {
            GameObject launchPadA = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            GameObject launchPadB = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            launchPadA.AddComponent<WindLaunchPad>();
            launchPadB.AddComponent<WindLaunchPad>();

            launchPadA.transform.position = path.spline[0].Position;
            launchPadB.transform.position = path.spline[path.spline.Count-1].Position;

            launchPadA.transform.localScale = new Vector3(5, 5, 5);
            launchPadB.transform.localScale = new Vector3(5, 5, 5);

            launchPadA.transform.parent = path.gameObjectRef.transform;
            launchPadB.transform.parent = path.gameObjectRef.transform;

            TestWindTunnelSetup tunnel = path.gameObjectRef.AddComponent<TestWindTunnelSetup>();
            tunnel.pathSpline = path.spline;
            tunnel.startLaunchPad = launchPadA.GetComponent<WindLaunchPad>();
            tunnel.endLaunchPad = launchPadB.GetComponent<WindLaunchPad>();
        }
    }

    void CleanUpPath(Spline spline, SplineContainer splineContainer)
    {
        
        // Clean up knots that are too sharp
        // Shouldn't take more than 50 tries
        int attempts = 0;
        bool noProblemKnots = false;
        // Also check if there were no problem knots, just exit if there weren't
        while (attempts < 50 && noProblemKnots == false)
        {
            // Set this to true before checking
            noProblemKnots = true;

            // Skip the entrance/exit knots, start at 2 and end at Count-2
            for (int i = 2; i < spline.Count-2; i++)
            {
                // Get how small the knot's angle is, it's a magnitude though not an angle
                float tangentLength = math.length(spline[i].TangentOut);
                if (tangentLength <= 12f)
                {
                    // If we find a problem knot
                    // Debug.Log("REMOVING " + spline[i] + " AT " + tangentLength + " ON " + splineContainer.name);
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
        
    }


    // Adds the mesh and mesh collider to the input path game object
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
            splineExtrude.Capped = false;

            var hasMeshRenderer = container.gameObject.TryGetComponent<MeshRenderer>(out var meshRenderer);
            if (hasMeshRenderer)
                meshRenderer.material = new Material(Shader.Find("Standard"));
        }

        // For some reason the mesh doesn't show unless you mess with the component in the editor,
        // or if you turn it off and on here, so that's what this is for
        splineExtrude.enabled = false;
        splineExtrude.enabled = true;

        // Add a mesh collider and set it to the generated mesh
        MeshCollider meshCollider = container.gameObject.AddComponent<MeshCollider>();
        
    }

    // Repositions the entrance of the path to be farther from other entrances, so they don't overlap/get too close
    void RepositionEntrance(Path path, string whichEntrance = "A")
    {
        BezierKnot entranceKnot;
        Arena arena = null;
        int knotIndex;
        int secondaryKnotIndex; // Used for the knot that makes the path face the arena
        // The passed whichEntrance string decides if we're checking the first knot or the last
        switch (whichEntrance)
        {
            case "A":
                knotIndex = 0;
                secondaryKnotIndex = 1;
                arena = path.arenaA;
                break;
            case "B":
                Spline spline = path.gameObjectRef.GetComponent<SplineContainer>().Spline;
                knotIndex = spline.Count-1;
                secondaryKnotIndex = spline.Count-2;
                arena = path.arenaB;
                break;
            default:
                //Debug.LogWarning("Invalid entrance specified for repositioning. Must be 'A' or 'B'.");
                return;
        }
        // Set the knot to whatever index we got from whichEntrance (first or last)
        entranceKnot = path.gameObjectRef.GetComponent<SplineContainer>().Spline[knotIndex];

        // Get necessary references
        Vector3 entrancePos = entranceKnot.Position; // Knot's position
        Vector3 directionFromArena = (entrancePos - arena.position).normalized; // Dir from arena center to the knot
        float angle = Mathf.Atan2(directionFromArena.z, directionFromArena.x); // Float angle value of the direction (in radians)

        // Initialize sweep angle in the positive direction
        float posAngle = angle;
        bool foundAngle = false;
        int attempts = 0;
        // Sweep angle to +90 degrees (from the path entrance) in increments of 1 degree, and raycast in that direction to check for other entrances
        while (attempts < 360 && !foundAngle)
        {
            posAngle += Mathf.Deg2Rad * 1; // Increment the angle by 1 degree
            attempts++; // Increment attempts

            // Get the direction vector from the arena based on the angle
            directionFromArena = new Vector3(Mathf.Cos(posAngle), 0, Mathf.Sin(posAngle));

            // Raycast from the arena
            RaycastHit[] hits = Physics.RaycastAll(arena.position, directionFromArena, arena.gameObjectRef.transform.localScale.x + 100f);
            foreach (RaycastHit hit in hits)
            {
                //Debug.Log("POSITIVE " + arena.gameObjectRef.name + " " + hit.collider.gameObject.name + " " + path.gameObjectRef.name + " " + attempts);
                // If we hit a path's collider other than the entrance collider
                if (hit.collider.gameObject.CompareTag("Path") && hit.collider.gameObject != path.gameObjectRef)
                {
                    // Debug.Log("GOINGPOS " + "PATH " + path.gameObjectRef.name + "HIT COLL ->" + hit.collider.gameObject.name);
                    foundAngle = true; // there's another entrance in this direction
                    break; // Exit the loop
                }
            }
        }

        // Initialize sweep angle in the negative direction
        float negAngle = angle;
        foundAngle = false;
        attempts = 0;
        // Sweep angle to -90 degrees (from the path entrance) in increments of 1 degree, and raycast in that direction to check for other entrances
        while (attempts < 360 && !foundAngle)
        {
            negAngle -= Mathf.Deg2Rad * 1; // decrement the angle by 1 degree
            attempts++; // Increment attempts

            // Get the direction vector from the arena based on the angle
            directionFromArena = new Vector3(Mathf.Cos(negAngle), 0, Mathf.Sin(negAngle));

            // Raycast from the arena
            RaycastHit[] hits = Physics.RaycastAll(arena.position, directionFromArena, arena.gameObjectRef.transform.localScale.x + 100f);
            foreach (RaycastHit hit in hits)
            {
                //Debug.Log("NEGATIVE " + arena.gameObjectRef.name + " " + hit.collider.gameObject.name + " " + path.gameObjectRef.name + " " + attempts);
                // If we hit a path's collider other than the entrance collider
                if (hit.collider.gameObject.CompareTag("Path") && hit.collider.gameObject != path.gameObjectRef)
                {
                    // Debug.Log("GOINGNEG" + " PATH " + path.gameObjectRef.name + "HIT COLL ->" + hit.collider.gameObject.name);
                    foundAngle = true; // there's another entrance in this direction
                    break; // Exit the loop
                }
            }
        }

        // Now decide on the new angle of the entrance
        float newAngle = 0f;
        // compare the final pos and neg angles, and choose a random new angle that is farther from other path entrances
        if (Mathf.Abs( Mathf.Abs(posAngle - angle) - Mathf.Abs(negAngle - angle) ) <= 0.0001f ) // If they were equal (or close enough)
        {
            // Don't move the entrance, exit the function
            // Debug.Log("PATH " + path.gameObjectRef.name + " DIDNT MOVE ON " + arena.gameObjectRef.name);
            return;
        }
        else if (Mathf.Abs(posAngle - angle) > Mathf.Abs(negAngle - angle)) // Negative angle closer
        {
            newAngle = Random.Range(angle + Mathf.Deg2Rad * 5, posAngle - Mathf.Deg2Rad * 5);
        }
        else if (Mathf.Abs(posAngle - angle) < Mathf.Abs(negAngle - angle)) // Positive angle closer
        {
            newAngle = Random.Range(angle - Mathf.Deg2Rad * 5, negAngle + Mathf.Deg2Rad * 5);
        }
        
        
        // Get a point in the new direction to get 
        Vector3 newDirection = new Vector3(Mathf.Cos(newAngle), 0, Mathf.Sin(newAngle));
        Vector3 newPoint = arena.position + newDirection * arena.gameObjectRef.transform.localScale.x;
                
        entranceKnot.Position = arena.arenaBounds.ClosestPoint(newPoint);
        // Debug.Log(angle + "<-ANGLE " + "\n NewANGLE->" + newAngle  + "\n POSANGLE->" + posAngle + " " + Mathf.Abs(posAngle - angle) + " " + (angle + 90*Mathf.Deg2Rad).ToString() + " \n NEGANGLE->" + negAngle + " " + Mathf.Abs(negAngle - angle) + " " + (angle - 90*Mathf.Deg2Rad).ToString() + " \n " + newPoint + "<-NEWPOINT OLDPOINT->" + entrancePos + "\n ARENA->" + arena.gameObjectRef.name + " \n PATH->" + path.gameObjectRef.name);
        // Gotta set knot to make it actually move the knot
        path.spline.SetKnot(knotIndex, entranceKnot);

        // Do the same for the secondary knot, slightly farther out
        BezierKnot secondaryKnot = path.spline[secondaryKnotIndex];
        secondaryKnot.Position = newPoint + newDirection * 20f;
        path.spline.SetKnot(secondaryKnotIndex, secondaryKnot);
    }

    // NOT WORKING
    void SpaceOutPaths()
    {
        bool pointsAllSpaced = false;
        int attempts = 0;
        while (!pointsAllSpaced && attempts < 10)
        {
            // yield return new WaitForSeconds(.1f);
            attempts++;
            pointsAllSpaced = true;
            // Loop through all paths
            for (int i = 0; i < paths.Count; i++)
            {
                // Loop through this path's points (skip the 2 points from each end)
                for (int knotIndex = 2; knotIndex < paths[i].spline.Count-2; knotIndex++)
                {
                    // Get knots from list
                    BezierKnot knot = paths[i].spline[knotIndex];
                    // Check a sphere with 75 radius on the point if there are colliders overlapping
                    Collider[] hitColliders = Physics.OverlapSphere(knot.Position, 75f);

                    foreach (var hitCollider in hitColliders)
                    {
                        // Don't check the current path
                        if (hitCollider.gameObject != paths[i].gameObjectRef) {
                            // Set points spaced false and space them apart
                            pointsAllSpaced = false;

                            // If it was a path, loop through its points
                            if (hitCollider.gameObject.tag == "Path")
                            {
                                Spline otherSpline = hitCollider.gameObject.GetComponent<SplineContainer>().Spline;
                                for (int otherKnotIndex = 0; otherKnotIndex < otherSpline.Count; otherKnotIndex++)
                                {
                                    // Get their positions
                                    Vector3 pos = knot.Position;
                                    Vector3 otherPos = otherSpline[otherKnotIndex].Position;

                                    Vector3 betweenVector = otherPos - pos;

                                    // Debug.Log("Spacing out " + knot + " and " + otherSpline[otherKnotIndex] + " at distance " + Vector3.Distance(pos, otherPos));
                                    
                                    Vector3 newPos = pos;
                                    newPos -= betweenVector.normalized * (50 - betweenVector.magnitude) / 50;
                                    knot.Position = newPos;

                                    paths[i].spline.SetKnot(knotIndex, knot);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        // Draws the bounds of the map's generation area (between minBounds and maxBounds)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.Lerp(minBounds, maxBounds, 0.5f), maxBounds - minBounds);

    }
}
