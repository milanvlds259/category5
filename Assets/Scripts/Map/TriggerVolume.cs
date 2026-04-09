using System;
using UnityEngine;
using UnityEngine.Events;
using WebSocketSharp;

// This script requires the GameObject to have a 3D collider
[RequireComponent(typeof(Collider))]
public class TriggerVolume : MonoBehaviour
{
    [SerializeField] private Collider collider;

    public LayerMask targetLayers;
    public string targetTag;

    public event Action OnTriggerVolumeEnter;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // If the collider wasn't set in the inspector
        if (collider == null){}
        {
            // Default is the first collider on this gameObject
            collider = GetComponent<Collider>();

            // Try to get first collider that is a trigger
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider coll in colliders)
            {
                if (coll.isTrigger)
                {
                    collider = coll;
                    break;
                }
            }
        }
        // No triggers
        if (!collider.isTrigger)
        {
            Debug.LogError("TriggerVolume Collider must be trigger, no trigger colliders found on " + gameObject.name);
            return;
        }
        // Only collide with specified target layers (default to all layers)
        if (targetLayers == 0)
        {
            Debug.LogError("Colliding layers set to nothing on TriggerVolume " + gameObject.name + "!");
        }
        collider.includeLayers = targetLayers; // Only include specified layers
        collider.excludeLayers = ~targetLayers; // Exclude all other layers
    }

    void OnTriggerEnter(Collider other)
    {
        // The object that entered the trigger must have the specified
        // targetTag if any was specified. If not, then it don't matter
        if (!targetTag.IsNullOrEmpty())
        {
            if (other.gameObject.tag == targetTag)
            {
                OnTriggerVolumeEnter.Invoke();
            }
        }
        else
        {
            Debug.Log(other.gameObject.name);
            OnTriggerVolumeEnter.Invoke();
        }
    }
}
