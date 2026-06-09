using System.Collections.Generic;
using Category5.Enemies;
using UnityEngine;
using Category5.MapEnums;

public class Island : MonoBehaviour
{
    [System.Serializable]
    public struct SpawnerData
    {
        public Transform spawnerMarker; // The position and rotation of the spawner, set in the inspector
        public Vector3 spawnerBounds;
        public TriggerVolume trigger; // Placed on the island prefab and set in the inspector
    };
    [SerializeField] public SpawnerData[] spawnerDataArray;

    [SerializeField] public IslandTag[] islandTags;
}
