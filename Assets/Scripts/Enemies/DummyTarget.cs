using UnityEngine;
using Unity.Netcode;
using Category5.Core;

namespace Category5.Enemies
{
    public class DummyTarget : NetworkBehaviour, IDamageable
    {
        [SerializeField] private NetworkVariable<int> Health = new NetworkVariable<int>(100);
        [SerializeField] private Renderer meshRenderer;
        
        private Color _originalColor;

        private void Awake()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<Renderer>();
        }

        public override void OnNetworkSpawn()
        {
            if (meshRenderer != null)
            {
                _originalColor = meshRenderer.material.color;
            }
            
            // subscribe to health changes to update UI or visuals
            Health.OnValueChanged += OnHealthChanged;
        }

        public override void OnNetworkDespawn()
        {
            Health.OnValueChanged -= OnHealthChanged;
        }

        public void TakeDamage(int damage)
        {
            // only the server can modify NetworkVariables
            if (!IsServer) return;

            Health.Value -= damage;
            // Debug.Log($"Dummy took {damage} damage. Health: {Health.Value}");

            // visual feedback (server side trigger, but we should use ClientRpc for proper visuals)
            PlayHitEffectClientRpc();

            if (Health.Value <= 0)
            {
                Die();
            }
        }

        [ClientRpc]
        private void PlayHitEffectClientRpc()
        {
            // flash red
            StartCoroutine(FlashRed());
        }

        private System.Collections.IEnumerator FlashRed()
        {
            if (meshRenderer != null)
            {
                meshRenderer.material.color = Color.red;
                yield return new WaitForSeconds(0.1f);
                meshRenderer.material.color = _originalColor;
            }
        }

        private void OnHealthChanged(int oldHealth, int newHealth)
        {
            // update health bar here when we have one
        }

        private void Die()
        {
            // despawn the object across the network
            GetComponent<NetworkObject>().Despawn();
        }
    }
}
