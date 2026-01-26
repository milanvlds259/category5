using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;
using Category5.Items;

namespace Category5.PowerUps
{
    // component attached to player to calculate all stat modifications
    // reads from PlayerInventory (items) and manages temporary stat buffs
    public class PlayerStats : NetworkBehaviour
    {
        [Header("base stats (reference only)")]
        [SerializeField] private int baseMaxHealth = 100;
        [SerializeField] private float baseDodgeCooldown = 2f;
        [SerializeField] private float baseMoveSpeed = 5f;
        
        // reference to player inventory (for reading items)
        private PlayerInventory _playerInventory;

        // cached calculated stats
        private float _damageMultiplier = 1f;
        private int _flatDamageBonus = 0;
        private int _maxHealthBonus = 0;
        private float _dodgeCooldownReduction = 0f;
        private int _lifestealAmount = 0;
        private float _moveSpeedMultiplier = 1f;
        private float _attackSpeedMultiplier = 1f;

        // public accessors for other systems to use
        public float DamageMultiplier => _damageMultiplier;
        public int FlatDamageBonus => _flatDamageBonus;
        public int MaxHealthBonus => _maxHealthBonus;
        public int TotalMaxHealth => baseMaxHealth + _maxHealthBonus;
        public float DodgeCooldownReduction => _dodgeCooldownReduction;
        public float EffectiveDodgeCooldown => Mathf.Max(0.5f, baseDodgeCooldown - _dodgeCooldownReduction);
        public int LifestealAmount => _lifestealAmount;
        public float MoveSpeedMultiplier => _moveSpeedMultiplier;
        public float AttackSpeedMultiplier => _attackSpeedMultiplier;
        public float EffectiveMoveSpeed => baseMoveSpeed * _moveSpeedMultiplier;

        // event for when stats change
        public event System.Action OnStatsChanged;

        public override void OnNetworkSpawn()
        {
            // get reference to player inventory
            _playerInventory = GetComponent<PlayerInventory>();
            if (_playerInventory != null)
            {
                _playerInventory.OnInventoryChanged += RecalculateStats;
            }
            
            // recalculate stats on spawn
            RecalculateStats();
        }

        public override void OnNetworkDespawn()
        {
            if (_playerInventory != null)
            {
                _playerInventory.OnInventoryChanged -= RecalculateStats;
            }
        }

        // recalculates all stats from items
        private void RecalculateStats()
        {
            // reset to base values
            _damageMultiplier = 1f;
            _flatDamageBonus = 0;
            _maxHealthBonus = 0;
            _dodgeCooldownReduction = 0f;
            _lifestealAmount = 0;
            _moveSpeedMultiplier = 1f;
            _attackSpeedMultiplier = 1f;
            
            // apply items from inventory
            if (_playerInventory != null)
            {
                var items = _playerInventory.GetAllItems();
                foreach (var item in items)
                {
                    ApplyItemEffects(item);
                }
            }

            Debug.Log($"PlayerStats recalculated: DamageMult={_damageMultiplier:F2}, FlatDmg={_flatDamageBonus}, MaxHP+={_maxHealthBonus}, DodgeCD-={_dodgeCooldownReduction:F2}, Lifesteal={_lifestealAmount}, MoveSpeed*={_moveSpeedMultiplier:F2}");

            OnStatsChanged?.Invoke();
        }
        
        // applies item effects to stats
        private void ApplyItemEffects(ItemData item)
        {
            foreach (var effect in item.Effects)
            {
                switch (effect.effectType)
                {
                    case ItemEffectType.DamageMultiplier:
                        _damageMultiplier += effect.value;
                        break;

                    case ItemEffectType.MaxHealthBonus:
                        _maxHealthBonus += Mathf.RoundToInt(effect.value);
                        break;

                    case ItemEffectType.DodgeCooldownReduction:
                        _dodgeCooldownReduction += effect.value;
                        break;

                    case ItemEffectType.FlatDamageBonus:
                        _flatDamageBonus += Mathf.RoundToInt(effect.value);
                        break;

                    case ItemEffectType.Lifesteal:
                        _lifestealAmount += Mathf.RoundToInt(effect.value);
                        break;

                    case ItemEffectType.MoveSpeedMultiplier:
                        _moveSpeedMultiplier += effect.value;
                        break;

                    case ItemEffectType.AttackSpeedMultiplier:
                        _attackSpeedMultiplier += effect.value;
                        break;

                    default:
                        Debug.LogWarning($"PlayerStats: Unhandled item effect type {effect.effectType}");
                        break;
                }
            }
        }

        // calculates final damage output given base damage
        public int CalculateDamage(int baseDamage)
        {
            // use effective damage multiplier (includes temporary boosts)
            float effectiveMultiplier = GetEffectiveDamageMultiplier();
            float modified = baseDamage * effectiveMultiplier + _flatDamageBonus;
            return Mathf.RoundToInt(modified);
        }
        
        // apply a temporary stat multiplier (used by abilities like Fighter R)
        private Dictionary<string, (float multiplier, float remaining)> _temporaryMultipliers = new Dictionary<string, (float, float)>();
        
        public void ApplyTemporaryMultiplier(string statName, float bonusMultiplier, float duration)
        {
            _temporaryMultipliers[statName] = (bonusMultiplier, duration);
        }
        
        private void Update()
        {
            if (!IsSpawned) return;
            
            // update temporary multipliers
            var keys = new List<string>(_temporaryMultipliers.Keys);
            foreach (var key in keys)
            {
                var (multiplier, remaining) = _temporaryMultipliers[key];
                remaining -= Time.deltaTime;
                
                if (remaining <= 0)
                {
                    _temporaryMultipliers.Remove(key);
                }
                else
                {
                    _temporaryMultipliers[key] = (multiplier, remaining);
                }
            }
        }
        
        // get effective damage multiplier including temporary boosts
        public float GetEffectiveDamageMultiplier()
        {
            float effective = _damageMultiplier;
            if (_temporaryMultipliers.TryGetValue("damage", out var boost))
            {
                effective += boost.multiplier;
            }
            return effective;
        }
        
        // get effective speed multiplier including temporary boosts
        public float GetEffectiveSpeedMultiplier()
        {
            float effective = _moveSpeedMultiplier;
            if (_temporaryMultipliers.TryGetValue("speed", out var boost))
            {
                effective += boost.multiplier;
            }
            return effective;
        }
    }
}
