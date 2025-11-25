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
                
                // spawn damage number if damage taken
                if (newVal < oldVal)
                {
                    // pass the boss position directly, let the damage number handle offsets
                    ShowDamageNumber(oldVal - newVal, boss.transform.position);
                }
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
