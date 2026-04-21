using Category5.Enemies;
using UnityEngine;

public class Arena : MonoBehaviour
{
    // Stores if this arena is an eye or not, players can drop into eyes
    private bool isEye;
    // Stores if the arena is a "hidden" arena. Hidden arenas will be initially inaccessible, and
    // paths connected to them will also be hidden
    private bool isHidden;
    // Arenas have a capsule collider surrounding them that defines
    // the arena's boundaries (The storm cloud walls)
    private CapsuleCollider arenaBounds;

    private TriggerVolume trigger;

    public EnemySpawner enemySpawner;
    private SpriteRenderer mapSprite;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mapSprite = GetComponentInChildren<SpriteRenderer>();
        mapSprite.color = Color.orange;
    }

    // Update is called once per frame
    void Update()
    {
        if (enemySpawner != null && enemySpawner.isCleared)
        {
            mapSprite.color = Color.softBlue;
        }
    }
}
