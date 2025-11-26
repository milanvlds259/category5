using UnityEngine;
using Unity.Netcode;
using Category5.Player;
using Category5.Boss;

namespace Category5.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("hud references")]
        [SerializeField] private HealthBar playerHealthBar;
        [SerializeField] private HealthBar bossHealthBar;
        [SerializeField] private GameObject bossHealthContainer; // to hide it when no boss

        [Header("damage numbers")]
        [SerializeField] private DamageNumber damageNumberPrefab;
        [SerializeField] private Transform damageNumberContainer;

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
                playerHealthBar.Initialize(100, player.CurrentHealth.Value);
                
                // subscribe to health changes
                player.CurrentHealth.OnValueChanged += (oldVal, newVal) => 
                {
                    playerHealthBar.UpdateHealth(newVal);
                };
            }
        }

        public void RegisterBoss(BossBase boss)
        {
            if (bossHealthContainer != null) bossHealthContainer.SetActive(true);
            
            bossHealthBar.Initialize(boss.MaxHealth, boss.CurrentHealth.Value);

            boss.CurrentHealth.OnValueChanged += (oldVal, newVal) =>
            {
                bossHealthBar.UpdateHealth(newVal);
                // damage numbers are now spawned by the attacking player, not here
            };
        }

        public void ShowDamageNumber(int damage, Vector3 position)
        {
            if (damageNumberPrefab == null) return;

            // instantiate inside the canvas container if available
            Transform parent = damageNumberContainer != null ? damageNumberContainer : transform;
            DamageNumber dn = Instantiate(damageNumberPrefab, parent);
            dn.Initialize(damage, position);
        }
    }
}
