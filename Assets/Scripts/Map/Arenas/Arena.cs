using System.Collections.Generic;
using System.Linq;
using Category5.Enemies;
using Category5.MapEnums;
using Category5.Player.WindRiding;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Splines;
using UnityEngine.UI;

public class Arena : NetworkBehaviour
{
    public float scaleFactor;
    public float radius;

    // Stores if this arena is an eye or not, players can drop into eyes
    public bool isEye;

    
    // Stores if the arena is a "hidden" arena. Hidden arenas will be initially inaccessible, and
    // paths connected to them will also be hidden
    public bool isHidden;

    // List of the path entrances connected to this arena. Set and Used in map generator reposition path entrances function
    public List<Path> connectedPaths = new List<Path>();

    // Arenas have a capsule collider surrounding them that defines
    // the arena's boundaries (The storm cloud walls)
    public Collider arenaBounds;

    public TriggerVolume trigger; // Unused?

    public GameObject[] islandPrefabs;
    public List<GameObject> islands = new List<GameObject>();
    public LayerMask islandMask;


    protected class WindPath
    {
        public Island islandA;
        public Island islandB;
        public GameObject windTunnel;
        public WindPath(Island islandA, Island islandB, GameObject windTunnel)
        {
            this.islandA = islandA;
            this.islandB = islandB;
            this.windTunnel = windTunnel;
        }
    }
    protected List<WindPath> windPaths = new List<WindPath>();
    public GameObject windLaunchPrefab;

    public List<EnemySpawner> enemySpawners = new List<EnemySpawner>();

    public Sprite arenaMapSprite;
    protected SpriteRenderer mapSprite;

    // Update is called once per frame
    void Update()
    {
        // if (enemySpawner != null && enemySpawner.isCleared)
        // {
        //     mapSprite.color = Color.softBlue;
        // }
    }

    // Creates the common necessary items for every arena
    // This is called on each arena after they are instantiated in the map generator first since other parts of map gen
    // need the trigger and stuff to be set up before they can do their thing
    public void GenerateArenaBase()
    {
        radius = scaleFactor*60;

        // Add a capsule collider to define the bounds of the arena
        if (isEye)
        {
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = radius; // Set the radius
            capsule.height = 100f; // Set the height
            capsule.center = new Vector3(0, 10, 0); // Center the collider on the arena
            arenaBounds = capsule;
        }
        else
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = radius;
            arenaBounds = sphere;
        }
        arenaBounds.isTrigger = true; // Set the collider to be a trigger so players can fall through

        // Add TriggerVolume script that will invoke an event when that capsule
        // collider trigger is entered. This will automatically get the capsule trigger collider
        trigger = gameObject.AddComponent<TriggerVolume>();
        trigger.targetLayers = LayerMask.GetMask("Player");
        trigger.targetTag = "Player";

        // Add cloud layer
        GameObject cloudLayer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        DestroyImmediate(cloudLayer.GetComponent<CapsuleCollider>()); // Remove the cloud layer's collider
        cloudLayer.transform.position = new Vector3(transform.position.x, transform.position.y - scaleFactor * 40, transform.position.z);
        cloudLayer.transform.localScale = new Vector3(
                                        radius * 2,
                                        1f,
                                        radius * 2
                                        );
        cloudLayer.transform.parent = transform;
        // cloudLayer.GetComponent<MeshRenderer>().material = cloudMaterial; // Set the cloud material
        MeshCollider cloudCollider = cloudLayer.AddComponent<MeshCollider>();
        cloudCollider.convex = true; // Set convex to true so it can be a trigger
        cloudCollider.isTrigger = true; // Add a mesh collider and set it to be a trigger so players can fall through
        cloudLayer.layer = 8;


        // Attach Sprite Renderer for the mini and overhead maps
        GameObject sprite = new GameObject("sprite");
        sprite.transform.position = transform.position;
        sprite.transform.localScale = new Vector3(
                                        radius * 2,
                                        radius * 2,
                                        1f
                                        );
        sprite.transform.rotation = UnityEngine.Quaternion.Euler(90, 0, 0);
        int layerIndex = LayerMask.NameToLayer("Map");
        sprite.layer = layerIndex;
        sprite.transform.parent = transform;

        SpriteRenderer renderer = sprite.AddComponent<SpriteRenderer>();
        renderer.sprite = arenaMapSprite;

        mapSprite = sprite.GetComponentInChildren<SpriteRenderer>();
        mapSprite.color = Color.orange;
    }

    public virtual void GenerateArena()
    {
        // This will be overridden by the specific arena types to generate the interior of the arena
    }

    public void ConnectAllIslands()
    {
        // Find the closest island to each island and make a path between them

        // An array of the islands
        GameObject[] islandsArray = islands.ToArray();
        foreach (GameObject islandA in islands)
        {
            // sort from closest to this island to farthest
            GameObject[] closeToFarIslands = islandsArray.OrderBy(island =>  Vector3.Distance(islandA.transform.position, island.transform.position)).ToArray();

            // Loop through the islands from closest to farthest and try to connect
            foreach (GameObject islandB in closeToFarIslands)
            {
                if (islandA == islandB) continue; // Skip same islands
                // If a connection is sucessful, break out of this loop
                if ( ConnectIslands(islandA.GetComponent<Island>(), islandB.GetComponent<Island>()) )
                {
                    break;
                }
            }
        }
    }

    protected Island CreateIsland(Vector3 position, Vector3 scale, IslandTag[] tags=null, bool forceCreation=false)
    {
        GameObject selectedPrefab = islandPrefabs[Random.Range(0, islandPrefabs.Length)];
        Vector3 islandBounds = selectedPrefab.GetComponentInChildren<MeshRenderer>().bounds.extents;
        // (Might have to do something abt rotation since the bounds will be different depending on how the island is rotated)
        // Check if the new island is too close to a previous one
        // Use an OverlapBox to detect collisions
        // Use bounds bigger than the island's actual bounds to prevent them from being too close to each other
        islandMask = LayerMask.GetMask("Default");
        Collider[] colliders = Physics.OverlapBox(position, islandBounds * 5, Quaternion.identity, islandMask, QueryTriggerInteraction.Ignore);
                                                
        // Only count colliders from previously placed arenas (children of mapParent) to avoid
        // false overlaps with scene geometry (van, decorations, etc.)
        int islandColliders = 0;
        foreach (Collider c in colliders)
        {
            if (c.transform.IsChildOf(transform.parent))
            {
                islandColliders++;
            }
                
        }
        if (islandColliders > 0 && !forceCreation) // overlap
        {
            // Return null, the island wasn't created
            return null;
        }
        else
        {
            GameObject island = Instantiate(selectedPrefab, position, Quaternion.identity);
            island.transform.parent = transform;
            islands.Add(island);

            return island.GetComponent<Island>();
        }
    }

    // Connects 2 given islands with a windpath
    protected bool ConnectIslands(Island islandA, Island islandB)
    {
        // Checks if the path is valid
        // Path to same arena?
        if (islandA == islandB)
        {
            //Debug.LogWarning("Attempted to create a path between the same arena. Path creation aborted.");
            return false; // Do not create a path between the same arena
        }
        // Path to null arena?
        if (islandA == null || islandB == null)
        {
            //Debug.LogWarning("Attempted to create a path with a null arena reference. Path creation aborted.");
            return false; // Do not create a path if either arena reference is null
        }
        // Path already exists?
        foreach (WindPath path in windPaths)
        {
            // Island islandScript = island.GetComponent<Island>();
            if ((path.islandA == islandA && path.islandB == islandB) || (path.islandA == islandB && path.islandB == islandA))
            {
                //Debug.LogWarning("Attempted to create a duplicate path between " + islandA.gameObjectRef.name + " and " + islandB.gameObjectRef.name + ". Path creation aborted.");
                return false; // Do not create a duplicate path
            }
        }
        LayerMask launchPadMask = LayerMask.GetMask("CloudSurface");
        // LayerMask launchPadMask = Physics.AllLayers;
        // Get the edge points on each island that are facing each other and spawn a wind launch prefab on each of those points
        // Edge points are created in the editor

        // Get the closest unoccupied point for island a
        Vector3[] islandAPermieterPoints = islandA.GetPointsClosestToIsland(islandB);
        Vector3 spawnPointA = islandAPermieterPoints[islandAPermieterPoints.Length-1];
        foreach (Vector3 point in islandAPermieterPoints)
        {
            Collider[] colliders = Physics.OverlapSphere(point, 10, launchPadMask, QueryTriggerInteraction.Collide);
            
            if (colliders.Length > 0) {
                continue;
            }
            else
            {
                spawnPointA = point;
                break;
            }
        }
        // Do the same for island b
        Vector3[] islandBPermieterPoints = islandB.GetPointsClosestToIsland(islandA);
        Vector3 spawnPointB = islandBPermieterPoints[islandBPermieterPoints.Length-1];
        foreach (Vector3 point in islandBPermieterPoints)
        {
            Collider[] colliders = Physics.OverlapSphere(point, 10, launchPadMask, QueryTriggerInteraction.Collide);
          
            if (colliders.Length > 0) {
                continue;
            }
            else
            {
                spawnPointB = point;
                break;
            }
        }
        // Create the launch pads for each island
        GameObject launchPadA = Instantiate(windLaunchPrefab,
                    spawnPointA,
                    Quaternion.identity);
        GameObject launchPadB = Instantiate(windLaunchPrefab,
                    spawnPointB,
                    Quaternion.identity);

        // Create a gameobject with a splinecontainer component
        GameObject pathObj = new GameObject();
        SplineContainer splineContainer = pathObj.AddComponent<SplineContainer>();
        // Make path a child of the parent
        splineContainer.gameObject.transform.parent = transform;
        // Create a spline to be held by the container
        Spline spline = splineContainer.Spline;

        BezierKnot Aknot = new BezierKnot(spawnPointA);
        BezierKnot Bknot = new BezierKnot(spawnPointB);

        // Add points to the spline at the positions of the two islands
        spline.Add(Aknot, TangentMode.AutoSmooth); // Start pos
        spline.Add(Bknot, TangentMode.AutoSmooth); // End pos

        // These points make the path curve outward from the island's land
        // Vector3 pointBeforeA = splineContainer.EvaluatePosition(spline, .13f);
        // Vector3 pointBeforeB = splineContainer.EvaluatePosition(spline, .87f);
        Vector3 islandADir = (spawnPointA - islandA.transform.position).normalized;
        Vector3 pointBeforeA = spawnPointA + islandADir * 10f;

        Vector3 islandBDir = (spawnPointB - islandB.transform.position).normalized;
        Vector3 pointBeforeB = spawnPointB + islandBDir * 10f;

        // Add points to the spline before the end points to point the entrances to the
        // path at the islands
        BezierKnot beforeAknot = new BezierKnot(pointBeforeA);
        BezierKnot beforeBknot = new BezierKnot(pointBeforeB);
        spline.Insert(1, beforeAknot, TangentMode.AutoSmooth); // Start pos
        spline.Insert(spline.Count-1, beforeBknot, TangentMode.AutoSmooth); // End pos

        // Create the TestWindTunnelSetup component and set its variables to the spline and launch pads
        // This is the part that actually moves the player along the path when they enter the launch pad trigger
        TestWindTunnelSetup tunnel = pathObj.AddComponent<TestWindTunnelSetup>();
        tunnel.pathSpline = spline;
        tunnel.startLaunchPad = launchPadA.GetComponent<WindLaunchPad>();
        tunnel.endLaunchPad = launchPadB.GetComponent<WindLaunchPad>();

        // Create a Path instance to hold the path's data
        // Path pathData = new Path(arenaA, arenaB, splineContainer.gameObject);
        WindPath pathData = new WindPath(islandA, islandB, pathObj);
        
        windPaths.Add(pathData); // Store reference to the created path

        return true;
    }

    // Loops through each path and checks for overlaps with objects along the
    // spline's position. If there are overlaps, make a new knot and make it move away from the object that was overlapped
    public void AvoidWindPathObstacles()
    {
        foreach (WindPath path in windPaths)
        {
            int numberOfNewKnots = 0;
            for (int i = 2; i < 8; i++)
            {
                (bool isOverlapping, Vector3 position, Collider collider) result = CheckWindPathCollision(path, i/10f);
                if (result.isOverlapping)
                {
                    Vector3 dirFromObstacle = (result.position - result.collider.bounds.center).normalized;
                    Vector3 newPos = result.position + (dirFromObstacle * 10f);

                    SplineContainer splineContainer = path.windTunnel.GetComponent<SplineContainer>();
                    Spline spline = splineContainer.Spline;

                    BezierKnot newKnot = new BezierKnot(newPos);
                    spline.Insert(numberOfNewKnots + 2, newKnot, TangentMode.AutoSmooth);
                    numberOfNewKnots++;
                }
            }
        }
    }

    // Gets a place along the spline and checks if it's colliding with anything
    // If yes, then return true with the pos, else false w the pos
    (bool, Vector3, Collider) CheckWindPathCollision(WindPath path, float splinePercentage)
    {
        SplineContainer splineContainer = path.windTunnel.GetComponent<SplineContainer>();
        Spline spline = splineContainer.Spline;

        Vector3 pos = splineContainer.EvaluatePosition(spline, splinePercentage);
        Collider[] colliders = Physics.OverlapSphere(pos, 5, islandMask, QueryTriggerInteraction.Ignore);
            
        (bool, Vector3, Collider) result;

        if (colliders.Length > 0) {
            result = (true, pos, colliders[0]);
            Debug.Log(path + " PATH, POS " + pos + " POS, PERCENTAGE " + splinePercentage);
            return result;
        }
        else
        {
            result = (false, pos, null);
            return result;
        }
    }
}
