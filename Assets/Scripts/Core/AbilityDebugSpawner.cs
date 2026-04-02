using UnityEngine;
using Unity.Netcode;
using Category5.Player;

namespace Category5
{
    // debug tool to spawn dummy players and enemies for testing abilities
    // press f5 to spawn a dummy player, f6 to spawn an enemy
    public class AbilityDebugSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private float spawnDistance = 5f;
        
        [Header("Enemy Spawning")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private float enemySpawnDistance = 10f;
        
        private void Update()
        {
            // only allow in editor or development builds
            if (!Debug.isDebugBuild && !Application.isEditor) return;
            
            // only server can spawn
            if (!NetworkManager.Singleton.IsServer) return;
            
            // f5 to spawn dummy player
            if (Input.GetKeyDown(KeyCode.F5))
            {
                SpawnDummyPlayer();
            }
            
            // f6 to spawn enemy
            if (Input.GetKeyDown(KeyCode.F6))
            {
                SpawnEnemy();
            }
        }
        
        private void SpawnDummyPlayer()
        {
            if (playerPrefab == null)
            {
                Debug.LogWarning("AbilityDebugSpawner: No player prefab assigned!");
                return;
            }
            
            // find local player to spawn near
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null)
            {
                Debug.LogWarning("AbilityDebugSpawner: No local player found!");
                return;
            }
            
            // spawn position offset from local player
            Vector3 spawnPos = localPlayer.transform.position + localPlayer.transform.right * spawnDistance;
            
            // instantiate and spawn
            GameObject dummyObj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            NetworkObject netObj = dummyObj.GetComponent<NetworkObject>();
            
            if (netObj != null)
            {
                netObj.Spawn();
                // Debug.Log($"Spawned dummy player at {spawnPos}");
            }
            else
            {
                Debug.LogError("Player prefab missing NetworkObject component!");
                Destroy(dummyObj);
            }
        }
        
        private void SpawnEnemy()
        {
            if (enemyPrefab == null)
            {
                Debug.LogWarning("AbilityDebugSpawner: No enemy prefab assigned!");
                return;
            }
            
            // find local player to spawn near
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null)
            {
                Debug.LogWarning("AbilityDebugSpawner: No local player found!");
                return;
            }
            
            // spawn position in front of local player
            Vector3 spawnPos = localPlayer.transform.position + localPlayer.transform.forward * enemySpawnDistance;
            
            // instantiate and spawn
            GameObject enemyObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            NetworkObject netObj = enemyObj.GetComponent<NetworkObject>();
            
            if (netObj != null)
            {
                netObj.Spawn();
                // Debug.Log($"Spawned enemy at {spawnPos}");
            }
            else
            {
                Debug.LogError("Enemy prefab missing NetworkObject component!");
                Destroy(enemyObj);
            }
        }
        
        private PlayerController FindLocalPlayer()
        {
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var player in players)
            {
                if (player.IsOwner)
                {
                    return player;
                }
            }
            return null;
        }
    }
}
