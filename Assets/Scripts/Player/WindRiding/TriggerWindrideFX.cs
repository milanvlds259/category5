using UnityEngine;
using Category5.Audio;
using Category5.Player;

public class TriggerWindrideFX : MonoBehaviour
{
    [SerializeField] public GameObject windRideFX;

    void OnEnable()
    {
        WindRideEvents.OnRideStarted += HandleRideStarted;
        WindRideEvents.OnRideEnded += HandleRideEnded;
    }

    void OnDisable()
    {
        WindRideEvents.OnRideStarted -= HandleRideStarted;
        WindRideEvents.OnRideEnded -= HandleRideEnded;
    }

    private void HandleRideStarted(PlayerController player, Vector3 position)
    {
        if (windRideFX != null)
        {
            windRideFX.SetActive(true);
        }
    }

    private void HandleRideEnded(PlayerController player, Vector3 position, Vector3 exitVelocity)
    {
        if (windRideFX != null)
        {
            windRideFX.SetActive(false);
        }
    }
}