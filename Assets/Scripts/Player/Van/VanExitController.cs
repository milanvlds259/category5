using UnityEngine;
using Category5.Player.WindRiding;

namespace Category5.Player.Van
{
    [RequireComponent(typeof(Collider))]
    public class VanExitController : MonoBehaviour
    {
        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            if (!player.IsOwner && !IsOffline()) return;

            WindRiderController rider = player.GetComponent<WindRiderController>();
            if (rider == null) return;

            if (rider.IsWindRiding) return;

            rider.StartVanDescent();
        }

        private bool IsOffline()
        {
            return Unity.Netcode.NetworkManager.Singleton == null ||
                   !Unity.Netcode.NetworkManager.Singleton.IsListening;
        }
    }
}
