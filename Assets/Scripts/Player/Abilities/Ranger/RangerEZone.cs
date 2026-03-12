using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Category5.Boss;
using Category5.Enemies;
using Category5.Player;

namespace Category5
{
    // networked dot zone for ranger e
    [RequireComponent(typeof(NetworkObject))]
    public class RangerEZone : NetworkBehaviour
    {
        [Header("zone settings")]
        [SerializeField] private float zoneRadius = 5f;
        [SerializeField] private float zoneDuration = 6f;
        [SerializeField] private float tickInterval = 0.5f;
        [SerializeField] private float slowMultiplier = 0.6f;
        [SerializeField] private int damage = 20;

        private ulong _ownerClientId;
        private PlayerStats _ownerStats;
        private float _tickTimer;
        private string _slowSourceId;

        public static event Action<Vector3, float> OnZoneSpawned;
        public static event Action<Vector3> OnZoneExpired;

        public void Initialize(ulong ownerClientId, PlayerStats ownerStats, int baseDamage,
            float radius, float duration, float damageTickInterval, float slowMult)
        {
            _ownerClientId = ownerClientId;
            _ownerStats = ownerStats;
            damage = baseDamage;
            zoneRadius = radius;
            zoneDuration = duration;
            tickInterval = damageTickInterval;
            slowMultiplier = slowMult;
            _slowSourceId = $"RangerE_{ownerClientId}";
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _tickTimer = tickInterval;
                Invoke(nameof(ExpireZone), zoneDuration);
            }

            NotifyZoneSpawnedClientRpc(transform.position, zoneRadius);
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;

            _tickTimer -= Time.fixedDeltaTime;
            if (_tickTimer > 0f) return;

            _tickTimer = tickInterval;
            TickZone();
        }

        private void TickZone()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, zoneRadius, ~0, QueryTriggerInteraction.Ignore);
            HashSet<int> processedTargets = new HashSet<int>();

            foreach (Collider collider in colliders)
            {
                EnemyBase enemy = collider.GetComponent<EnemyBase>();
                if (enemy == null)
                {
                    enemy = collider.GetComponentInParent<EnemyBase>();
                }

                if (enemy != null)
                {
                    int enemyId = enemy.GetInstanceID();
                    if (processedTargets.Add(enemyId) && !enemy.IsDead)
                    {
                        enemy.TakeDamage(CalculateTickDamage());
                        enemy.ApplyMovementModifier(slowMultiplier, tickInterval + 0.1f, _slowSourceId);
                    }

                    continue;
                }

                BossBase boss = collider.GetComponent<BossBase>();
                if (boss == null)
                {
                    boss = collider.GetComponentInParent<BossBase>();
                }

                if (boss != null)
                {
                    int bossId = boss.GetInstanceID();
                    if (processedTargets.Add(bossId))
                    {
                        boss.TakeDamage(CalculateTickDamage());
                        boss.ApplyMovementModifier(slowMultiplier, tickInterval + 0.1f, _slowSourceId);
                    }
                }
            }
        }

        private int CalculateTickDamage()
        {
            return _ownerStats != null ? _ownerStats.CalculateDamage(damage) : damage;
        }

        private void ExpireZone()
        {
            if (!IsServer) return;

            NotifyZoneExpiredClientRpc(transform.position);
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        [ClientRpc]
        private void NotifyZoneSpawnedClientRpc(Vector3 position, float radius)
        {
            OnZoneSpawned?.Invoke(position, radius);
        }

        [ClientRpc]
        private void NotifyZoneExpiredClientRpc(Vector3 position)
        {
            OnZoneExpired?.Invoke(position);
        }
    }
}