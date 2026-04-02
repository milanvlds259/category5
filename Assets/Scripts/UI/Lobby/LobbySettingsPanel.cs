using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Category5.UI
{
    // placeholder settings panel for lobby
    // stores values in PlayerPrefs for future AudioManager integration
    public class LobbySettingsPanel : MonoBehaviour
    {
        [Header("voice chat (placeholder)")]
        [SerializeField] private Toggle voiceChatToggle;
        [SerializeField] private TextMeshProUGUI voiceChatLabel;
        
        [Header("music volume")]
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private TextMeshProUGUI musicVolumeLabel;
        
        [Header("sfx volume")]
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TextMeshProUGUI sfxVolumeLabel;
        
        // playerprefs keys
        private const string PREFS_VOICE_CHAT = "Settings_VoiceChat";
        private const string PREFS_MUSIC_VOLUME = "Settings_MusicVolume";
        private const string PREFS_SFX_VOLUME = "Settings_SFXVolume";
        
        private void OnEnable()
        {
            // setup listeners
            if (voiceChatToggle != null)
                voiceChatToggle.onValueChanged.AddListener(OnVoiceChatChanged);
            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }
        
        private void OnDisable()
        {
            // cleanup listeners
            if (voiceChatToggle != null)
                voiceChatToggle.onValueChanged.RemoveListener(OnVoiceChatChanged);
            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        }
        
        // call this when entering the lobby to load saved settings
        public void Initialize()
        {
            LoadSettings();
            UpdateLabels();
        }
        
        private void LoadSettings()
        {
            // load voice chat setting
            if (voiceChatToggle != null)
            {
                bool voiceEnabled = PlayerPrefs.GetInt(PREFS_VOICE_CHAT, 1) == 1;
                voiceChatToggle.SetIsOnWithoutNotify(voiceEnabled);
            }
            
            // load music volume (0-100, default 80)
            if (musicVolumeSlider != null)
            {
                float musicVolume = PlayerPrefs.GetFloat(PREFS_MUSIC_VOLUME, 80f);
                musicVolumeSlider.SetValueWithoutNotify(musicVolume);
            }
            
            // load sfx volume (0-100, default 80)
            if (sfxVolumeSlider != null)
            {
                float sfxVolume = PlayerPrefs.GetFloat(PREFS_SFX_VOLUME, 80f);
                sfxVolumeSlider.SetValueWithoutNotify(sfxVolume);
            }
        }
        
        private void OnVoiceChatChanged(bool enabled)
        {
            PlayerPrefs.SetInt(PREFS_VOICE_CHAT, enabled ? 1 : 0);
            PlayerPrefs.Save();
            
            UpdateVoiceChatLabel();
            
            // placeholder - future integration with voice chat system
            // Debug.Log($"LobbySettingsPanel: Voice chat set to {enabled} (placeholder)");
        }
        
        private void OnMusicVolumeChanged(float value)
        {
            PlayerPrefs.SetFloat(PREFS_MUSIC_VOLUME, value);
            PlayerPrefs.Save();
            
            UpdateMusicVolumeLabel();
            
            // placeholder - future integration with AudioManager
            // AudioManager.Instance?.SetMusicVolume(value / 100f);
            // Debug.Log($"LobbySettingsPanel: Music volume set to {value}% (placeholder)");
        }
        
        private void OnSfxVolumeChanged(float value)
        {
            PlayerPrefs.SetFloat(PREFS_SFX_VOLUME, value);
            PlayerPrefs.Save();
            
            UpdateSfxVolumeLabel();
            
            // placeholder - future integration with AudioManager
            // AudioManager.Instance?.SetSfxVolume(value / 100f);
            // Debug.Log($"LobbySettingsPanel: SFX volume set to {value}% (placeholder)");
        }
        
        private void UpdateLabels()
        {
            UpdateVoiceChatLabel();
            UpdateMusicVolumeLabel();
            UpdateSfxVolumeLabel();
        }
        
        private void UpdateVoiceChatLabel()
        {
            if (voiceChatLabel != null && voiceChatToggle != null)
            {
                voiceChatLabel.text = voiceChatToggle.isOn ? "ON" : "OFF";
            }
        }
        
        private void UpdateMusicVolumeLabel()
        {
            if (musicVolumeLabel != null && musicVolumeSlider != null)
            {
                musicVolumeLabel.text = $"{Mathf.RoundToInt(musicVolumeSlider.value)}%";
            }
        }
        
        private void UpdateSfxVolumeLabel()
        {
            if (sfxVolumeLabel != null && sfxVolumeSlider != null)
            {
                sfxVolumeLabel.text = $"{Mathf.RoundToInt(sfxVolumeSlider.value)}%";
            }
        }
        
        // public accessors for current values
        public bool VoiceChatEnabled => voiceChatToggle != null && voiceChatToggle.isOn;
        public float MusicVolume => musicVolumeSlider != null ? musicVolumeSlider.value / 100f : 0.8f;
        public float SfxVolume => sfxVolumeSlider != null ? sfxVolumeSlider.value / 100f : 0.8f;
    }
}
