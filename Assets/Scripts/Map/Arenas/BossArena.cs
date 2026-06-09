using UnityEngine;

public class BossArena : Arena
{
    public override void GenerateArena()
    {

        // TEMPORARY! Replace basic shapes with prefabs of premade arenas and stuff
        
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
    }
}
