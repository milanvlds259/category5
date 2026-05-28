using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Category5.Player;

namespace Category5.Player.Van
{
    /// <summary>
    /// Gradually heals players while they are standing inside the van.
    /// This should be attached to a trigger volume that covers the van's interior.
    /// Works for both networked (server-side) and offline modes.
    /// Uses Physics.OverlapBox for reliable detection, avoiding OnTriggerExit misses during teleports.
    /// </summary>
    public class VanHealingZone : MonoBehaviour
    {
        [Header("Heal Settings")]
        [Tooltip("Amount of health restored per tick")]
        [SerializeField] private int healAmount = 1;
        
        [Tooltip("Seconds between healing ticks")]
        [SerializeField] private float healInterval = 0.2f;

        [Header("Detection")]
        [SerializeField] private LayerMask playerLayer = -1;

        private float _timer;
        private BoxCollider _boxCollider;

        private void Awake()
        {
            _boxCollider = GetComponent<BoxCollider>();
            if (_boxCollider == null)
            {
                Debug.LogError($"[VanHealingZone] No BoxCollider found on {gameObject.name}. Healing will not work.");
            }
        }

        private void Update()
        {
            // healing logic only runs on the server (or in offline mode)
            if (!IsServer()) return;
            if (_boxCollider == null) return;

            _timer += Time.deltaTime;
            if (_timer >= healInterval)
            {
                _timer = 0f;
                HealPlayersInZone();
            }
        }

        private void HealPlayersInZone()
        {
            // Use OverlapBox to find all players currently inside the volume.
            // This is more reliable than OnTriggerEnter/Exit which can be missed during teleports.
            Vector3 center = transform.TransformPoint(_boxCollider.center);
            Vector3 halfExtents = Vector3.Scale(_boxCollider.size, transform.lossyScale) * 0.5f;
            Quaternion orientation = transform.rotation;

            Collider[] hitColliders = Physics.OverlapBox(center, halfExtents, orientation, playerLayer, QueryTriggerInteraction.Collide);
            
            // Track healed players to avoid healing multiple colliders on the same player
            HashSet<PlayerController> healedThisTick = new HashSet<PlayerController>();

            foreach (var hitCollider in hitColliders)
            {
                PlayerController player = hitCollider.GetComponentInParent<PlayerController>();
                if (player != null && !player.IsDead.Value && !healedThisTick.Contains(player))
                {
                    player.Heal(healAmount);
                    healedThisTick.Add(player);
                }
            }
        }

        private bool IsServer()
        {
            // if no network manager, we are in offline mode (effectively the server)
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                return true;
            }
            return NetworkManager.Singleton.IsServer;
        }

        private void OnDrawGizmosSelected()
        {
            if (_boxCollider == null) _boxCollider = GetComponent<BoxCollider>();
            if (_boxCollider == null) return;

            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(_boxCollider.center, _boxCollider.size);
            Gizmos.matrix = oldMatrix;
        }
    }
}
