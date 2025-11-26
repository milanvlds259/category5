using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace Category5.Core
{
    // ensures networkmanager persists across scenes and handles player spawning
    // NOTE TO SELF attach this to the same gameobject as NetworkManager in the main menu scene
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkManagerBootstrap : MonoBehaviour
    {
        private static bool isInitialized = false;
        
        [SerializeField] private string menuSceneName = "MainMenu";
        [SerializeField] private GameObject playerPrefab; // assign player prefab here instead of NetworkManager
        
        private NetworkManager networkManager;
        
        private void Awake()
        {
            // only keep the first instance
            if (isInitialized)
            {
                Destroy(gameObject);
                return;
            }
            
            isInitialized = true;
            DontDestroyOnLoad(gameObject);
            
            networkManager = GetComponent<NetworkManager>();
            
            // clear the NetworkManager's player prefab to prevent auto-spawning
            // we handle spawning manually after scene load
            if (networkManager != null && playerPrefab == null)
            {
                // if not set on bootstrap, grab it from NetworkManager before clearing
                playerPrefab = networkManager.NetworkConfig.PlayerPrefab;
            }
            
            if (networkManager != null)
            {
                networkManager.NetworkConfig.PlayerPrefab = null;
            }
            
            Debug.Log("NetworkManagerBootstrap: NetworkManager initialized, manual player spawning enabled");
        }
        
        private void Start()
        {
            // subscribe to scene load events - must wait for Start since SceneManager may not exist in Awake
            if (networkManager != null)
            {
                // subscribe when network starts
                networkManager.OnServerStarted += OnServerStarted;
            }
        }
        
        private void OnServerStarted()
        {
            // now we can subscribe to scene events
            if (networkManager.SceneManager != null)
            {
                networkManager.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
            }
        }
        
        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.OnServerStarted -= OnServerStarted;
                
                if (networkManager.SceneManager != null)
                {
                    networkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
                }
            }
        }
        
        private void OnSceneLoadCompleted(string sceneName, LoadSceneMode loadSceneMode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
        {
            // only the server handles spawning
            if (!networkManager.IsServer) return;
            
            // don't spawn players in the menu scene
            if (sceneName == menuSceneName) return;
            
            Debug.Log($"NetworkManagerBootstrap: Scene '{sceneName}' loaded, spawning players");
            
            // reset spawn index for fresh spawning
            PlayerSpawnPoint.ResetSpawnIndex();
            
            // spawn players for all connected clients
            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                SpawnPlayerForClient(clientId);
            }
        }
        
        private void SpawnPlayerForClient(ulong clientId)
        {
            // check if player already has a spawned object
            if (networkManager.ConnectedClients.TryGetValue(clientId, out var client))
            {
                if (client.PlayerObject != null)
                {
                    Debug.Log($"NetworkManagerBootstrap: Player {clientId} already has a player object, repositioning");
                    RepositionPlayer(client.PlayerObject);
                    return;
                }
            }
            
            // get spawn point
            var spawnPoint = PlayerSpawnPoint.GetNextSpawnPoint();
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.transform.position : Vector3.zero;
            Quaternion spawnRot = spawnPoint != null ? spawnPoint.transform.rotation : Quaternion.identity;
            
            // spawn the player prefab
            if (playerPrefab == null)
            {
                Debug.LogError("NetworkManagerBootstrap: No player prefab assigned");
                return;
            }
            
            GameObject playerInstance = Instantiate(playerPrefab, spawnPos, spawnRot);
            NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
            
            if (networkObject != null)
            {
                networkObject.SpawnAsPlayerObject(clientId);
                Debug.Log($"NetworkManagerBootstrap: Spawned player for client {clientId} at {spawnPos}");
            }
            else
            {
                Debug.LogError("NetworkManagerBootstrap: Player prefab missing NetworkObject component");
                Destroy(playerInstance);
            }
        }
        
        private void RepositionPlayer(NetworkObject playerObject)
        {
            var spawnPoint = PlayerSpawnPoint.GetNextSpawnPoint();
            if (spawnPoint != null)
            {
                // disable character controller if present to allow position change
                var controller = playerObject.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                
                playerObject.transform.position = spawnPoint.transform.position;
                playerObject.transform.rotation = spawnPoint.transform.rotation;
                
                if (controller != null) controller.enabled = true;
            }
        }
        
        private void OnApplicationQuit()
        {
            isInitialized = false;
        }
        
        // resets the initialized state for editor play mode
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            isInitialized = false;
        }
    }
}
