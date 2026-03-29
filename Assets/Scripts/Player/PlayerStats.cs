using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;
using Category5.Items;

namespace Category5.Player
{
    // return type for damage calculations so callers know if a crit happened
    public struct DamageResult
    {
        public int damage;
        public bool wasCrit;
    }
    
    // component attached to player to calculate all stat modifications
    // reads base stats from PlayerClass SO and applies item bonuses on top
    public class PlayerStats : NetworkBehaviour
    {
        // class data source (set by PlayerClassManager when class loads)
        private PlayerClass _classData;
        
        // fallback base stats used before class data is loaded
        [Header("fallback base stats (used before class loads)")]
        [SerializeField] private float fallbackAttackDamage = 12f;
        [SerializeField] private int fallbackMaxHealth = 100;
        [SerializeField] private int fallbackMaxMana = 80;
        [SerializeField] private float fallbackMoveSpeed = 7f;
        [SerializeField] private float fallbackAttackSpeed = 1f;
        [SerializeField] private float fallbackDodgeCooldown = 2f;
        [SerializeField] private float fallbackManaRegenRate = 2f;
        [SerializeField] private float fallbackArmor = 10f;
        [SerializeField] private float fallbackCritChance = 0.05f;
        [SerializeField] private float fallbackCritDamage = 1.5f;
        
        // reference to player inventory (for reading items)
        private PlayerInventory _playerInventory;

        // cached item bonuses (reset and recalculated from inventory)
        private float _damageMultiplier = 1f;
        private int _flatDamageBonus = 0;
        private int _maxHealthBonus = 0;
        private int _maxManaBonus = 0;
        private float _dodgeCooldownReduction = 0f;
        private int _lifestealAmount = 0;
        private float _moveSpeedMultiplier = 1f;
        private float _attackSpeedMultiplier = 1f;
        private float _manaRegenMultiplier = 1f;
        private float _manaCostReduction = 0f;
        private float _armorBonus = 0f;
        private float _critChanceBonus = 0f;
        private float _critDamageBonus = 0f;

        // dynamic bonus hp from item behaviours (e.g. Recharging Shield) that changes frequently
        private int _dynamicMaxHealthBonus = 0;

        // base stat accessors (from class data or fallback)
        public float BaseAttackDamage => _classData != null ? _classData.baseAttackDamage : fallbackAttackDamage;
        private int BaseMaxHealth => _classData != null ? _classData.baseMaxHealth : fallbackMaxHealth;
        private int BaseMaxMana => _classData != null ? _classData.baseMaxMana : fallbackMaxMana;
        private float BaseMoveSpeed => _classData != null ? _classData.baseMoveSpeed : fallbackMoveSpeed;
        private float BaseAttackSpeed => _classData != null ? _classData.baseAttackSpeed : fallbackAttackSpeed;
        private float BaseDodgeCooldown => _classData != null ? _classData.baseDodgeCooldown : fallbackDodgeCooldown;
        private float BaseManaRegenRate => _classData != null ? _classData.baseManaRegenRate : fallbackManaRegenRate;
        private float BaseArmor => _classData != null ? _classData.baseArmor : fallbackArmor;
        private float BaseCritChance => _classData != null ? _classData.baseCritChance : fallbackCritChance;
        private float BaseCritDamage => _classData != null ? _classData.baseCritDamage : fallbackCritDamage;
        
        // melee coefficient accessors (from class data or fallback)
        public float LightAttackCoefficient => _classData != null ? _classData.lightAttackCoefficient : 0.8f;
        public float HeavyAttackCoefficient => _classData != null ? _classData.heavyAttackCoefficient : 1.5f;

        // public accessors for final stats (base + items)
        public float DamageMultiplier => _damageMultiplier;
        public int FlatDamageBonus => _flatDamageBonus;
        public int MaxHealthBonus => _maxHealthBonus + _dynamicMaxHealthBonus;
        public int TotalMaxHealth => BaseMaxHealth + _maxHealthBonus + _dynamicMaxHealthBonus;
        public int MaxManaBonus => _maxManaBonus;
        public int TotalMaxMana => BaseMaxMana + _maxManaBonus;
        public float DodgeCooldownReduction => _dodgeCooldownReduction;
        public float EffectiveDodgeCooldown => Mathf.Max(0.5f, BaseDodgeCooldown - _dodgeCooldownReduction);
        public int LifestealAmount => _lifestealAmount;
        public float MoveSpeedMultiplier => _moveSpeedMultiplier;
        public float AttackSpeedMultiplier => _attackSpeedMultiplier;
        public float ManaRegenMultiplier => _manaRegenMultiplier;
        public float ManaCostReduction => _manaCostReduction;
        public float EffectiveMoveSpeed => BaseMoveSpeed * _moveSpeedMultiplier;
        public float TotalArmor => BaseArmor + _armorBonus;
        public float TotalCritChance => Mathf.Clamp01(BaseCritChance + _critChanceBonus);
        public float TotalCritDamage => BaseCritDamage + _critDamageBonus;
        public float EffectiveManaRegenRate => BaseManaRegenRate * _manaRegenMultiplier;
        public bool HasClassData => _classData != null;

        // base stat accessors for item behaviour calculations
        public int BaseMaxHealthValue => BaseMaxHealth;

        // set dynamic max hp bonus from item behaviours (e.g. Recharging Shield)
        // this avoids triggering a full recalculation for frequently changing values
        public void SetDynamicMaxHealthBonus(int bonus)
        {
            if (_dynamicMaxHealthBonus != bonus)
            {
                _dynamicMaxHealthBonus = bonus;
                OnStatsChanged?.Invoke();
            }
        }

        // event for when stats change
        public event System.Action OnStatsChanged;

        // called by PlayerClassManager after class is resolved
        public void SetClassData(PlayerClass classData)
        {
            _classData = classData;
            Debug.Log($"PlayerStats: class data set to {classData.className} (ATK={classData.baseAttackDamage}, HP={classData.baseMaxHealth}, Armor={classData.baseArmor})");
            RecalculateStats();
        }

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
        public void RecalculateStats()
        {
            // reset item bonuses to defaults
            _damageMultiplier = 1f;
            _flatDamageBonus = 0;
            _maxHealthBonus = 0;
            _maxManaBonus = 0;
            _dodgeCooldownReduction = 0f;
            _lifestealAmount = 0;
            _moveSpeedMultiplier = 1f;
            _attackSpeedMultiplier = 1f;
            _manaRegenMultiplier = 1f;
            _manaCostReduction = 0f;
            _armorBonus = 0f;
            _critChanceBonus = 0f;
            _critDamageBonus = 0f;
            
            // apply items from inventory (tier-aware)
            if (_playerInventory != null)
            {
                var items = _playerInventory.GetAllItemsWithTier();
                foreach (var (item, tier) in items)
                {
                    ApplyItemEffects(item, tier);
                }
            }

            Debug.Log($"PlayerStats recalculated: ATK={BaseAttackDamage}, DmgMult={_damageMultiplier:F2}, FlatDmg={_flatDamageBonus}, MaxHP={TotalMaxHealth}, Armor={TotalArmor:F1}, Crit={TotalCritChance:P0}/{TotalCritDamage:F1}x");

            OnStatsChanged?.Invoke();
        }
        
        // applies item effects to stats, scaled by tier
        private void ApplyItemEffects(ItemData item, int tier)
        {
            foreach (var effect in item.Effects)
            {
                float scaledValue = ItemData.GetTierScaledValue(effect.value, tier);

                switch (effect.effectType)
                {
                    case ItemEffectType.DamageMultiplier:
                        _damageMultiplier += scaledValue;
                        break;

                    case ItemEffectType.MaxHealthBonus:
                        _maxHealthBonus += Mathf.RoundToInt(scaledValue);
                        break;

                    case ItemEffectType.DodgeCooldownReduction:
                        _dodgeCooldownReduction += scaledValue;
                        break;

                    case ItemEffectType.FlatDamageBonus:
                        _flatDamageBonus += Mathf.RoundToInt(scaledValue);
                        break;

                    case ItemEffectType.Lifesteal:
                        _lifestealAmount += Mathf.RoundToInt(scaledValue);
                        break;

                    case ItemEffectType.MoveSpeedMultiplier:
                        _moveSpeedMultiplier += scaledValue;
                        break;

                    case ItemEffectType.AttackSpeedMultiplier:
                        _attackSpeedMultiplier += scaledValue;
                        break;
                    
                    case ItemEffectType.MaxManaBonus:
                        _maxManaBonus += Mathf.RoundToInt(scaledValue);
                        break;
                    
                    case ItemEffectType.ManaRegenMultiplier:
                        _manaRegenMultiplier += scaledValue;
                        break;
                    
                    case ItemEffectType.ManaCostReduction:
                        _manaCostReduction += scaledValue;
                        break;
                    
                    case ItemEffectType.ArmorBonus:
                        _armorBonus += scaledValue;
                        break;
                    
                    case ItemEffectType.CritChanceBonus:
                        _critChanceBonus += scaledValue;
                        break;
                    
                    case ItemEffectType.CritDamageBonus:
                        _critDamageBonus += scaledValue;
                        break;

                    default:
                        Debug.LogWarning($"PlayerStats: Unhandled item effect type {effect.effectType}");
                        break;
                }
            }
        }

        // calculates final damage from a coefficient (fraction of class attack damage)
        // coefficient of 1.0 = 100% of attack damage, 2.5 = 250%, etc.
        // crit rolls happen server-side for authority
        public DamageResult CalculateDamage(float damageCoefficient)
        {
            float effectiveMultiplier = GetEffectiveDamageMultiplier();
            float rawDmg = BaseAttackDamage * damageCoefficient * effectiveMultiplier + _flatDamageBonus;
            
            // crit roll (server-side)
            bool wasCrit = UnityEngine.Random.value < TotalCritChance;
            if (wasCrit)
            {
                rawDmg *= TotalCritDamage;
            }
            
            return new DamageResult
            {
                damage = Mathf.Max(1, Mathf.RoundToInt(rawDmg)),
                wasCrit = wasCrit
            };
        }
        
        // convenience overload that returns just the int damage (for call sites that don't need crit info)
        public int CalculateFlatDamage(float damageCoefficient)
        {
            return CalculateDamage(damageCoefficient).damage;
        }

        // applies armor damage reduction using lol-style formula: dmg * 100 / (100 + armor)
        public int ApplyArmor(int incomingDamage)
        {
            float armor = TotalArmor;
            if (armor <= 0f) return incomingDamage;
            float reduced = incomingDamage * 100f / (100f + armor);
            return Mathf.Max(1, Mathf.RoundToInt(reduced));
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

        public float GetEffectiveAttackSpeedMultiplier()
        {
            float effective = _attackSpeedMultiplier;
            if (_temporaryMultipliers.TryGetValue("attackSpeed", out var boost))
            {
                effective += boost.multiplier;
            }
            return effective;
        }
    }
}
