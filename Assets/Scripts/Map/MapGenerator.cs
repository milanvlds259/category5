using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public int numberOfEyes;

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
        for (int i = 0; i < numberOfEyes; i++)
        {
            CreateArena(new Vector3(Random.Range(0, 100), Random.Range(-10, 0), Random.Range(0, 100)));
        }
    }

    void CreateArena(Vector3 inputPos)
    {
        // TEMPORARY! Replace cylinders with prefabs of premade arenas and stuff
        

        // Make sure the arena does not overlap with existing arenas
        int maxIterations = 10;
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
            Collider[] colliders = Physics.OverlapBox(arena.transform.position, new Vector3(arena.transform.localScale.x / 2, arena.transform.localScale.y * 100, arena.transform.localScale.z / 2));
            if (colliders.Length > 1) // More than one collider means overlap
            {
                Destroy(arena); // Remove the overlapping arena
                // Generate a new random position
                inputPos = new Vector3(Random.Range(0, 100), Random.Range(-10, 0), Random.Range(0, 100));

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
                return; // No overlap, exit the function
            }
        }
    }
}
