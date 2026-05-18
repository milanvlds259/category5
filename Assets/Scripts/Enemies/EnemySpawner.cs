using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;
using Category5.Core;
using Category5.Items;
using Random = UnityEngine.Random;

namespace Category5.Enemies
{
    // server-authoritative spawner for enemies
    // place in scene with spawn points and configure in inspector
    public class EnemySpawner : NetworkBehaviour
    {
        [Header("enemy configuration")]
        [SerializeField] private EnemyData enemyData;
        [SerializeField] private GameObject enemyPrefab;
        
        [Header("spawn points")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private bool useRandomSpawnPoints = true;
        public Vector3 spawnBounds;
        
        [Header("wave settings")]
        [SerializeField] private int enemiesPerWave = 3;
        [SerializeField] private int totalWaves = 2;
        [SerializeField] private float spawnInterval = 0.5f;
        [SerializeField] private float waveCooldown = 5f;
        public bool autoStartOnSpawn = true;
        public bool startOnTrigger = false;
        public TriggerVolume triggerVolume;
        [SerializeField] private float spawnOccupancyRadius = 0.6f; // avoid spawning inside other colliders
        
        [Header("runtime state")]
        [SerializeField] private bool isSpawning = false;
        
        // tracking
        private List<EnemyBase> _aliveEnemies = new List<EnemyBase>();
        private int _currentWave = 0;
        private int _spawnedThisWave = 0;
        private int _nextSpawnPointIndex = 0;
        private float _spawnTimer;
        private float _waveTimer;
        public bool _isActive = false;
        private bool hasStartedSpawning = false; // If the spawner has ever started spawning
        private int _effectiveEnemiesPerWave; // scaled by multiplier each round
        public bool isCleared = false; // Is true when all enemies that would be spawned from this spawner have been defeated
        
        // item drop
        [SerializeField] private GameObject itemDropPrefab;
        private bool _isResetting = false;
        
        // per-spawner collection tracking (Story 002: TR-item-002)
        private SpawnerCollectionTracker _collectionTracker = new SpawnerCollectionTracker();
        
        // events
        public static event Action<EnemySpawner> OnAllEnemiesDefeated;
        public static event Action<EnemySpawner, int> OnWaveStarted;
        public static event Action<EnemySpawner, int> OnWaveCompleted;
        
        // =====================================
        // lifecycle
        // =====================================
        
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            
            _effectiveEnemiesPerWave = enemiesPerWave;

            if (autoStartOnSpawn)
            {
                StartSpawning();
            }
            else if (startOnTrigger)
            {
                if (triggerVolume != null)
                {
                    triggerVolume.OnTriggerVolumeEnter += StartSpawning;
                }
                else
                {
                    Debug.LogError("No triggerVolume, spawning won't work");
                }
            }
        }
        
        private void Update()
        {
            if (!IsServer) return;
            if (!_isActive) return;
            
            // handle wave cooldown
            if (_waveTimer > 0f)
            {
                _waveTimer -= Time.deltaTime;
                if (_waveTimer <= 0f)
                {
                    // start the next wave automatically when cooldown expires
                    StartNextWave();
                }
                return;
            }
            
            // handle spawning
            if (isSpawning)
            {
                _spawnTimer -= Time.deltaTime;
                if (_spawnTimer <= 0f)
                {
                    SpawnEnemy();
                    _spawnTimer = spawnInterval;
                }
            }
        }
        
        // =====================================
        // spawning control
        // =====================================
        
        public void StartSpawning()
        {
            if (!IsServer) return;
            if (hasStartedSpawning) return;
            
            hasStartedSpawning = true;
            _isActive = true;
            _currentWave = 0;
            StartNextWave();
        }
        
        public void StopSpawning()
        {
            if (!IsServer) return;
            
            _isActive = false;
            isSpawning = false;
        }
        
        // resets spawner for a new round, despawning any remaining enemies
        public void ResetSpawner(float enemyMultiplier = 1f)
        {
            if (!IsServer) return;
            
            _isResetting = true;
            
            // despawn any remaining alive enemies
            for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
            {
                if (_aliveEnemies[i] != null)
                {
                    var netObj = _aliveEnemies[i].GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn();
                    }
                }
            }
            _aliveEnemies.Clear();
            
            // reset state
            _currentWave = 0;
            _spawnedThisWave = 0;
            _nextSpawnPointIndex = 0;
            _spawnTimer = 0f;
            _waveTimer = 0f;
            _isActive = false;
            isSpawning = false;
            hasStartedSpawning = false;
            
            // apply enemy scaling
            _effectiveEnemiesPerWave = Mathf.RoundToInt(enemiesPerWave * enemyMultiplier);
            if (_effectiveEnemiesPerWave < 1) _effectiveEnemiesPerWave = 1;
            
            // reset cleared state so ItemDrop can spawn again next round
            isCleared = false;
            
            // clear per-spawner collection tracking (Story 002: TR-item-002)
            _collectionTracker.Clear();
            
            _isResetting = false;
        }

        // static helper to reset all spawners in the scene
        public static void ResetAllSpawners(float enemyMultiplier = 1f)
        {
            var spawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
            foreach (var spawner in spawners)
            {
                spawner.ResetSpawner(enemyMultiplier);
            }
        }
        
        // static helper to start all spawners in the scene
        public static void StartAllSpawners()
        {
            var spawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
            foreach (var spawner in spawners)
            {
                spawner.StartSpawning();
            }
            // Debug.Log($"EnemySpawner: started {spawners.Length} spawners with {enemyMultiplier}x enemy multiplier");
        }
        
        private void StartNextWave()
        {
            if (_currentWave >= totalWaves)
            {
                // all waves complete
                isSpawning = false;
                CheckAllEnemiesDefeated();
                return;
            }
            
            _currentWave++;
            _spawnedThisWave = 0;
            isSpawning = true;
            _spawnTimer = 0f; // spawn first enemy immediately
            
            OnWaveStarted?.Invoke(this, _currentWave);
        }
        
        private void SpawnEnemy()
        {
            if (_spawnedThisWave >= _effectiveEnemiesPerWave)
            {
                // wave spawning complete
                isSpawning = false;
                OnWaveCompleted?.Invoke(this, _currentWave);

                // re-check completion now that spawning has stopped
                // this handles the case where enemies were killed before the wave officially finished spawning
                CheckAllEnemiesDefeated();
                
                // wait for enemies to die or start next wave after cooldown
                if (_currentWave < totalWaves)
                {
                    _waveTimer = waveCooldown;
                    // next wave will start automatically when _waveTimer reaches 0 in Update
                }
                return;
            }
            
            // choose a spawn point but avoid points that are currently occupied
            /*
            Transform spawnPoint = null;
            int attempts = (spawnPoints != null) ? spawnPoints.Length : 1;
            for (int i = 0; i < attempts; i++)
            {
                Transform candidate = GetNextSpawnPoint();
                if (candidate == null)
                {
                    continue;
                }

                // check occupancy
                bool occupied = Physics.OverlapSphere(candidate.position, spawnOccupancyRadius).Length > 0;
                if (!occupied)
                {
                    spawnPoint = candidate;
                    break;
                }
                // if occupied, try next candidate (loop will call GetNextSpawnPoint again)
            }

            if (spawnPoint == null)
            {
                // fallback to a direct GetNextSpawnPoint if all were occupied
                spawnPoint = GetNextSpawnPoint();
            }
            if (spawnPoint == null)
            {
                Debug.LogWarning("EnemySpawner: No spawn points available!");
                return;
            }*/

            Vector3 spawnPoint = transform.position; // default to spawner position if no spawn points defined
            int attempts = 100;
            for (int i = 0; i < attempts; i++)
            {
                Vector3 candidate = GetNextSpawnPoint();

                // check occupancy
                Collider[] colliders = new Collider[1];
                Physics.OverlapSphereNonAlloc(candidate, spawnOccupancyRadius, colliders, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                bool occupied = colliders[0] != null;
                if (!occupied)
                {
                    spawnPoint = candidate;
                    break;
                }
                // if occupied, try next candidate (loop will call GetNextSpawnPoint again)
            }
            
            GameObject prefab = enemyPrefab;
            if (prefab == null && enemyData != null)
            {
                prefab = enemyData.enemyPrefab;
            }
            
            if (prefab == null)
            {
                Debug.LogError("EnemySpawner: No enemy prefab assigned!");
                return;
            }
            
            // spawn the enemy
            GameObject enemyObject = Instantiate(prefab, spawnPoint, Quaternion.identity);
            NetworkObject networkObject = enemyObject.GetComponent<NetworkObject>();
            
            if (networkObject == null)
            {
                Debug.LogError("EnemySpawner: Enemy prefab must have a NetworkObject component!");
                Destroy(enemyObject);
                return;
            }
            
            networkObject.Spawn();
            
            // register with this spawner
            EnemyBase enemy = enemyObject.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.SetSpawner(this);
                _aliveEnemies.Add(enemy);
            }
            
            _spawnedThisWave++;
        }
        
        private void StartNextWaveIfReady()
        {
            if (!_isActive) return;
            if (isSpawning) return;
            if (_currentWave >= totalWaves) return;
            
            StartNextWave();
        }
        
        private Vector3 GetNextSpawnPoint()
        {
            /*
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return transform.position; // use spawner position as fallback
            }*/
            
            if (useRandomSpawnPoints)
            {
                return new Vector3(
                    Random.Range(transform.position.x - spawnBounds.x/2, transform.position.x + spawnBounds.x/2),
                    Random.Range(transform.position.y - spawnBounds.y/2, transform.position.y + spawnBounds.y/2),
                    Random.Range(transform.position.z - spawnBounds.z/2, transform.position.z + spawnBounds.z/2));
                //return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            }
            else
            {
                Transform point = spawnPoints[_nextSpawnPointIndex];
                _nextSpawnPointIndex = (_nextSpawnPointIndex + 1) % spawnPoints.Length;
                return point.position;
            }
        }
        
        // =====================================
        // enemy death tracking
        // =====================================
        
        public void OnEnemyDied(EnemyBase enemy)
        {
            _aliveEnemies.Remove(enemy);
            CheckAllEnemiesDefeated();
        }
        
        private void CheckAllEnemiesDefeated()
        {
            if (!IsServer) return;
            if (isCleared) return;
            
            if (_aliveEnemies.Count == 0 && !isSpawning && _currentWave >= totalWaves)
            {
                // direct server callback for robust progression
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && GameFlowManager.Instance != null)
                {
                    GameFlowManager.Instance.NotifySpawnerCompleted(this);
                }
                
                // spawn item drop at spawner position (server-authoritative)
                if (!_isResetting && itemDropPrefab != null)
                {
                    GameObject itemDropObject = Instantiate(itemDropPrefab, transform.position, Quaternion.identity);
                    NetworkObject networkObject = itemDropObject.GetComponent<NetworkObject>();
                    if (networkObject == null)
                    {
                        Debug.LogError("EnemySpawner: ItemDrop prefab must have a NetworkObject component!", this);
                        Destroy(itemDropObject);
                    }
                    else
                    {
                        networkObject.Spawn();
                        ItemDrop itemDrop = itemDropObject.GetComponent<ItemDrop>();
                        if (itemDrop != null)
                        {
                            itemDrop.SetSpawner(this);
                        }
                    }
                }
                
                // mark cleared only after spawn attempt succeeds
                isCleared = true;
                
                OnAllEnemiesDefeated?.Invoke(this);
            }
        }
        
        // =====================================
        // public accessors
        // =====================================
        
        public int AliveEnemyCount => _aliveEnemies.Count;
        public int CurrentWave => _currentWave;
        public int TotalWaves => totalWaves;
        public bool IsActive => _isActive;
        public bool IsSpawning => isSpawning;
        
        // =====================================
        // per-spawner collection tracking (Story 002: TR-item-002)
        // =====================================
        
        /// <summary>
        /// Returns true if the given client has already collected the item from this spawner this round.
        /// </summary>
        public bool HasPlayerCollected(ulong clientId)
        {
            return _collectionTracker.HasPlayerCollected(clientId);
        }

        /// <summary>
        /// Attempts to mark the given client as having collected the item from this spawner.
        /// Returns true if the client was newly marked, false if already collected.
        /// Server-authoritative callers (ItemDrop) should use this return value as an atomic gate.
        /// </summary>
        public bool MarkCollected(ulong clientId)
        {
            return _collectionTracker.TryMarkCollected(clientId);
        }
        
        // =====================================
        // gizmos
        // =====================================
        
        private void OnDrawGizmos()
        {
            // draw spawner
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            
            // draw spawn points
            if (spawnPoints != null)
            {
                Gizmos.color = Color.cyan;
                foreach (var point in spawnPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawWireSphere(point.position, 0.3f);
                        Gizmos.DrawLine(transform.position, point.position);
                    }
                }
            }

            // Draws the bounds of the spawn area (between minSpawnBounds and maxSpawnBounds)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, spawnBounds);
        }
        
        private void OnDrawGizmosSelected()
        {
            // draw detection range from enemy data
            if (enemyData != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                foreach (var point in spawnPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawWireSphere(point.position, enemyData.detectionRange);
                    }
                }
            }
        }
    }
}
