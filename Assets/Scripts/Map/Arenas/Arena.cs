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

    public bool isBoss=false;
    
    // Stores if the arena is a "hidden" arena. Hidden arenas will be initially inaccessible, and
    // paths connected to them will also be hidden
    public bool isHidden;

    // Arenas have a capsule collider surrounding them that defines
    // the arena's boundaries (The storm cloud walls)
    public CapsuleCollider arenaBounds;

    public TriggerVolume trigger;

    // The map parent, this gets set in the create arena method in map generator script
    public Transform parent;

    public List<GameObject> spawners = new List<GameObject>();

    public Sprite arenaMapSprite;
    protected SpriteRenderer mapSprite;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // if (enemySpawner != null && enemySpawner.isCleared)
        // {
        //     mapSprite.color = Color.softBlue;
        // }
    }

    public virtual void GenerateArena(GameObject enemySpawnerPrefab)
    {
    }
}
