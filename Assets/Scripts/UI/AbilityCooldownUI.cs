using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Category5.Player;

namespace Category5.UI
{
    // displays cooldown indicators for the 3 player abilities (Q/E/R)
    // positioned bottom-right per ui sketch
    public class AbilityCooldownUI : MonoBehaviour
    {
        [Header("Ability Slot References")]
        [SerializeField] private AbilitySlotUI ability1Slot; // Q
        [SerializeField] private AbilitySlotUI ability2Slot; // E
        [SerializeField] private AbilitySlotUI ability3Slot; // R
        
        [Header("Buff Indicator")]
        [SerializeField] private GameObject buffIndicator; // shows when quickbow is active
        
        private PlayerAbilityManager abilityManager;
        private int _retryCount = 0;
        private int _maxRetries = 5;
        
        private void Start()
        {
            // hide buff indicator initially
            if (buffIndicator != null)
            {
                buffIndicator.SetActive(false);
            }
            
            // find the local player's ability manager
            FindLocalPlayerAbilityManager();
        }
        
        private void OnEnable()
        {
            PlayerAbilityManager.OnCooldownChanged += HandleCooldownChanged;
        }
        
        private void OnDisable()
        {
            PlayerAbilityManager.OnCooldownChanged -= HandleCooldownChanged;
        }
        
        private void Update()
        {
            // update cooldown text countdown each frame
            if (ability1Slot != null) ability1Slot.UpdateCooldownText(Time.deltaTime);
            if (ability2Slot != null) ability2Slot.UpdateCooldownText(Time.deltaTime);
            if (ability3Slot != null) ability3Slot.UpdateCooldownText(Time.deltaTime);
        }
        
        private void FindLocalPlayerAbilityManager()
        {
            // wait a frame for players to spawn with retry logic for host initialization timing issues
            Invoke(nameof(FindLocalPlayerAbilityManagerDelayed), 0.5f);
        }
        
        private void FindLocalPlayerAbilityManagerDelayed()
        {
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            Debug.Log($"[AbilityCooldownUI] FindLocalPlayerAbilityManagerDelayed attempt {_retryCount + 1}: Found {players.Length} players");
            
            foreach (var player in players)
            {
                Debug.Log($"[AbilityCooldownUI] Checking player {player.gameObject.name}: IsOwner={player.IsOwner}, HasAbilityManager={player.GetComponent<PlayerAbilityManager>() != null}");
                
                if (player.IsOwner)
                {
                    abilityManager = player.GetComponent<PlayerAbilityManager>();
                    if (abilityManager != null)
                    {
                        Debug.Log($"[AbilityCooldownUI] Found local player ability manager on attempt {_retryCount + 1}");
                        InitializeSlots();
                        _retryCount = 0;
                        return;
                    }
                }
            }
            
            // retry if not found (helps with host initialization timing)
            _retryCount++;
            if (_retryCount < _maxRetries)
            {
                Debug.Log($"[AbilityCooldownUI] Ability manager not found, retrying... ({_retryCount}/{_maxRetries})");
                Invoke(nameof(FindLocalPlayerAbilityManagerDelayed), 0.3f);
            }
            else
            {
                Debug.LogWarning("[AbilityCooldownUI] Could not find local player ability manager after max retries!");
                _retryCount = 0;
            }
        }
        
        private void InitializeSlots()
        {
            if (abilityManager == null) return;
            
            // set up each slot with ability data
            var ability1 = abilityManager.GetAbility1();
            var ability2 = abilityManager.GetAbility2();
            var ability3 = abilityManager.GetAbility3();
            
            if (ability1 != null && ability1Slot != null)
            {
                ability1Slot.Initialize(ability1.Data, "Q");
            }
            
            if (ability2 != null && ability2Slot != null)
            {
                ability2Slot.Initialize(ability2.Data, "E");
            }
            
            if (ability3 != null && ability3Slot != null)
            {
                ability3Slot.Initialize(ability3.Data, "R");
            }
        }
        
        private void HandleCooldownChanged(PlayerAbilityManager source, AbilitySlot slot, float current, float max)
        {
            Debug.Log($"[AbilityCooldownUI] HandleCooldownChanged: source={source.gameObject.name}, slot={slot}, current={current}, max={max}");
            
            // only update if this is for OUR local player's ability manager
            if (abilityManager == null)
            {
                Debug.LogWarning("[AbilityCooldownUI] abilityManager is null!");
                return;
            }
            
            // filter: only handle events from our ability manager
            if (source != abilityManager)
            {
                Debug.Log($"[AbilityCooldownUI] Ignoring cooldown change from different player");
                return;
            }
            
            AbilitySlotUI targetSlot = slot switch
            {
                AbilitySlot.Ability1 => ability1Slot,
                AbilitySlot.Ability2 => ability2Slot,
                AbilitySlot.Ability3 => ability3Slot,
                _ => null
            };
            
            if (targetSlot != null)
            {
                Debug.Log($"[AbilityCooldownUI] Found target slot for {slot}");
                targetSlot.UpdateCooldown(current, max);
            }
            else
            {
                Debug.LogWarning($"[AbilityCooldownUI] Target slot is null for {slot}");
            }
        }
        
        // public method for abilities to show/hide buff indicator
        public void ShowBuffIndicator(bool show)
        {
            if (buffIndicator != null)
            {
                buffIndicator.SetActive(show);
            }
        }
    }
    
    // individual ability slot component
    [System.Serializable]
    public class AbilitySlotUI
    {
        [Header("UI References")]
        public Image iconImage;
        public Image fillImage; // radial fill for cooldown
        public TextMeshProUGUI keybindText;
        public TextMeshProUGUI cooldownText; // cooldown countdown text
        public GameObject readyGlow; // shows when ability is ready
        
        [Header("Placeholder Settings")]
        public Color placeholderColor = Color.cyan;
        
        private AbilityData abilityData;
        private string keybind;
        private float remainingCooldown;
        private float maxCooldown;
        
        public void Initialize(AbilityData data, string key)
        {
            abilityData = data;
            keybind = key;
            remainingCooldown = 0f;
            maxCooldown = 0f;
            
            // set keybind text
            if (keybindText != null)
            {
                keybindText.text = keybind;
            }
            
            // hide cooldown text initially
            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(false);
            }
            
            // set icon (or placeholder)
            if (iconImage != null)
            {
                if (data.abilityIcon != null)
                {
                    iconImage.sprite = data.abilityIcon;
                    iconImage.color = Color.white;
                }
                else
                {
                    // use placeholder colored square with keybind letter
                    iconImage.color = placeholderColor;
                }
            }
            
            // set fill to ready initially
            if (fillImage != null)
            {
                fillImage.fillAmount = 0f;
            }
            
            // show ready glow
            if (readyGlow != null)
            {
                readyGlow.SetActive(true);
            }
        }
        
        public void UpdateCooldown(float currentCooldown, float maxCooldown)
        {
            Debug.Log($"[AbilitySlotUI] UpdateCooldown called: current={currentCooldown}, max={maxCooldown}, cooldownText={cooldownText}, fillImage={fillImage}");
            
            remainingCooldown = currentCooldown;
            this.maxCooldown = maxCooldown;
            
            if (fillImage != null)
            {
                // calculate fill amount (1 = on cooldown, 0 = ready)
                float fillAmount = maxCooldown > 0f ? (currentCooldown / maxCooldown) : 0f;
                fillImage.fillAmount = fillAmount;
            }
            else
            {
                Debug.LogWarning("[AbilitySlotUI] fillImage is null!");
            }
            
            // show/hide glow based on ready state
            bool isReady = currentCooldown <= 0f;
            if (readyGlow != null)
            {
                readyGlow.SetActive(isReady);
            }
            
            // show/hide cooldown text and update it
            if (cooldownText != null)
            {
                if (currentCooldown > 0f)
                {
                    Debug.Log($"[AbilitySlotUI] Showing cooldown text, setting to {currentCooldown.ToString("F1")}");
                    cooldownText.gameObject.SetActive(true);
                    cooldownText.text = Mathf.Max(0f, currentCooldown).ToString("F1");
                }
                else
                {
                    Debug.Log($"[AbilitySlotUI] Hiding cooldown text (cooldown ready)");
                    cooldownText.gameObject.SetActive(false);
                }
            }
            else
            {
                Debug.LogWarning("[AbilitySlotUI] cooldownText is null!");
            }
            
            // dim icon while on cooldown
            if (iconImage != null)
            {
                iconImage.color = isReady ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            }
        }
        
        public void UpdateCooldownText(float deltaTime)
        {
            // only update if cooldown text is assigned
            if (cooldownText == null) return;
            
            // decrease remaining cooldown each frame
            if (remainingCooldown > 0f)
            {
                remainingCooldown -= deltaTime;
                
                // update text display
                if (cooldownText.gameObject.activeSelf)
                {
                    cooldownText.text = Mathf.Max(0f, remainingCooldown).ToString("F1");
                }
                
                // hide when cooldown reaches 0
                if (remainingCooldown <= 0f)
                {
                    cooldownText.gameObject.SetActive(false);
                }
            }
            else if (remainingCooldown <= 0f && cooldownText.gameObject.activeSelf)
            {
                // ensure text is hidden when not on cooldown
                cooldownText.gameObject.SetActive(false);
            }
        }
    }
}
