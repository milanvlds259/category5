using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Category5;
using Category5.Player;
using Category5.SkillTree;

namespace Category5.UI
{
    // displays cooldown indicators for the 3 player abilities (Q/E/R)
    // positioned bottom-right per summer's ui sketch
    public class AbilityCooldownUI : MonoBehaviour
    {
        [Header("Ability Slot References")]
        [SerializeField] private AbilitySlotUI ability1Slot; // Q
        [SerializeField] private AbilitySlotUI ability2Slot; // E
        [SerializeField] private AbilitySlotUI ability3Slot; // R
        
        [Header("Buff Indicator")]
        [SerializeField] private GameObject buffIndicator; // shows when ranger q is active

        [Header("Enchanter UI")]
        [SerializeField] private TextMeshProUGUI enchanterChargeText;
        
        private PlayerAbilityManager abilityManager;
        private UltimateLockManager _ultimateLockManager;
        private int _retryCount = 0;
        private int _maxRetries = 5;
        private bool _isEnchanter;
        private bool _isAssassin;
        private AssassinQ _assassinQ;
        
        private static readonly Color LockedColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);
        
        private void Start()
        {
            // hide buff indicator initially
            if (buffIndicator != null)
            {
                buffIndicator.SetActive(false);
            }

            if (enchanterChargeText != null)
            {
                enchanterChargeText.gameObject.SetActive(false);
            }
            
            // find the local player's ability manager
            FindLocalPlayerAbilityManager();
        }
        
        private void OnEnable()
        {
            PlayerAbilityManager.OnCooldownChanged += HandleCooldownChanged;
            PlayerAbilityManager.OnAbilitiesLoaded += HandleAbilitiesLoaded;
            ElementalistQ.OnElementChanged += HandleElementChanged;
            PlayerAbilityManager.OnEnchanterChargesChanged += HandleEnchanterChargesChanged;
            AssassinQ.OnBuffStateChanged += HandleAssassinBuffStateChanged;
            AssassinQ.OnChargesChanged += HandleAssassinChargesChanged;
        }

        private void OnDisable()
        {
            PlayerAbilityManager.OnCooldownChanged -= HandleCooldownChanged;
            PlayerAbilityManager.OnAbilitiesLoaded -= HandleAbilitiesLoaded;
            ElementalistQ.OnElementChanged -= HandleElementChanged;
            PlayerAbilityManager.OnEnchanterChargesChanged -= HandleEnchanterChargesChanged;
            AssassinQ.OnBuffStateChanged -= HandleAssassinBuffStateChanged;
            AssassinQ.OnChargesChanged -= HandleAssassinChargesChanged;

            if (_ultimateLockManager != null)
            {
                _ultimateLockManager.OnUltimateLockStateChanged -= HandleUltimateLockStateChanged;
            }
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
            // Debug.Log($"[AbilityCooldownUI] FindLocalPlayerAbilityManagerDelayed attempt {_retryCount + 1}: Found {players.Length} players");
            
            foreach (var player in players)
            {
                // Debug.Log($"[AbilityCooldownUI] Checking player {player.gameObject.name}: IsOwner={player.IsOwner}, HasAbilityManager={player.GetComponent<PlayerAbilityManager>() != null}");
                
                if (player.IsOwner)
                {
                    abilityManager = player.GetComponent<PlayerAbilityManager>();
                    if (abilityManager != null)
                    {
                        // Debug.Log($"[AbilityCooldownUI] Found local player ability manager on attempt {_retryCount + 1}");

                        // Subscribe to ultimate lock state
                        _ultimateLockManager = player.GetComponent<UltimateLockManager>();
                        if (_ultimateLockManager != null)
                        {
                            _ultimateLockManager.OnUltimateLockStateChanged -= HandleUltimateLockStateChanged;
                            _ultimateLockManager.OnUltimateLockStateChanged += HandleUltimateLockStateChanged;
                        }

                        InitializeSlots();

                        // also subscribe to class changes so we re-initialize if the class changes later
                        var classManager = player.GetComponent<PlayerClassManager>();
                        if (classManager != null)
                        {
                            classManager.SelectedClassId.OnValueChanged -= OnClassChanged;
                            classManager.SelectedClassId.OnValueChanged += OnClassChanged;
                        }

                        _retryCount = 0;
                        return;
                    }
                }
            }
            
            // retry if not found (helps with host initialization timing)
            _retryCount++;
            if (_retryCount < _maxRetries)
            {
                // Debug.Log($"[AbilityCooldownUI] Ability manager not found, retrying... ({_retryCount}/{_maxRetries})");
                Invoke(nameof(FindLocalPlayerAbilityManagerDelayed), 0.3f);
            }
            else
            {
                Debug.LogWarning("[AbilityCooldownUI] Could not find local player ability manager after max retries!");
                _retryCount = 0;
            }
        }

        private void OnClassChanged(int oldClass, int newClass)
        {
            // abilities will be re-initialized via OnAbilitiesLoaded event — no fixed delay needed
        }

        private void HandleAbilitiesLoaded(PlayerAbilityManager source)
        {
            // only handle events from our local player's ability manager
            if (source != abilityManager) 
            {
                return;
            }
            InitializeSlots();
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

                _isEnchanter = ability1 is EnchanterQ;
                _assassinQ = ability1 as AssassinQ;
                _isAssassin = _assassinQ != null;
                UpdateAbility1ChargeVisibility();
                ShowBuffIndicator(_assassinQ != null && _assassinQ.HasDamageBuff);

                if (_isEnchanter)
                {
                    UpdateEnchanterCharges(abilityManager.GetEnchanterCharges(), abilityManager.GetMaxEnchanterCharges());
                }

                if (_isAssassin)
                {
                    UpdateAbility1Charges(_assassinQ.CurrentCharges, _assassinQ.MaxCharges);
                }

                if (ability1 is ElementalistQ elementalistQ)
                {
                    ability1Slot.UpdateIconSprite(elementalistQ.CurrentIcon);
                }
            }
            
            if (ability2 != null && ability2Slot != null)
            {
                if (ability2 is ElementalistE_Dispatcher dispatcher)
                {
                    var activeData = dispatcher.ActiveAbilityData;
                    ability2Slot.Initialize(activeData, "E");
                }
                else
                {
                    ability2Slot.Initialize(ability2.Data, "E");
                }
            }
            
            if (ability3 != null && ability3Slot != null)
            {
                ability3Slot.Initialize(ability3.Data, "R");

                // Apply initial ultimate lock state
                if (_ultimateLockManager != null)
                {
                    ability3Slot.SetLocked(!_ultimateLockManager.IsUnlocked);
                }
            }
        }
        
        private void HandleCooldownChanged(PlayerAbilityManager source, AbilitySlot slot, float current, float max)
        {
            // Debug.Log($"[AbilityCooldownUI] HandleCooldownChanged: source={source.gameObject.name}, slot={slot}, current={current}, max={max}");
            
            // only update if this is for OUR local player's ability manager
            if (abilityManager == null)
            {
                Debug.LogWarning("[AbilityCooldownUI] abilityManager is null!");
                return;
            }
            
            // filter: only handle events from our ability manager
            if (source != abilityManager)
            {
                // Debug.Log($"[AbilityCooldownUI] Ignoring cooldown change from different player");
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
                // Debug.Log($"[AbilityCooldownUI] Found target slot for {slot}");
                targetSlot.UpdateCooldown(current, max);
            }
            else
            {
                Debug.LogWarning($"[AbilityCooldownUI] Target slot is null for {slot}");
            }
        }

        private void HandleElementChanged(ElementMode mode)
        {
            if (abilityManager == null || ability2Slot == null) return;

            if (ability1Slot != null)
            {
                var elementalistQ = abilityManager.GetAbility1() as ElementalistQ;
                if (elementalistQ != null)
                {
                    ability1Slot.UpdateIconSprite(elementalistQ.GetIconForElement(mode));
                }
            }

            var dispatcher = abilityManager.GetAbility2() as ElementalistE_Dispatcher;
            if (dispatcher == null) return;

            AbilityData data = dispatcher.ActiveAbilityData;
            ability2Slot.UpdateIcon(data);
        }

        private void HandleEnchanterChargesChanged(PlayerAbilityManager source, int current, int max)
        {
            if (abilityManager == null) return;
            if (source != abilityManager) return;
            if (!_isEnchanter) return;

            UpdateAbility1Charges(current, max);
        }

        private void UpdateEnchanterCharges(int current, int max)
        {
            UpdateAbility1Charges(current, max);
        }

        private void UpdateAbility1Charges(int current, int max)
        {
            if (enchanterChargeText == null) return;
            enchanterChargeText.text = $"{current}/{max}";
        }

        private void UpdateAbility1ChargeVisibility()
        {
            if (enchanterChargeText == null) return;
            enchanterChargeText.gameObject.SetActive(_isEnchanter || _isAssassin);
        }

        private void HandleAssassinBuffStateChanged(AssassinQ source, bool isActive)
        {
            if (_assassinQ == null) return;
            if (source != _assassinQ) return;

            ShowBuffIndicator(isActive);
        }

        private void HandleAssassinChargesChanged(AssassinQ source, int current, int max)
        {
            if (_assassinQ == null) return;
            if (source != _assassinQ) return;
            if (!_isAssassin) return;

            UpdateAbility1Charges(current, max);
        }

        private void HandleUltimateLockStateChanged(bool isUnlocked)
        {
            if (ability3Slot == null) return;

            if (isUnlocked)
            {
                ability3Slot.SetLocked(false);
            }
            else
            {
                ability3Slot.SetLocked(true);
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
            // Debug.Log($"[AbilitySlotUI] UpdateCooldown called: current={currentCooldown}, max={maxCooldown}, cooldownText={cooldownText}, fillImage={fillImage}");
            
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
                    // Debug.Log($"[AbilitySlotUI] Showing cooldown text, setting to {currentCooldown.ToString("F1")}");
                    cooldownText.gameObject.SetActive(true);
                    cooldownText.text = Mathf.Max(0f, currentCooldown).ToString("F1");
                }
                else
                {
                    // Debug.Log($"[AbilitySlotUI] Hiding cooldown text (cooldown ready)");
                    cooldownText.gameObject.SetActive(false);
                }
            }
            else
            {
                Debug.LogWarning("[AbilitySlotUI] cooldownText is null!");
            }
            
            // dim icon while on cooldown
            if (iconImage != null && !_isLocked)
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

        public void UpdateIcon(AbilityData data)
        {
            abilityData = data;

            if (iconImage != null)
            {
                if (data != null && data.abilityIcon != null)
                {
                    iconImage.sprite = data.abilityIcon;
                    iconImage.color = remainingCooldown <= 0f ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.color = placeholderColor;
                }
            }
        }

        public void UpdateIconSprite(Sprite sprite)
        {
            if (iconImage != null)
            {
                if (sprite != null)
                {
                    iconImage.sprite = sprite;
                    iconImage.color = remainingCooldown <= 0f ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.color = placeholderColor;
                }
            }
        }

        private bool _isLocked = false;
        private static readonly Color LockedSlotColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);

        public void SetLocked(bool locked)
        {
            _isLocked = locked;

            if (iconImage != null)
            {
                if (locked)
                {
                    iconImage.color = LockedSlotColor;
                }
                else
                {
                    iconImage.color = remainingCooldown <= 0f ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
                }
            }

            if (readyGlow != null)
            {
                readyGlow.SetActive(!locked && remainingCooldown <= 0f);
            }
        }
    }
}
