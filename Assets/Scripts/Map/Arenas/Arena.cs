using System.Collections.Generic;
using Category5.Enemies;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

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
        // GameObject cloudLayer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        // DestroyImmediate(cloudLayer.GetComponent<CapsuleCollider>()); // Remove the cloud layer's collider
        // cloudLayer.transform.position = new Vector3(transform.position.x, transform.position.y - 1f, transform.position.z);
        // cloudLayer.transform.localScale = new Vector3(
        //                                 radius * 2,
        //                                 1f,
        //                                 radius * 2
        //                                 );
        // cloudLayer.transform.parent = transform;
        // // cloudLayer.GetComponent<MeshRenderer>().material = cloudMaterial; // Set the cloud material
        // MeshCollider cloudCollider = cloudLayer.AddComponent<MeshCollider>();
        // cloudCollider.convex = true; // Set convex to true so it can be a trigger
        // cloudCollider.isTrigger = true; // Add a mesh collider and set it to be a trigger so players can fall through
        // cloudLayer.layer = 8;


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

    protected void CreateIsland(Vector3 position, Vector3 scale)
    {
        // Replace with instantiating a prefab of an island once we have those
        // GameObject island = GameObject.CreatePrimitive(PrimitiveType.Cube);
        // islands.Add(island);

        // // Set transform
        // island.transform.position = position;
        // island.transform.localScale = scale;

        // // Add a Rigidbody to make it interact with physics
        // island.AddComponent<Rigidbody>();
        // island.GetComponent<Rigidbody>().isKinematic = true;

        // island.AddComponent<Island>();

        GameObject island = Instantiate(islandPrefabs[Random.Range(0, islandPrefabs.Length)], position, Quaternion.identity);
        islands.Add(island);
    }
}
