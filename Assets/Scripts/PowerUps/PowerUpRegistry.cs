using System.Collections.Generic;
using UnityEngine;

namespace Category5.PowerUps
{
    // singleton registry that holds all available power-ups
    // this should be placed in the scene or on a persistent gameobject
    public class PowerUpRegistry : MonoBehaviour
    {
        public static PowerUpRegistry Instance { get; private set; }

        [Header("available power-ups")]
        [SerializeField] private List<PowerUpData> allPowerUps = new List<PowerUpData>();

        private Dictionary<string, PowerUpData> _powerUpLookup;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                BuildLookup();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void BuildLookup()
        {
            _powerUpLookup = new Dictionary<string, PowerUpData>();
            foreach (var powerUp in allPowerUps)
            {
                if (powerUp == null) continue;
                
                if (_powerUpLookup.ContainsKey(powerUp.UniqueId))
                {
                    Debug.LogWarning($"PowerUpRegistry: Duplicate power-up id '{powerUp.UniqueId}'");
                    continue;
                }
                _powerUpLookup[powerUp.UniqueId] = powerUp;
            }
            Debug.Log($"PowerUpRegistry: Loaded {_powerUpLookup.Count} power-ups");
        }

        // get power-up by its unique id
        public PowerUpData GetPowerUpById(string id)
        {
            if (_powerUpLookup == null) BuildLookup();
            
            _powerUpLookup.TryGetValue(id, out var powerUp);
            return powerUp;
        }

        // get power-up by index
        public PowerUpData GetPowerUpByIndex(int index)
        {
            if (index < 0 || index >= allPowerUps.Count) return null;
            return allPowerUps[index];
        }

        // get total count
        public int PowerUpCount => allPowerUps.Count;

        // get all power-ups
        public IReadOnlyList<PowerUpData> AllPowerUps => allPowerUps;

        // get random power-ups for selection (excludes duplicates)
        public List<PowerUpData> GetRandomPowerUps(int count)
        {
            var result = new List<PowerUpData>();
            var available = new List<PowerUpData>(allPowerUps);
            
            // shuffle and take first N
            for (int i = 0; i < count && available.Count > 0; i++)
            {
                int randomIndex = Random.Range(0, available.Count);
                result.Add(available[randomIndex]);
                available.RemoveAt(randomIndex);
            }

            return result;
        }

        // get power-up indices for networking (random selection)
        public int[] GetRandomPowerUpIndices(int count)
        {
            var indices = new List<int>();
            for (int i = 0; i < allPowerUps.Count; i++)
            {
                indices.Add(i);
            }

            // fisher-yates shuffle
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            // take first N
            var result = new int[Mathf.Min(count, indices.Count)];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = indices[i];
            }

            return result;
        }
    }
}
