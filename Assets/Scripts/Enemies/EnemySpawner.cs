using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;
using Category5.Core;

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
        
        [Header("wave settings")]
        [SerializeField] private int enemiesPerWave = 3;
        [SerializeField] private int totalWaves = 2;
        [SerializeField] private float spawnInterval = 0.5f;
        [SerializeField] private float waveCooldown = 5f;
        [SerializeField] private bool autoStartOnSpawn = true;
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
        private bool _isActive = false;
        private int _effectiveEnemiesPerWave; // scaled by multiplier each round
        
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
            
            // apply enemy scaling
            _effectiveEnemiesPerWave = Mathf.RoundToInt(enemiesPerWave * enemyMultiplier);
            if (_effectiveEnemiesPerWave < 1) _effectiveEnemiesPerWave = 1;
        }
        
        // static helper to reset and start all spawners in the scene
        public static void StartAllSpawners(float enemyMultiplier = 1f)
        {
            var spawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
            foreach (var spawner in spawners)
            {
                spawner.ResetSpawner(enemyMultiplier);
                spawner.StartSpawning();
            }
            Debug.Log($"EnemySpawner: started {spawners.Length} spawners with {enemyMultiplier}x enemy multiplier");
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
            GameObject enemyObject = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
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
        
        private Transform GetNextSpawnPoint()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return transform; // use spawner position as fallback
            }
            
            if (useRandomSpawnPoints)
            {
                return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            }
            else
            {
                Transform point = spawnPoints[_nextSpawnPointIndex];
                _nextSpawnPointIndex = (_nextSpawnPointIndex + 1) % spawnPoints.Length;
                return point;
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
            if (_aliveEnemies.Count == 0 && !isSpawning && _currentWave >= totalWaves)
            {
                // direct server callback for robust progression
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && GameFlowManager.Instance != null)
                {
                    GameFlowManager.Instance.NotifySpawnerCompleted(this);
                }

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
