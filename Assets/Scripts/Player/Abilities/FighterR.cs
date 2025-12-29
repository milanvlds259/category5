using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Audio;
using Category5.Player;

namespace Category5
{
    // fighter r ability (ultimate) - summons taunt aura that forces enemies to target the player
    // also applies temporary stat boost
    public class FighterR : AbilityBase
    {
        [SerializeField] private GameObject tauntAuraPrefab;
        [SerializeField] private float auraDuration = 4f;
        [SerializeField] private float damageBoost = 0.4f; // 40% damage boost
        [SerializeField] private float speedBoost = 0.25f; // 25% movement speed boost
        
        private TauntAura currentAura;
        private float auraTimer;
        private bool isActive;
        
        // events for vfx/sfx
        public static event System.Action<Vector3> OnAuraActivate;
        public static event System.Action<Vector3> OnAuraDeactivate;
        
        public override bool CanUse()
        {
            if (!base.CanUse()) return false;
            if (isActive) return false; // can't use while active
            
            // check ability manager cooldown
            if (abilityManager.ability3Cooldown.Value > 0) return false;
            
            return true;
        }

        public override void Execute()
        {
            if (!CanUse()) return;
            
            // activate aura on owner
            ActivateAuraServerRpc();
        }

        [Rpc(SendTo.Server)]
        private void ActivateAuraServerRpc()
        {
            if (!IsServer) return;
            
            isActive = true;
            auraTimer = auraDuration;
            
            // notify all clients to play effects
            TriggerAuraEffectsClientRpc(playerController.transform.position, true);
            
            // set cooldown
            abilityManager.ability3Cooldown.Value = abilityData.cooldownDuration;
        }
        
        [ClientRpc]
        private void TriggerAuraEffectsClientRpc(Vector3 position, bool activate)
        {
            if (activate)
            {
                OnAuraActivate?.Invoke(position);
                
                if (IsOwner)
                {
                    // create taunt aura if on owner
                    if (tauntAuraPrefab != null)
                    {
                        // spawn at player feet position (not parented)
                        // player position is at center, so we need to offset down to ground
                        Vector3 spawnPos = playerController.transform.position;
                        
                        // raycast down to find ground, or use a fixed offset
                        if (Physics.Raycast(playerController.transform.position, Vector3.down, out RaycastHit hit, 10f))
                        {
                            spawnPos.y = hit.point.y + 0.05f; // slightly above ground
                        }
                        else
                        {
                            spawnPos.y = playerController.transform.position.y - 1f; // fallback: 1m below player center
                        }
                        
                        var auraObj = Instantiate(tauntAuraPrefab, spawnPos, Quaternion.identity);
                        currentAura = auraObj.GetComponent<TauntAura>();
                        if (currentAura != null)
                        {
                            currentAura.Initialize(playerController);
                        }
                    }
                    
                    // apply temporary stat boost
                    if (playerStats != null)
                    {
                        // create a temporary power-up effect
                        ApplyStatBoost();
                    }
                    
                    if (HitFeedbackManager.Instance != null)
                    {
                        HitFeedbackManager.Instance.TriggerHeavyHit(playerController.transform.position);
                    }
                }
            }
            else
            {
                OnAuraDeactivate?.Invoke(position);
                
                if (IsOwner && currentAura != null)
                {
                    Destroy(currentAura.gameObject);
                    currentAura = null;
                }
            }
        }
        
        private void ApplyStatBoost()
        {
            // apply temporary stat boosts via PlayerStats
            if (playerStats != null)
            {
                playerStats.ApplyTemporaryMultiplier("damage", damageBoost, auraDuration);
                playerStats.ApplyTemporaryMultiplier("speed", speedBoost, auraDuration);
                
                Debug.Log($"FighterR: Applied damage boost {damageBoost:P0} and speed boost {speedBoost:P0} for {auraDuration}s");
            }
        }

        private void Update()
        {
            // note: only owner updates the timer
            if (!IsOwner || !isActive) return;
            
            auraTimer -= Time.deltaTime;
            if (auraTimer <= 0)
            {
                isActive = false;
                
                // notify all clients to clean up effects
                DeactivateAuraServerRpc();
            }
        }
        
        [Rpc(SendTo.Server)]
        private void DeactivateAuraServerRpc()
        {
            if (!IsServer) return;
            
            TriggerAuraEffectsClientRpc(playerController.transform.position, false);
        }
    }
}
