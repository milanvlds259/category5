using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Category5.PowerUps
{
    // component attached to player to track active power-ups and calculate modified stats
    public class PlayerStats : NetworkBehaviour
    {
        [Header("base stats (reference only)")]
        [SerializeField] private int baseMaxHealth = 100;
        [SerializeField] private float baseDodgeCooldown = 2f;

        // networked list of power-up ids the player has acquired
        private NetworkList<Unity.Collections.FixedString64Bytes> acquiredPowerUpIds;

        // cached calculated stats
        private float _damageMultiplier = 1f;
        private int _flatDamageBonus = 0;
        private int _maxHealthBonus = 0;
        private float _dodgeCooldownReduction = 0f;
        private int _lifestealAmount = 0;

        // public accessors for other systems to usee
        public float DamageMultiplier => _damageMultiplier;
        public int FlatDamageBonus => _flatDamageBonus;
        public int MaxHealthBonus => _maxHealthBonus;
        public int TotalMaxHealth => baseMaxHealth + _maxHealthBonus;
        public float DodgeCooldownReduction => _dodgeCooldownReduction;
        public float EffectiveDodgeCooldown => Mathf.Max(0.5f, baseDodgeCooldown - _dodgeCooldownReduction);
        public int LifestealAmount => _lifestealAmount;

        // event for when stats change
        public event System.Action OnStatsChanged;

        private void Awake()
        {
            acquiredPowerUpIds = new NetworkList<Unity.Collections.FixedString64Bytes>();
        }

        public override void OnNetworkSpawn()
        {
            // subscribe to list changes
            acquiredPowerUpIds.OnListChanged += OnPowerUpListChanged;
            
            // recalculate stats on spawn in case we have power-ups
            RecalculateStats();
        }

        public override void OnNetworkDespawn()
        {
            acquiredPowerUpIds.OnListChanged -= OnPowerUpListChanged;
        }

        private void OnPowerUpListChanged(NetworkListEvent<Unity.Collections.FixedString64Bytes> changeEvent)
        {
            RecalculateStats();
        }

        // called by server to add a power-up to this player
        public void AddPowerUp(string powerUpId)
        {
            if (!IsServer)
            {
                Debug.LogWarning("PlayerStats.AddPowerUp should only be called on server");
                return;
            }

            acquiredPowerUpIds.Add(new Unity.Collections.FixedString64Bytes(powerUpId));
            Debug.Log($"PlayerStats: Added power-up {powerUpId} to player {OwnerClientId}");
        }

        // recalculates all stats from the list of acquired power-ups
        private void RecalculateStats()
        {
            // reset to base values
            _damageMultiplier = 1f;
            _flatDamageBonus = 0;
            _maxHealthBonus = 0;
            _dodgeCooldownReduction = 0f;
            _lifestealAmount = 0;

            // get power-up registry
            var registry = PowerUpRegistry.Instance;
            if (registry == null)
            {
                Debug.LogWarning("PlayerStats: PowerUpRegistry not found, cannot recalculate stats");
                return;
            }

            // apply each power-up's effect
            foreach (var powerUpId in acquiredPowerUpIds)
            {
                var powerUp = registry.GetPowerUpById(powerUpId.ToString());
                if (powerUp == null)
                {
                    Debug.LogWarning($"PlayerStats: Power-up '{powerUpId}' not found in registry");
                    continue;
                }

                ApplyPowerUpEffect(powerUp);
            }

            Debug.Log($"PlayerStats recalculated: DamageMult={_damageMultiplier}, FlatDmg={_flatDamageBonus}, MaxHP+={_maxHealthBonus}, DodgeCD-={_dodgeCooldownReduction}, Lifesteal={_lifestealAmount}");

            OnStatsChanged?.Invoke();
        }

        private void ApplyPowerUpEffect(PowerUpData powerUp)
        {
            switch (powerUp.EffectType)
            {
                case PowerUpEffectType.DamageMultiplier:
                    // additive instead of multiplicative cuz im too lazy to figure it out rn
                    _damageMultiplier += powerUp.EffectValue;
                    break;

                case PowerUpEffectType.MaxHealthBonus:
                    _maxHealthBonus += Mathf.RoundToInt(powerUp.EffectValue);
                    break;

                case PowerUpEffectType.DodgeCooldownReduction:
                    _dodgeCooldownReduction += powerUp.EffectValue;
                    break;

                case PowerUpEffectType.FlatDamageBonus:
                    _flatDamageBonus += Mathf.RoundToInt(powerUp.EffectValue);
                    break;

                case PowerUpEffectType.Lifesteal:
                    _lifestealAmount += Mathf.RoundToInt(powerUp.EffectValue);
                    break;
            }
        }

        // calculates final damage output given base damage
        public int CalculateDamage(int baseDamage)
        {
            float modified = baseDamage * _damageMultiplier + _flatDamageBonus;
            return Mathf.RoundToInt(modified);
        }

        // returns a list of power-up ids for ui display
        public List<string> GetAcquiredPowerUpIds()
        {
            var list = new List<string>();
            foreach (var id in acquiredPowerUpIds)
            {
                list.Add(id.ToString());
            }
            return list;
        }

        // clears all power-ups (for game reset)
        public void ClearPowerUps()
        {
            if (!IsServer) return;
            acquiredPowerUpIds.Clear();
        }
    }
}
