using UnityEngine;

public class BossArena : Arena
{
    public override void GenerateArena(GameObject enemySpawnerPrefab)
    {
        // TEMPORARY! Replace basic shapes with prefabs of premade arenas and stuff

        radius = scaleFactor*60;
        
        // Create a new arena as a cube primitive GameObject
        GameObject island = GameObject.CreatePrimitive(PrimitiveType.Cube);

        // Set transform
        island.transform.position = transform.position;
        island.transform.localScale = new Vector3(
                                            radius,
                                            2*scaleFactor,
                                            radius
                                            );
        
        // Add a Rigidbody to make it interact with physics
        island.AddComponent<Rigidbody>();
        island.GetComponent<Rigidbody>().isKinematic = true;


        // arena.GetComponent<MeshRenderer>().material = islandMaterial;

        // Add a capsule collider to define the bounds of the arena
        arenaBounds = gameObject.AddComponent<CapsuleCollider>();
        arenaBounds.radius = radius; // Set the radius
        arenaBounds.height = 100f; // Set the height
        arenaBounds.center = new Vector3(0, 10, 0); // Center the collider on the arena
        arenaBounds.isTrigger = true; // Set the collider to be a trigger so players can fall through

        // Add TriggerVolume script that will invoke an event when that capsule
        // collider trigger is entered. This will automatically get the capsule trigger collider
        trigger = gameObject.AddComponent<TriggerVolume>();
        trigger.targetLayers = LayerMask.GetMask("Player");
        trigger.targetTag = "Player";

        // Add cloud layer
        GameObject cloudLayer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        DestroyImmediate(cloudLayer.GetComponent<CapsuleCollider>()); // Remove the cloud layer's collider
        cloudLayer.transform.position = new Vector3(transform.position.x, transform.position.y - 1f, transform.position.z);
        cloudLayer.transform.localScale = new Vector3(
                                        165 * scaleFactor,
                                        1f,
                                        165 * scaleFactor
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
                                        165 * scaleFactor,
                                        165 * scaleFactor,
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
        Debug.Log("Created arena with scale factor " + scaleFactor);
    }
}
