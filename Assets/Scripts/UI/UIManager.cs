using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Category5.Player;
using Category5.Boss;
using Category5.PowerUps;
using System.Collections.Generic;

namespace Category5.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("hud references")]
        [SerializeField] private HealthBar playerHealthBar;
        [SerializeField] private ManaBar playerManaBar;
        [SerializeField] private HealthBar bossHealthBar;
        [SerializeField] private GameObject bossHealthContainer; // to hide it when no boss
        [SerializeField] private TextMeshProUGUI roundText;

        [Header("damage numbers")]
        [SerializeField] private DamageNumber damageNumberPrefab;
        [SerializeField] private Transform damageNumberContainer;
        
        // track current boss for re-registration
        private BossBase _currentBoss;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (bossHealthContainer != null) bossHealthContainer.SetActive(false);
            
            // find and register any bosses that spawned before UIManager was ready
            FindAndRegisterBoss();
            
            // subscribe to round changes
            if (Category5.Core.GameFlowManager.Instance != null)
            {
                Category5.Core.GameFlowManager.Instance.OnRoundChanged += UpdateRoundDisplay;
                UpdateRoundDisplay(Category5.Core.GameFlowManager.Instance.CurrentRound.Value);
            }
        }
        
        private void OnDestroy()
        {
            if (Category5.Core.GameFlowManager.Instance != null)
            {
                Category5.Core.GameFlowManager.Instance.OnRoundChanged -= UpdateRoundDisplay;
            }
        }
        
        private void UpdateRoundDisplay(int round)
        {
            if (roundText != null)
            {
                roundText.text = $"Category {round}";
            }
        }
        
        private void FindAndRegisterBoss()
        {
            // look for any boss in the scene that needs to be registered
            BossBase boss = FindFirstObjectByType<BossBase>();
            if (boss != null && boss.IsSpawned)
            {
                Debug.Log("UIManager: Found existing boss, registering");
                RegisterBoss(boss);
            }
        }

        public void RegisterPlayer(PlayerController player)
        {
            // only register the local player for the main hud
            if (player.IsOwner)
            {
                // initialize health bar
                playerHealthBar.Initialize(player.MaxHealth, player.CurrentHealth.Value);
                
                // subscribe to health changes
                player.CurrentHealth.OnValueChanged += (oldVal, newVal) => 
                {
                    playerHealthBar.UpdateHealth(newVal);
                };
                
                // subscribe to max health changes (when items increase max hp)
                player.OnMaxHealthChanged += (newMaxHealth) =>
                {
                    playerHealthBar.Initialize(newMaxHealth, player.CurrentHealth.Value);
                };
                
                // initialize mana bar
                if (playerManaBar != null)
                {
                    playerManaBar.Initialize(player.MaxMana, player.CurrentMana.Value);
                    
                    // subscribe to mana changes
                    player.CurrentMana.OnValueChanged += (oldVal, newVal) =>
                    {
                        playerManaBar.UpdateMana(newVal, player.MaxMana);
                    };
                    
                    // subscribe to mana changed event (for max mana updates)
                    player.OnManaChanged += (current, max) =>
                    {
                        playerManaBar.UpdateMana(current, max);
                    };
                }
            }
        }

        public void RegisterBoss(BossBase boss)
        {
            _currentBoss = boss;
            
            if (bossHealthContainer != null) bossHealthContainer.SetActive(true);
            
            bossHealthBar.Initialize(boss.MaxHealth, boss.CurrentHealth.Value);

            // unsubscribe from previous and subscribe to new
            boss.CurrentHealth.OnValueChanged += OnBossHealthChanged;
        }
        
        private void OnBossHealthChanged(int oldVal, int newVal)
        {
            if (_currentBoss == null) return;
            
            // check if boss was reset (health went up significantly)
            if (newVal > oldVal && newVal == _currentBoss.MaxHealth)
            {
                // boss was reset, reinitialize health bar
                bossHealthBar.Initialize(_currentBoss.MaxHealth, newVal);
            }
            else
            {
                bossHealthBar.UpdateHealth(newVal);
            }
        }

        public void ShowDamageNumber(int damage, Vector3 position)
        {
            if (damageNumberPrefab == null) return;

            // instantiate inside the canvas container if available
            Transform parent = damageNumberContainer != null ? damageNumberContainer : transform;
            DamageNumber dn = Instantiate(damageNumberPrefab, parent);
            dn.Initialize(damage, position);
        }
        
        // get player name by client id (used by disconnect notifications, etc)
        public string GetPlayerName(ulong clientId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                return $"Player {clientId}";
            }
            
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                var player = client.PlayerObject?.GetComponent<PlayerController>();
                if (player != null)
                {
                    string name = player.GetPlayerName();
                    if (!string.IsNullOrWhiteSpace(name) && name != "Player")
                    {
                        return name;
                    }
                }
            }
            
            return $"Player {clientId}";
        }
        
        // get all player controllers in the game
        public List<PlayerController> GetAllPlayers()
        {
            var players = new List<PlayerController>();
            
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                return players;
            }
            
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                var player = client.PlayerObject?.GetComponent<PlayerController>();
                if (player != null)
                {
                    players.Add(player);
                }
            }
            
            return players;
        }
    }
}
