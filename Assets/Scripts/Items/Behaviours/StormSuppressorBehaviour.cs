using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Category5.Enemies;
using Category5.Boss;

namespace Category5.Items
{
    // storm suppressor: hitting an enemy reduces their damage output for a duration
    // re-hitting them while the debuff is active resets the timer (does not stack)
    // less effective on bosses by design
    public class StormSuppressorBehaviour : ItemBehaviour
    {
        [SerializeField] private float[] debuffDuration    = { 3f, 3.5f, 4f, 5f, 6f };
        [SerializeField] private float[] debuffStrength    = { 0.15f, 0.18f, 0.21f, 0.25f, 0.30f }; // fraction of damage removed
        [SerializeField] private float[] bossDebuffStrength = { 0.05f, 0.06f, 0.07f, 0.09f, 0.11f }; // weaker on bosses

        // active debuffs: target object → coroutine handle for cancellation
        private readonly Dictionary<int, Coroutine> _activeDebuffs = new Dictionary<int, Coroutine>();

        protected override void OnInitialize()
        {
            if (!IsServer) return;
            PlayerCombat.OnPlayerDealtDamage += OnDealtDamage;
            //Debug.Log($"[StormSuppressor] initialized for client {OwnerClientId}");
        }

        protected override void OnTierChanged(int oldTier, int newTier) { }

        public override void OnRemoved()
        {
            if (PlayerCombat != null)
                PlayerCombat.OnPlayerDealtDamage -= OnDealtDamage;

            // remove all active debuffs
            foreach (var kvp in _activeDebuffs)
                if (kvp.Value != null) StopCoroutine(kvp.Value);
            _activeDebuffs.Clear();
        }

        private void OnDealtDamage(int damage, GameObject target, bool wasCrit)
        {
            if (target == null) { /*Debug.Log("[StormSuppressor] OnDealtDamage target is null");*/ return; }

            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);

            // check target type and apply appropriate debuff strength
            var enemy = target.GetComponentInParent<EnemyBase>();
            var boss  = target.GetComponentInParent<BossBase>();

            //Debug.Log($"[StormSuppressor] hit '{target.name}' — enemy={enemy != null}, boss={boss != null}");

            if (enemy != null)
            {
                ApplyDebuff(target.GetInstanceID(), enemy, null, debuffStrength[idx], debuffDuration[idx]);
            }
            else if (boss != null)
            {
                ApplyDebuff(target.GetInstanceID(), null, boss, bossDebuffStrength[idx], debuffDuration[idx]);
            }
            else
            {
                //Debug.Log("[StormSuppressor] target has neither EnemyBase nor BossBase — debuff not applied");
            }
        }

        private void ApplyDebuff(int targetId, EnemyBase enemy, BossBase boss, float strength, float duration)
        {
            // cancel existing timer if already debuffed — reset the duration
            if (_activeDebuffs.TryGetValue(targetId, out var existing))
            {
                if (existing != null) StopCoroutine(existing);
                _activeDebuffs.Remove(targetId);
                //Debug.Log($"[StormSuppressor] refreshing debuff on id={targetId}");
            }

            float multiplier = 1f - strength;
            //Debug.Log($"[StormSuppressor] applying debuff — multiplier={multiplier:F2}, duration={duration}s on {(enemy != null ? enemy.name : boss?.name)}");

            if (enemy != null) enemy.DamageOutputMultiplier = multiplier;
            if (boss != null)  boss.DamageOutputMultiplier  = multiplier;

            var coroutine = StartCoroutine(ExpireDebuff(targetId, enemy, boss, duration));
            _activeDebuffs[targetId] = coroutine;
        }

        private IEnumerator ExpireDebuff(int targetId, EnemyBase enemy, BossBase boss, float duration)
        {
            yield return new WaitForSeconds(duration);

            if (enemy != null) enemy.DamageOutputMultiplier = 1f;
            if (boss != null)  boss.DamageOutputMultiplier  = 1f;

            _activeDebuffs.Remove(targetId);
            //Debug.Log($"[StormSuppressor] debuff expired on id={targetId}");
        }

        public override object[] GetFormatValues(int tier)
        {
            int idx = Mathf.Clamp(tier - 1, 0, 4);
            return new object[]
            {
                Mathf.RoundToInt(debuffStrength[idx] * 100f),
                debuffDuration[idx],
                Mathf.RoundToInt(bossDebuffStrength[idx] * 100f)
            };
        }
    }
}
