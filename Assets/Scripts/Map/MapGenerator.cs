using System;
using System.Collections.Generic;
using System.Collections;
using Debug = UnityEngine.Debug;
using Vector3 = UnityEngine.Vector3;
using Vector2 = UnityEngine.Vector2;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Linq;
using System.Numerics;
using Category5.Player.WindRiding;
using Category5.Enemies;
using Category5.MapEnums;
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

    // The random seed used to generate the map
    public NetworkVariable<int> Seed = new NetworkVariable<int>(
        0, // default value
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );


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

    [SerializeField] GameObject[] islandPrefabs;

    [Header("Materials")]
    [SerializeField] Material cloudMaterial;

    // Material for walls on storm eyes and wind tunnels
    [SerializeField] Material cloudWallMaterial;
    // Material for entrances to wind tunnels
    [SerializeField] Material entranceMaterial;
    [SerializeField] Material islandMaterial;

    [Header("Prefabs")]
    [SerializeField] GameObject cloudwallPrefab;
    [SerializeField] GameObject cloudSpherePrefab;
    
    // Spawner stuff
    [SerializeField] GameObject enemySpawnerPrefab;
    private List<GameObject> spawners = new List<GameObject>();

    [Header("MiniAndOverheadMap")]
    [SerializeField] Sprite arenaMapSprite;
    [SerializeField] Sprite pathMapSprite;

    /*
    void Start()
    {
        // Make sure there's no map existing when generating on start
        DeleteMap();
        StartCoroutine(GenerateMap());
    }*/

    public void StartRound()
    {
        // Get a random seed on the server, then generate a map with it
        // Clients will use the seed from the server so they generate the same map
        if (IsServer) {
            GetSeed();
            DeleteMap();
            GenerateMap(Seed.Value);
            //AddEnemySpawnersToArenas();
            return;
        }
        // Everyone waits for the seed to be ready
        Seed.OnValueChanged += (_, newSeed) =>
        {
            DeleteMap();
            GenerateMap(newSeed);
        };

        // If the seed was already set before this client joined
        if (Seed.Value != 0)
        {
            DeleteMap();
            GenerateMap(Seed.Value);
        }
    }

    public override void OnNetworkSpawn()
    {
        
        StartRound();
    }

    // Deletes the current map and clears the lists
    public void DeleteMap()
    {
        if (mapParent != null) {
            DestroyImmediate(mapParent);
        }

        foreach (GameObject spawner in spawners)
        {
            DestroyImmediate(spawner);
        }
        spawners.Clear();
        arenas.Clear();
        paths.Clear();
    }
    public void GetSeed()
    {
        if (IsServer)
        {
            Seed.Value = Random.Range(-9999, 9999);
        }
    }
    
    // Randomly generates a map
    public void GenerateMap(int seed)
    {
        Random.InitState(seed);

        mapParent = new GameObject("Map");

        // Number of eyes cannot exceed number of arenas, and cannot be < 0
        numberOfEyes = Math.Clamp(numberOfEyes, 0, numberOfArenas);

        // The main boss arena will always be created in the center of the map
        Arena bossArena = CreateArena(Vector3.zero, mapParent.transform, ArenaType.Boss, "boss", 1.5f);
        bossArena.GenerateArenaBase();
        AddCloudBoundaryToArena(bossArena);

        // Create arenas at random positions between the input Vector3s for storm eyes
        for (int i = 0; i < numberOfArenas; i++)
        {
            float newScaleFactor = Random.Range(1f, 3f);
            // Store a boolean for if an arena was successfully created and create an arena
            Arena arenaCreated = CreateArena(minBounds, maxBounds, ArenaType.Combat, i.ToString(), mapParent.transform, newScaleFactor);

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
                newScaleFactor = Random.Range(1f, 3f);
                arenaCreated = CreateArena(minBounds, maxBounds, ArenaType.Combat, i.ToString(), mapParent.transform, newScaleFactor);
            }
            if (arenaCreated != null)
            {
                arenaCreated.GenerateArenaBase();
                AddCloudBoundaryToArena(arenaCreated);
            }
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
                float distance = Vector3.Distance(arenas[i].transform.position, arenas[j].transform.position);
                if (closestArena == null || distance < Vector3.Distance(arenas[i].transform.position, closestArena.transform.position))
                {
                    secondClosestArena = closestArena;
                    closestArena = arenas[j];
                }
                else if (secondClosestArena == null || distance < Vector3.Distance(arenas[i].transform.position, secondClosestArena.transform.position))
                {
                    secondClosestArena = arenas[j];
                }
                
            }
            CreatePath(arenas[i], closestArena, mapParent.transform, pathCount.ToString());
            pathCount++;
            CreatePath(arenas[i], secondClosestArena, mapParent.transform, pathCount.ToString());
            pathCount++;
        }

        foreach (Path path in paths)
        {
            AddPathMidpoints(path.gameObjectRef.GetComponent<SplineContainer>());
            // force refresh
            path.gameObjectRef.GetComponent<SplineContainer>().Spline.Closed = true;
            path.gameObjectRef.GetComponent<SplineContainer>().Spline.Closed = false;
        }
        
        
        // Space out all the path points so they dont overlap!
        // SpaceOutPaths();

        if (Application.isPlaying) {
            // Add navmesh surfaces to all arenas
            StartCoroutine(AddNavMeshSurfaceToArenas());
            // Add the wind tunnel component and launch pads to each path
            AddWindTunnelToPaths();
            // Add a mesh to all paths
            StartCoroutine(CreatePathMeshes());
        }

        // Reposition the path entrances away from each
        // other to reduce crowding
        foreach (Arena arena in arenas)
        {
            RepositionEntrances(arena, 40f);
            // Have the arena script generate the interior of the arena
            arena.GenerateArena();
        }
        // After the arenas are created, add enemy spawners to them
        AddEnemySpawnersToArenas();
    }


    // Creates an arena at the specified location, specific location version!
    // Overload below that does a random position
    Arena CreateArena(Vector3 inputPos, Transform parent, ArenaType arenaType, string numberforname = "", float scaleFactor=1f)
    {   
        // Check if the new arena is too close to a previous one
        // Use an OverlapBox to detect collisions
        // Do not let arenas spawn on top of each other
        // The radius is a little bigger than the arena's actual size to prevent them from being too close, 
        // since the paths will be generated from the edges of the arenas
        Collider[] colliders = Physics.OverlapCapsule(inputPos - new Vector3(0, maxBounds.y, 0), 
                                                    inputPos + new Vector3(0, maxBounds.y, 0), 
                                                    scaleFactor * 120f, // Double radius to make sure there's enough space for paths between arenas, since paths are generated from the edges of the arenas
                                                    arenaMask, 
                                                    QueryTriggerInteraction.Collide
                                                    );
        // Only count colliders from previously placed arenas (children of mapParent) to avoid
        // false overlaps with scene geometry (van, decorations, etc.)
        int arenaColliders = 0;
        foreach (Collider c in colliders)
        {
            if (c.transform.IsChildOf(parent))
            {
                arenaColliders++;
            }
                
        }
        if (arenaColliders > 0) // overlap
        {
            // DestroyImmediate(arena); // Remove the overlapping arena

            // Return null, the arena wasn't created
            return null;
        }
        else
        {
            // Create an empty gameobject to serve as the arena's base object
            GameObject arena = new GameObject();
            arena.transform.position = inputPos;
            arena.transform.parent = parent;
            
            // Set the arena's name and make it a child of the parent param
            if (!string.IsNullOrEmpty(numberforname))
            {
                arena.name = "Arena_" + numberforname;
            }

            // Attach Arena script
            Arena arenaScript;
            switch (arenaType)
            {
                case ArenaType.Boss:
                    arenaScript = arena.AddComponent<BossArena>();
                    break;
                case ArenaType.Combat:
                    arenaScript = arena.AddComponent<CombatArena>();
                    break;
                default:
                    arenaScript = arena.AddComponent<CombatArena>();
                    break;
            }
            arenaScript.scaleFactor = scaleFactor;
            arenaScript.arenaMapSprite = arenaMapSprite;
            arenaScript.islandPrefabs = islandPrefabs;
            // Make the arena an eye if we still need more eyes
            if (numberOfEyes > 0)
            {
                arenaScript.isEye = true;
                numberOfEyes--;
            }

            // Create an ArenaData instance to hold the arena's data
            //ArenaData arenaData = new ArenaData(arena.transform.position, arena, scaleFactor);
            arenas.Add(arenaScript); // Store reference to the created arena

            return arenaScript; // No overlap, return the created arena data
        }
    }

    // Overload of CreateArena that takes in Vector3 min and max for a random position,
    // Then calls the original version on a random position within the box created by the min and max
    // The min and max are the bounds of the area where the arena can spawn
    Arena CreateArena(Vector3 min, Vector3 max, ArenaType arenaType, string numberForName, Transform parent, float scaleFactor=1f)
    {
        // Create the arena, and check if the arena was successfully created
        // This call doesn't pass a scalefactor, so it defaults to 1f
        Arena arena = CreateArena(
                                    new Vector3(Random.Range(min.x, max.x), Random.Range(min.y, max.y), Random.Range(min.z, max.z)),
                                    parent,
                                    arenaType,
                                    numberForName,
                                    scaleFactor
                                 );

        if ( arena != null )
        {
            // If it was return the arena data
            return arena;
        }
        else
        {
            // If not return null
            return null;
        }
    }

    // Adds an enemy spawner to the given arena, making it a child of the arena and setting the bounds of the enemy spawns
    void AddEnemySpawnersToArenas()
    {
        if (!IsServer) return;
        // Loop through every arena
        foreach (Arena arena in arenas)
        {
            if (arena is CombatArena)
            {
                if (arena.islands.Count == 0)
                {
                    // Debug.LogWarning($"Arena {arena.gameObject.name} has no islands, skipping spawner generation for this arena.");
                    continue;
                }

                // Loop through every island
                foreach (GameObject island in arena.islands)
                {
                    Island islandScript = island.GetComponent<Island>();
                    // Loop through each instance of spawner data
                    foreach (Island.SpawnerData spawnerData in islandScript.spawnerDataArray)
                    {
                        GameObject spawnerObj = Instantiate(enemySpawnerPrefab);
    
                        EnemySpawner spawner = spawnerObj.GetComponent<EnemySpawner>();
                        spawner.spawnBounds = spawnerData.spawnerBounds;
                        // Here set the spawner to only start spawning using the triggervolume on this spawner
                        spawner.autoStartOnSpawn = false;
                        spawner.startOnTrigger = true;
                        spawner.triggerVolume = spawnerData.trigger;

                        spawner.GetComponent<NetworkObject>().Spawn();

                        spawnerObj.transform.position = spawnerData.spawnerMarker.position;
                        spawnerObj.transform.rotation = spawnerData.spawnerMarker.rotation;
                        spawnerObj.transform.parent = island.transform;
                        // Add the spawner to the spawners list
                        spawners.Add(spawnerObj);
                    }
                }
            }
        }
    }

    private IEnumerator AddNavMeshSurfaceToArenas()
    {
        yield return new WaitForEndOfFrame();
       
        NavMeshSurface surface = mapParent.AddComponent<NavMeshSurface>();
        surface.layerMask = LayerMask.GetMask("Default");
        surface.BuildNavMesh();
    }

    void AddCloudBoundaryToArena(Arena arena)
    {
        GameObject cloudBoundary;
        float Yscale = 0;
        float Ypos = 0;

        if (arena.isEye)
        {
            cloudBoundary = Instantiate(cloudwallPrefab);
            Yscale = 100;
            Ypos = arena.transform.position.y;
        }
        else
        {
            cloudBoundary = Instantiate(cloudSpherePrefab);
            Yscale = arena.radius;
            Ypos = arena.transform.position.y;
        }
        
        cloudBoundary.transform.localScale = new Vector3(
            arena.radius,
            Yscale,
            arena.radius
        );
        cloudBoundary.transform.position = new Vector3(
            arena.transform.position.x,
            Ypos,
            arena.transform.position.z
        );
        cloudBoundary.SetActive(true);
        cloudBoundary.transform.parent = mapParent.transform;
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
        GameObject pathObj = new GameObject("Path_" + numberforname);
        SplineContainer splineContainer = pathObj.AddComponent<SplineContainer>();

        // Give the path a Path tag
        splineContainer.tag = "Path";
        if (!string.IsNullOrEmpty(numberforname))
        {
            splineContainer.name = "Path_" + numberforname;
        }

        // Create a spline to be held by the container
        Spline spline = splineContainer.Spline;
        if (arenaA == null)
        {
            Debug.LogError("arenaA is null");
            return;
        }

        if (arenaB == null)
        {
            Debug.LogError("arenaB is null");
            return;
        }

        if (arenaA.arenaBounds == null)
        {
            Debug.LogError(arenaA.name + "arenaA.arenaBounds is null");
            return;
        }
        if (arenaB.arenaBounds == null)
        {
            Debug.LogError(arenaB.name + "arenaB.arenaBounds is null");
            return;
        }
        // Get the points on each arena's bounds collider closest to the other arena
        // These will be the start and end points of the path
        Vector3 pointOnA = arenaA.arenaBounds.ClosestPoint(new Vector3(arenaB.transform.position.x, Random.Range(arenaA.transform.position.y + 5f, arenaA.transform.position.y + 200f), arenaB.transform.position.z));
        Vector3 pointOnB = arenaB.arenaBounds.ClosestPoint(new Vector3(pointOnA.x, Random.Range(arenaB.transform.position.y + 5f, arenaB.transform.position.y + 200f), pointOnA.z));
        // Points are at a random height on the arena, between 5 and 100 units above the arena's center, to add some verticality to the paths
        pointOnA = new Vector3(pointOnA.x, arenaA.transform.position.y + 5f, pointOnA.z);
        pointOnB = new Vector3(pointOnB.x, arenaB.transform.position.y + 5f, pointOnB.z);

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
        // CleanUpPath(spline, splineContainer);

        // Make path a child of the parent
        splineContainer.gameObject.transform.parent = parent;
        // Put path on the cloudSurface layer
        splineContainer.gameObject.layer = 8;

        // Create a Path instance to hold the path's data
        // Path pathData = new Path(arenaA, arenaB, splineContainer.gameObject);
        Path pathData = pathObj.AddComponent<Path>();
        pathData.arenaA = arenaA;
        pathData.arenaB = arenaB;
        pathData.gameObjectRef = splineContainer.gameObject;
        pathData.spline = spline;

        paths.Add(pathData); // Store reference to the created path

        // Add the path to the arenas that it connects
        arenaA.connectedPaths.Add(pathData);
        arenaB.connectedPaths.Add(pathData);
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
    // Adds the wind tunnel to each path which players can use to travel through the paths
    // Also adds launch pads at the start and end of each path, and sets the tunnel's start and end launch pads to those
    void AddWindTunnelToPaths()
    {
        // loop through paths
        foreach (Path path in paths)
        {
            // Create launch pads at the start and end of the path, set them to be children of the path, and set the tunnel's start and end launch pads to those
            GameObject launchPadA = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            GameObject launchPadB = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            launchPadA.AddComponent<WindLaunchPad>();
            launchPadB.AddComponent<WindLaunchPad>();

            launchPadA.transform.position = path.spline[0].Position;
            launchPadB.transform.position = path.spline[path.spline.Count-1].Position;

            launchPadA.transform.localScale = new Vector3(25, 25, 25);
            launchPadB.transform.localScale = new Vector3(25, 25, 25);

            launchPadA.tag = "Path";
            launchPadB.tag = "Path";

            launchPadA.transform.LookAt(path.arenaA.transform.position);
            launchPadB.transform.LookAt(path.arenaB.transform.position);
            launchPadA.transform.Rotate(new Vector3(0, -90, 0));
            launchPadB.transform.Rotate(new Vector3(0, -90, 0));

            launchPadA.transform.parent = path.gameObjectRef.transform;
            launchPadB.transform.parent = path.gameObjectRef.transform;

            path.entranceA = launchPadA;
            path.entranceB = launchPadB;
            
            // Set the materials of the launch pads
            MeshRenderer renderer = launchPadA.GetComponent<MeshRenderer>();
            renderer.material = entranceMaterial;
            renderer = launchPadB.GetComponent<MeshRenderer>();
            renderer.material = entranceMaterial;

            // Create the TestWindTunnelSetup component and set its variables to the spline and launch pads
            // This is the part that actually moves the player along the path when they enter the launch pad trigger
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
    private IEnumerator CreatePathMeshes()
    {
        // Temporary implementation, just used to make a visible path rn!!
        yield return null;
        foreach (Path path in paths)
        {
            SplineContainer container = path.gameObjectRef.GetComponent<SplineContainer>();

            SplineExtrude splineExtrude = container.gameObject.AddComponent<SplineExtrude>();
            splineExtrude.Container = container;

            var hasMeshFilter = container.gameObject.TryGetComponent<MeshFilter>(out var meshFilter);
            if (hasMeshFilter)
            {
                if (meshFilter.sharedMesh == null)
                {
                    Mesh extrudeMesh = new Mesh();
                    extrudeMesh.name = "Spline Extrude Mesh";
                    meshFilter.sharedMesh = extrudeMesh;
                }
                // Set the mesh variables
                splineExtrude.Radius = 10;
                splineExtrude.FlipNormals = true;
                splineExtrude.Capped = false;
                splineExtrude.SegmentsPerUnit = 6f;
                splineExtrude.Sides = 20;
                splineExtrude.RebuildOnSplineChange = true;

                splineExtrude.Rebuild();

                var hasMeshRenderer = container.gameObject.TryGetComponent<MeshRenderer>(out var meshRenderer);
                if (hasMeshRenderer)
                    meshRenderer.material = new Material(cloudWallMaterial);
            }

            // For some reason the mesh doesn't show unless you mess with the component in the editor,
            // or if you turn it off and on here, so that's what this is for
            // splineExtrude.enabled = false;
            // splineExtrude.enabled = true;

            // Add a mesh collider and set it to the generated mesh
            MeshCollider meshCollider = container.gameObject.AddComponent<MeshCollider>();
        }
    }

    // Repositions the entrances on an arena, so they don't overlap/get too close
    void RepositionEntrances(Arena arena,  float minimumAngle = 60f)
    {
        // Dictionary that holds pairs of entrances and the angle between them, used to check if the entrances are too close and need to be repositioned
        Dictionary<(GameObject, GameObject), float> entrancePairDistances = new Dictionary<(GameObject, GameObject), float>();
        // This dictionary holds the entrance gameobjects as keys and their associated spline and knot as values, used to reposition the entrances later in this function
        Dictionary<GameObject, (Spline, BezierKnot, int)> entranceData = new Dictionary<GameObject, (Spline, BezierKnot, int)>();
        foreach (Path path in arena.connectedPaths)
        {
            
            BezierKnot entranceKnot;
            GameObject entranceObj;
            int knotIndex = 0;

            if (path.arenaA == arena)
            {
                entranceKnot = path.spline[0];
                entranceObj = path.entranceA;
                knotIndex = 0;
            }
            else
            {
                entranceKnot = path.spline[path.spline.Count - 1];
                entranceObj = path.entranceB;
                knotIndex = path.spline.Count - 1;
            }

            // Fill out the entranceData dictionary for this entrance
            entranceData.Add(entranceObj, (path.spline, entranceKnot, knotIndex));

            // Loop through paths again to compare this entrance to the others
            foreach (Path otherPath in arena.connectedPaths)
            {
                if (otherPath == path) continue; // skip the same path

                BezierKnot otherEntranceKnot;
                GameObject otherEntranceObj;

                if (otherPath.arenaA == arena)
                {
                    otherEntranceKnot = otherPath.spline[0];
                    otherEntranceObj = otherPath.entranceA;
                }
                else
                {
                    otherEntranceKnot = otherPath.spline[otherPath.spline.Count - 1];
                    otherEntranceObj = otherPath.entranceB;
                }

                float angleBetween = AngularDistance(
                    GetAngle(arena.transform.position, entranceKnot.Position),
                    GetAngle(arena.transform.position, otherEntranceKnot.Position)
                );
                
                // Check if we've already compared these two entrances, if we have, skip
                var keyA = (entranceObj, otherEntranceObj);
                var keyB = (otherEntranceObj, entranceObj);

                if (entrancePairDistances.ContainsKey(keyA) ||
                    entrancePairDistances.ContainsKey(keyB))
                {
                    continue;
                }
                // If not, add the pair and their angle distance to the dictionary
                entrancePairDistances.Add(keyA, angleBetween);
            }
        }
        
        // EntrancePairDistances now contains all pairs of entrances and the angle between them, so we can loop through it and reposition any entrances that are too close together
        // Get the angles from the pairs and sort them from smallest to largest
        float[] anglesBetween = entrancePairDistances.Values.ToArray();
        Array.Sort(anglesBetween);
        // loop through the angles
        foreach (float angle in anglesBetween)
        {
            // Get the associated pair of entrances (May be more than one, so we use an array)
            (GameObject, GameObject)[] entrancePairArray = entrancePairDistances.Where(x => x.Value == angle).Select(x => x.Key).ToArray();
            foreach ((GameObject, GameObject) pair in entrancePairArray)
            {
                GameObject[] entrancePair = { pair.Item1, pair.Item2 };

                if (angle < minimumAngle) // if the angle is less than the minimum angle, we consider the entrances too close and reposition them
                {
                    // Get the directions from the arena to the entrances
                    Vector3 entranceDirection1 = (entrancePair[0].transform.position - arena.transform.position).normalized;
                    float angleRad1 = Mathf.Atan2(entranceDirection1.z, entranceDirection1.x);
                    Vector3 entranceDirection2 = (entrancePair[1].transform.position - arena.transform.position).normalized;
                    float angleRad2 = Mathf.Atan2(entranceDirection2.z, entranceDirection2.x);

                    // Get the new position of each entrance here based on which one is in which direction
                    if (angleRad1 < angleRad2)
                    {
                        angleRad1 -= (minimumAngle - angle) * Mathf.Deg2Rad / 2f;
                        angleRad2 += (minimumAngle - angle) * Mathf.Deg2Rad / 2f;
                    }
                    else
                    {
                        angleRad1 += (minimumAngle - angle) * Mathf.Deg2Rad / 2f;
                        angleRad2 -= (minimumAngle - angle) * Mathf.Deg2Rad / 2f;
                    }

                    // Finally, for each entrance we change their position based on the new positions
                    // Here we use the entrance data from earlier since we have to set knot positions on the splines of the paths to move the entrances
                    for (int i = 0; i < entrancePair.Length; i++)
                    {
                        float newAngle;
                        if (i == 0) {
                            newAngle = angleRad1;
                        }
                        else {
                            newAngle = angleRad2;
                        }

                        Vector3 newDir = new Vector3(
                        Mathf.Cos(newAngle),
                        0,
                        Mathf.Sin(newAngle)
                        );

                        Vector3 newPos = arena.transform.position + newDir * arena.radius;

                        entrancePair[i].transform.position = newPos;

                        Spline thisPathSpline = entranceData[entrancePair[i]].Item1;
                        BezierKnot entranceKnot = entranceData[entrancePair[i]].Item2;
                        entranceKnot.Position = newPos;
                        thisPathSpline.SetKnot(entranceData[entrancePair[i]].Item3, entranceKnot);

                        // Do the same for the secondary knot, slightly farther out
                        int secondaryKnotIndex;
                        if (entranceData[entrancePair[i]].Item3 == 0)
                        {
                            secondaryKnotIndex = 1;
                        }
                        else
                        {
                            secondaryKnotIndex = entranceData[entrancePair[i]].Item1.Count - 2;
                        }
                        BezierKnot secondaryKnot = thisPathSpline[secondaryKnotIndex];
                        secondaryKnot.Position = newPos + newDir * 20f;
                        thisPathSpline.SetKnot(secondaryKnotIndex, secondaryKnot);
                    }
                }
            }
        }
    }
    // Helper methods for RepositionEntrance
    float GetAngle(Vector3 arenaPos, Vector3 point)
    {
        Vector3 dir = (point - arenaPos).normalized;
        return Mathf.Atan2(dir.z, dir.x);
    }
    float AngularDistance(float a, float b)
    {
        return Mathf.Abs(Mathf.DeltaAngle(
            a * Mathf.Rad2Deg,
            b * Mathf.Rad2Deg
        ));
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
