using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Category5.Player;

namespace Category5
{
    [RequireComponent(typeof(NetworkObject))]
    public class HealBeaconZone : NetworkBehaviour
    {
        [Header("Heal Settings")]
        [SerializeField] private LayerMask playerLayers;

        private ulong _ownerClientId;
        private float _healPerTick;
        private float _tickInterval;
        private float _duration;
        private float _radius;
        private float _elapsed;
        private float _tickTimer;

        public static event Action<Vector3, int> OnHealTick;
        public static event Action<Vector3, float> OnBeaconSpawned;
        public static event Action<Vector3> OnBeaconExpired;

        public void Initialize(ulong ownerClientId, float healPerTick, float tickInterval, float duration, float radius)
        {
            _ownerClientId = ownerClientId;
            _healPerTick = healPerTick;
            _tickInterval = tickInterval;
            _duration = duration;
            _radius = radius;
        }

        public void NotifySpawned()
        {
            if (!IsServer) return;
            NotifyBeaconSpawnedClientRpc(transform.position, _radius);
        }

        private void Update()
        {
            if (!IsServer) return;

            _elapsed += Time.deltaTime;
            _tickTimer += Time.deltaTime;

            if (_tickTimer >= _tickInterval)
            {
                _tickTimer = 0f;
                HealAllies();
            }

            if (_elapsed >= _duration)
            {
                NotifyBeaconExpiredClientRpc(transform.position);
                NetworkObject.Despawn(true);
            }
        }

        private void HealAllies()
        {
            Collider[] hits = playerLayers.value != 0
                ? Physics.OverlapSphere(transform.position, _radius, playerLayers)
                : Physics.OverlapSphere(transform.position, _radius);

            var healedTargets = new HashSet<int>();
            int healAmount = Mathf.RoundToInt(_healPerTick);
            int healedCount = 0;

            foreach (Collider collider in hits)
            {
                PlayerController player = collider.GetComponentInParent<PlayerController>();
                if (player == null) continue;
                if (player.IsDead.Value) continue;

                int id = player.GetInstanceID();
                if (!healedTargets.Add(id)) continue;

                player.Heal(healAmount);
                healedCount++;
            }

            if (healedCount > 0)
            {
                NotifyHealTickClientRpc(transform.position, healAmount);
            }
        }

        [ClientRpc]
        private void NotifyHealTickClientRpc(Vector3 position, int healAmount)
        {
            OnHealTick?.Invoke(position, healAmount);
        }

        [ClientRpc]
        private void NotifyBeaconSpawnedClientRpc(Vector3 position, float radius)
        {
            OnBeaconSpawned?.Invoke(position, radius);
        }

        [ClientRpc]
        private void NotifyBeaconExpiredClientRpc(Vector3 position)
        {
            OnBeaconExpired?.Invoke(position);
        }
    }
}
