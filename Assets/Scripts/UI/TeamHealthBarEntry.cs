using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Category5.Player;

namespace Category5.UI
{
    /// <summary>
    /// displays a single team member's health bar, name, and placeholder icon in the team health ui
    /// subscribes to player health changes and updates the visual representation
    /// handles death state by graying out the entry
    /// </summary>
    public class TeamHealthBarEntry : MonoBehaviour
    {
        [SerializeField] private HealthBar healthBar;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private Image playerIconImage;
        [SerializeField] private CanvasGroup canvasGroup; // for graying out when dead
        
        private PlayerController _player;
        private ulong _clientId;

        public void Initialize(PlayerController player, ulong clientId)
        {
            _player = player;
            _clientId = clientId;

            // set up initial data
            playerNameText.text = player.GetPlayerName();
            healthBar.Initialize(player.MaxHealth, player.CurrentHealth.Value);
            UpdateHealthText();

            // subscribe to health changes
            player.CurrentHealth.OnValueChanged += OnHealthChanged;
            
            // subscribe to death state changes
            player.IsDead.OnValueChanged += OnDeathStateChanged;
            
            // subscribe to name changes
            PlayerController.OnPlayerNameChanged += OnPlayerNameChanged;

            // set initial death state
            UpdateDeathState(player.IsDead.Value);
        }

        private void OnHealthChanged(int oldVal, int newVal)
        {
            if (_player == null) return;

            // handle boss reset (health went up significantly)
            if (newVal > oldVal && newVal == _player.MaxHealth)
            {
                healthBar.Initialize(_player.MaxHealth, newVal);
            }
            else
            {
                healthBar.UpdateHealth(newVal);
            }

            UpdateHealthText();
        }

        private void OnDeathStateChanged(bool oldVal, bool newVal)
        {
            UpdateDeathState(newVal);
        }

        private void OnPlayerNameChanged(PlayerController changedPlayer)
        {
            // only update if it's this player
            if (changedPlayer == _player)
            {
                playerNameText.text = _player.GetPlayerName();
            }
        }

        private void UpdateDeathState(bool isDead)
        {
            if (canvasGroup == null) return;

            // gray out the entry when dead
            canvasGroup.alpha = isDead ? 0.5f : 1f;
        }

        private void UpdateHealthText()
        {
            if (_player == null || healthText == null) return;

            healthText.text = $"{_player.CurrentHealth.Value}/{_player.MaxHealth}";
        }

        public ulong GetClientId()
        {
            return _clientId;
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.CurrentHealth.OnValueChanged -= OnHealthChanged;
                _player.IsDead.OnValueChanged -= OnDeathStateChanged;
            }

            PlayerController.OnPlayerNameChanged -= OnPlayerNameChanged;
        }
    }
}
