using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Category5.PowerUps
{
    // manages the power-up selection ui screen
    public class PowerUpSelectionUI : MonoBehaviour
    {
        [Header("ui references")]
        [SerializeField] private GameObject selectionPanel;
        [SerializeField] private PowerUpCard[] powerUpCards = new PowerUpCard[3];
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject waitingOverlay;

        [Header("round display")]
        [SerializeField] private TextMeshProUGUI roundText;

        private bool _hasSelected = false;
        private int[] _currentChoices;
        private bool _isSubscribed = false;

        private void Start()
        {
            TrySubscribeToEvents();

            // hide panel initially
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(false);
            }
        }
        
        private void Update()
        {
            // keep trying to subscribe if we haven't yet
            // handles case where PowerUpManager spawns after this UI
            if (!_isSubscribed)
            {
                TrySubscribeToEvents();
            }
        }
        
        private void TrySubscribeToEvents()
        {
            if (_isSubscribed) return;
            
            if (PowerUpManager.Instance != null)
            {
                PowerUpManager.Instance.OnShowPowerUpSelection += ShowSelection;
                PowerUpManager.Instance.OnHidePowerUpSelection += HideSelection;
                PowerUpManager.Instance.OnVictory += ShowVictory;
                PowerUpManager.Instance.OnRoundChanged += UpdateRoundDisplay;
                _isSubscribed = true;
                Debug.Log("PowerUpSelectionUI: Subscribed to PowerUpManager events");
            }
        }

        private void OnDestroy()
        {
            if (PowerUpManager.Instance != null)
            {
                PowerUpManager.Instance.OnShowPowerUpSelection -= ShowSelection;
                PowerUpManager.Instance.OnHidePowerUpSelection -= HideSelection;
                PowerUpManager.Instance.OnVictory -= ShowVictory;
                PowerUpManager.Instance.OnRoundChanged -= UpdateRoundDisplay;
            }
        }

        private void ShowSelection(int[] powerUpIndices)
        {
            if (selectionPanel == null) return;

            Debug.Log($"PowerUpSelectionUI: Showing selection with {powerUpIndices.Length} choices");
            
            _currentChoices = powerUpIndices;
            _hasSelected = false;

            // show panel
            selectionPanel.SetActive(true);
            
            // unlock cursor for selection
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // hide waiting overlay
            if (waitingOverlay != null)
            {
                waitingOverlay.SetActive(false);
            }

            // update status text
            if (statusText != null)
            {
                statusText.text = "Choose a Power-Up!";
            }

            // setup cards
            var registry = PowerUpRegistry.Instance;
            if (registry == null)
            {
                Debug.LogError("PowerUpSelectionUI: PowerUpRegistry not found");
                return;
            }

            for (int i = 0; i < powerUpCards.Length; i++)
            {
                if (powerUpCards[i] == null) continue;

                if (i < powerUpIndices.Length)
                {
                    var powerUp = registry.GetPowerUpByIndex(powerUpIndices[i]);
                    powerUpCards[i].Initialize(powerUp, powerUpIndices[i], this);
                    powerUpCards[i].SetInteractable(true);
                }
                else
                {
                    powerUpCards[i].gameObject.SetActive(false);
                }
            }
        }

        private void HideSelection()
        {
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(false);
            }

            // re-lock cursor for gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _hasSelected = false;
        }

        public void OnCardSelected(int powerUpIndex)
        {
            if (_hasSelected) return;

            _hasSelected = true;
            Debug.Log($"PowerUpSelectionUI: Selected power-up index {powerUpIndex}");

            // disable all cards
            foreach (var card in powerUpCards)
            {
                if (card != null)
                {
                    card.SetInteractable(false);
                }
            }

            // show waiting overlay
            if (waitingOverlay != null)
            {
                waitingOverlay.SetActive(true);
            }

            // update status text
            if (statusText != null)
            {
                statusText.text = "Waiting for other players...";
            }

            // send selection to manager
            PowerUpManager.Instance?.SelectPowerUp(powerUpIndex);
        }

        private void ShowVictory()
        {
            if (selectionPanel == null) return;

            selectionPanel.SetActive(true);
            
            // unlock cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // hide cards
            foreach (var card in powerUpCards)
            {
                if (card != null)
                {
                    card.gameObject.SetActive(false);
                }
            }

            // hide waiting overlay
            if (waitingOverlay != null)
            {
                waitingOverlay.SetActive(false);
            }

            // show victory message
            if (statusText != null)
            {
                statusText.text = "VICTORY!\nYou defeated all the bosses!";
            }
        }

        private void UpdateRoundDisplay(int round)
        {
            if (roundText != null)
            {
                roundText.text = $"Round {round}";
            }
        }
    }
}
