using UnityEngine;
using Unity.Netcode;
using Category5.Core;

namespace Category5.Core
{
    /// <summary>
    /// Manages the Homebase hub world logic.
    /// Handles local player spawning when offline and transitions to networking.
    /// </summary>
    public class HomebaseManager : MonoBehaviour
    {
        public static HomebaseManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform fallbackSpawnPoint;

        private GameObject _localPlayerInstance;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // If NetworkManager is already running (e.g. we returned from a game), 
            // NetworkManagerBootstrap will handle spawning.
            // If not, we spawn a local "offline" player.
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                SpawnLocalPlayer();
            }
            
            // Subscribe to network start to cleanup local player if we host/join while in Homebase
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientStarted += HandleNetworkStarted;
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientStarted -= HandleNetworkStarted;
            }
        }

        private void SpawnLocalPlayer()
        {
            if (_localPlayerInstance != null) return;

            // Find a van spawn point
            var spawnPoint = PlayerSpawnPoint.GetNextVanSpawnPoint();
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.transform.position : (fallbackSpawnPoint != null ? fallbackSpawnPoint.position : Vector3.zero);
            Quaternion spawnRot = spawnPoint != null ? spawnPoint.transform.rotation : (fallbackSpawnPoint != null ? fallbackSpawnPoint.rotation : Quaternion.identity);

            if (playerPrefab == null)
            {
                Debug.LogError("HomebaseManager: No player prefab assigned!");
                return;
            }

            _localPlayerInstance = Instantiate(playerPrefab, spawnPos, spawnRot);
            _localPlayerInstance.name = "LocalPlayer_Offline";
            
            // PlayerController.Start() handles the offline camera setup
            Debug.Log("HomebaseManager: Spawned local offline player.");
        }

        private void HandleNetworkStarted()
        {
            // When networking starts, we want to destroy our local "dummy" player
            // and let NetworkManager spawn the real networked player object.
            if (_localPlayerInstance != null)
            {
                Debug.Log("HomebaseManager: Network started, destroying local offline player.");
                Destroy(_localPlayerInstance);
                _localPlayerInstance = null;
            }
        }
    }
}
