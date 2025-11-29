using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using Category5.Core;
using Category5.Boss;

namespace Category5.Audio
{
    // centralized audio manager for the game
    // designers can assign audio clips in the inspector
    // automatically hooks into game events to play sounds
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        
        // =====================================
        // player sounds
        // =====================================
        
        [Header("player - combat")]
        [Tooltip("sound when player swings their weapon (plays on attack start)")]
        [SerializeField] private SoundData playerAttackSwing;
        
        [Tooltip("sound when player's light attack connects with enemy")]
        [SerializeField] private SoundData playerLightHit;
        
        [Tooltip("sound when player's heavy attack (combo finisher) connects")]
        [SerializeField] private SoundData playerHeavyHit;
        
        [Tooltip("sound when player takes damage")]
        [SerializeField] private SoundData playerHurt;
        
        [Tooltip("sound when player dies")]
        [SerializeField] private SoundData playerDeath;
        
        [Header("player - movement")]
        [Tooltip("sound when player performs a dodge roll")]
        [SerializeField] private SoundData playerDodge;
        
        [Tooltip("sound when player jumps")]
        [SerializeField] private SoundData playerJump;
        
        [Tooltip("sound when player lands on ground")]
        [SerializeField] private SoundData playerLand;
        
        [Tooltip("footstep sounds while running")]
        [SerializeField] private SoundData playerFootsteps;
        
        [Header("player - feedback")]
        [Tooltip("sound when player heals (lifesteal, etc)")]
        [SerializeField] private SoundData playerHeal;
        
        // =====================================
        // boss sounds
        // =====================================
        
        [Header("boss - general")]
        [Tooltip("ambient idle sound/breathing for boss (loops)")]
        [SerializeField] private SoundData bossIdle;
        
        [Tooltip("sound when boss takes damage")]
        [SerializeField] private SoundData bossHurt;
        
        [Tooltip("sound when boss dies")]
        [SerializeField] private SoundData bossDeath;
        
        [Tooltip("sound when boss spawns/appears")]
        [SerializeField] private SoundData bossSpawn;
        
        [Header("boss - attacks")]
        [Tooltip("generic telegraph/windup sound before any attack")]
        [SerializeField] private SoundData bossTelegraph;
        
        [Tooltip("ground slam attack sound")]
        [SerializeField] private SoundData bossGroundSlam;
        
        [Tooltip("lightning sweep attack sound")]
        [SerializeField] private SoundData bossLightningSweep;
        
        [Tooltip("thunder clap attack sound")]
        [SerializeField] private SoundData bossThunderClap;
        
        [Header("boss - attack hit")]
        [Tooltip("sound when boss attack hits a player")]
        [SerializeField] private SoundData bossAttackHit;
        
        // =====================================
        // ui sounds
        // =====================================
        
        [Header("ui")]
        [Tooltip("sound when hovering over a button or card")]
        [SerializeField] private SoundData uiHover;
        
        [Tooltip("sound when clicking/selecting a button")]
        [SerializeField] private SoundData uiSelect;
        
        [Tooltip("sound when power-up card is selected")]
        [SerializeField] private SoundData powerUpSelect;
        
        [Tooltip("sound when power-up selection screen appears")]
        [SerializeField] private SoundData powerUpScreenAppear;
        
        [Tooltip("sound when round starts")]
        [SerializeField] private SoundData roundStart;
        
        [Tooltip("sound when game is won")]
        [SerializeField] private SoundData victoryFanfare;
        
        [Tooltip("sound when all players die (game over)")]
        [SerializeField] private SoundData gameOverSound;
        
        // =====================================
        // music
        // =====================================
        
        [Header("music")]
        [Tooltip("main menu background music")]
        [SerializeField] private SoundData menuMusic;
        
        [Tooltip("combat/boss fight music")]
        [SerializeField] private SoundData combatMusic;
        
        [Tooltip("power-up selection music/sting")]
        [SerializeField] private SoundData powerUpMusic;
        
        [Tooltip("victory music")]
        [SerializeField] private SoundData victoryMusic;
        
        [Tooltip("game over music")]
        [SerializeField] private SoundData gameOverMusic;
        
        [Header("music settings")]
        [Tooltip("time in seconds to crossfade between music tracks")]
        [SerializeField] private float musicCrossfadeDuration = 1f;
        
        // =====================================
        // audio pooling settings
        // =====================================
        
        [Header("audio pool settings")]
        [Tooltip("number of audio sources to pool for sfx")]
        [SerializeField] private int sfxPoolSize = 20;
        
        [Tooltip("optional: main audio mixer for volume control")]
        [SerializeField] private AudioMixer mainMixer;
        
        // =====================================
        // private state
        // =====================================
        
        private List<AudioSource> _sfxPool = new List<AudioSource>();
        private int _currentPoolIndex = 0;
        
        private AudioSource _musicSourceA;
        private AudioSource _musicSourceB;
        private AudioSource _currentMusicSource;
        private Coroutine _musicFadeCoroutine;
        
        private AudioSource _bossIdleSource; // dedicated source for looping boss idle
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            InitializeAudioPool();
            InitializeMusicSources();
        }
        
        private void OnEnable()
        {
            SubscribeToEvents();
        }
        
        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }
        
        // =====================================
        // initialization
        // =====================================
        
        private void InitializeAudioPool()
        {
            // create audio source pool for sfx
            GameObject poolParent = new GameObject("SFX Pool");
            poolParent.transform.SetParent(transform);
            
            for (int i = 0; i < sfxPoolSize; i++)
            {
                GameObject sourceObj = new GameObject($"SFX Source {i}");
                sourceObj.transform.SetParent(poolParent.transform);
                AudioSource source = sourceObj.AddComponent<AudioSource>();
                source.playOnAwake = false;
                _sfxPool.Add(source);
            }
        }
        
        private void InitializeMusicSources()
        {
            // create two music sources for crossfading
            GameObject musicObj = new GameObject("Music Sources");
            musicObj.transform.SetParent(transform);
            
            _musicSourceA = musicObj.AddComponent<AudioSource>();
            _musicSourceA.playOnAwake = false;
            _musicSourceA.loop = true;
            _musicSourceA.priority = 0;
            _musicSourceA.spatialBlend = 0f; // 2D
            
            // add second source to same object
            _musicSourceB = musicObj.AddComponent<AudioSource>();
            _musicSourceB.playOnAwake = false;
            _musicSourceB.loop = true;
            _musicSourceB.priority = 0;
            _musicSourceB.spatialBlend = 0f; // 2D
            
            _currentMusicSource = _musicSourceA;
        }
        
        // =====================================
        // event subscriptions
        // =====================================
        
        private void SubscribeToEvents()
        {
            // hit feedback events (most common sfx triggers)
            HitFeedbackManager.OnPlayerHitEnemy += OnPlayerHitEnemy;
            HitFeedbackManager.OnPlayerTakeDamage += OnPlayerTakeDamage;
            HitFeedbackManager.OnBossAttackTelegraph += OnBossAttackTelegraph;
            HitFeedbackManager.OnBossAttackExecute += OnBossAttackExecute;
            
            // boss attack events (for specific attack sounds)
            TestBoss.OnAttackTelegraphStart += OnBossAttackTelegraphStart;
            TestBoss.OnAttackExecute += OnBossAttackExecuteSpecific;
            TestBoss.OnAttackHitTarget += OnBossAttackHitTarget;
            
            // player events
            PlayerEvents.OnPlayerDodge += OnPlayerDodge;
            PlayerEvents.OnPlayerJump += OnPlayerJump;
            PlayerEvents.OnPlayerLand += OnPlayerLand;
            PlayerEvents.OnPlayerDeath += OnPlayerDeath;
            PlayerEvents.OnPlayerHeal += OnPlayerHeal;
            PlayerEvents.OnPlayerAttackSwing += OnPlayerAttackSwing;
            
            // boss events
            BossEvents.OnBossDeath += OnBossDeath;
            BossEvents.OnBossSpawn += OnBossSpawn;
            BossEvents.OnBossHurt += OnBossHurt;
            
            // game flow events
            GameEvents.OnRoundStart += OnRoundStart;
            GameEvents.OnPowerUpSelectionStart += OnPowerUpSelectionStart;
            GameEvents.OnPowerUpSelected += OnPowerUpSelected;
            GameEvents.OnVictory += OnVictory;
            GameEvents.OnGameOver += OnGameOver;
        }
        
        private void UnsubscribeFromEvents()
        {
            HitFeedbackManager.OnPlayerHitEnemy -= OnPlayerHitEnemy;
            HitFeedbackManager.OnPlayerTakeDamage -= OnPlayerTakeDamage;
            HitFeedbackManager.OnBossAttackTelegraph -= OnBossAttackTelegraph;
            HitFeedbackManager.OnBossAttackExecute -= OnBossAttackExecute;
            
            TestBoss.OnAttackTelegraphStart -= OnBossAttackTelegraphStart;
            TestBoss.OnAttackExecute -= OnBossAttackExecuteSpecific;
            TestBoss.OnAttackHitTarget -= OnBossAttackHitTarget;
            
            PlayerEvents.OnPlayerDodge -= OnPlayerDodge;
            PlayerEvents.OnPlayerJump -= OnPlayerJump;
            PlayerEvents.OnPlayerLand -= OnPlayerLand;
            PlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            PlayerEvents.OnPlayerHeal -= OnPlayerHeal;
            PlayerEvents.OnPlayerAttackSwing -= OnPlayerAttackSwing;
            
            BossEvents.OnBossDeath -= OnBossDeath;
            BossEvents.OnBossSpawn -= OnBossSpawn;
            BossEvents.OnBossHurt -= OnBossHurt;
            
            GameEvents.OnRoundStart -= OnRoundStart;
            GameEvents.OnPowerUpSelectionStart -= OnPowerUpSelectionStart;
            GameEvents.OnPowerUpSelected -= OnPowerUpSelected;
            GameEvents.OnVictory -= OnVictory;
            GameEvents.OnGameOver -= OnGameOver;
        }
        
        // =====================================
        // event handlers - player
        // =====================================
        
        private void OnPlayerHitEnemy(Vector3 position, int damage, bool isCritical)
        {
            if (isCritical)
            {
                PlaySound(playerHeavyHit, position);
            }
            else
            {
                PlaySound(playerLightHit, position);
            }
        }
        
        private void OnPlayerTakeDamage(Vector3 position, int damage)
        {
            PlaySound(playerHurt, position);
        }
        
        private void OnPlayerDodge(Vector3 position)
        {
            PlaySound(playerDodge, position);
        }
        
        private void OnPlayerJump(Vector3 position)
        {
            PlaySound(playerJump, position);
        }
        
        private void OnPlayerLand(Vector3 position)
        {
            PlaySound(playerLand, position);
        }
        
        private void OnPlayerDeath(Vector3 position)
        {
            PlaySound(playerDeath, position);
        }
        
        private void OnPlayerHeal(Vector3 position, int amount)
        {
            PlaySound(playerHeal, position);
        }
        
        private void OnPlayerAttackSwing(Vector3 position)
        {
            PlaySound(playerAttackSwing, position);
        }
        
        // =====================================
        // event handlers - boss
        // =====================================
        
        private void OnBossAttackTelegraph(BossAttackType attackType, Vector3 position)
        {
            PlaySound(bossTelegraph, position);
        }
        
        private void OnBossAttackExecute(BossAttackType attackType, Vector3 position)
        {
            // generic boss attack sound fallback
            // specific attacks handled by OnBossAttackExecuteSpecific
        }
        
        private void OnBossAttackTelegraphStart(BossAttackData attack, Vector3 position)
        {
            // use attack-specific telegraph sound if available
            if (attack != null && attack.telegraphSound != null)
            {
                PlayClip(attack.telegraphSound, position);
            }
            else
            {
                PlaySound(bossTelegraph, position);
            }
        }
        
        private void OnBossAttackExecuteSpecific(BossAttackData attack, Vector3 position)
        {
            // use attack-specific sound if available
            if (attack != null && attack.attackSound != null)
            {
                PlayClip(attack.attackSound, position);
                return;
            }
            
            // fallback to attack name matching
            if (attack != null)
            {
                string attackName = attack.attackName.ToLower();
                
                if (attackName.Contains("slam") || attackName.Contains("ground"))
                {
                    PlaySound(bossGroundSlam, position);
                }
                else if (attackName.Contains("sweep") || attackName.Contains("lightning"))
                {
                    PlaySound(bossLightningSweep, position);
                }
                else if (attackName.Contains("clap") || attackName.Contains("thunder"))
                {
                    PlaySound(bossThunderClap, position);
                }
            }
        }
        
        private void OnBossAttackHitTarget(BossAttackData attack, Vector3 position, GameObject target)
        {
            PlaySound(bossAttackHit, position);
        }
        
        private void OnBossDeath(Vector3 position)
        {
            PlaySound(bossDeath, position);
            StopBossIdleLoop();
        }
        
        private void OnBossSpawn(Vector3 position)
        {
            PlaySound(bossSpawn, position);
            StartBossIdleLoop(position);
        }
        
        private void OnBossHurt(Vector3 position, int damage)
        {
            PlaySound(bossHurt, position);
        }
        
        // =====================================
        // event handlers - game flow
        // =====================================
        
        private void OnRoundStart(int roundNumber)
        {
            PlaySound2D(roundStart);
            PlayMusic(combatMusic);
        }
        
        private void OnPowerUpSelectionStart()
        {
            PlaySound2D(powerUpScreenAppear);
            // optionally switch to power-up music
            if (powerUpMusic != null && powerUpMusic.clips != null && powerUpMusic.clips.Length > 0)
            {
                PlayMusic(powerUpMusic);
            }
        }
        
        private void OnPowerUpSelected(string powerUpName)
        {
            PlaySound2D(powerUpSelect);
        }
        
        private void OnVictory()
        {
            PlaySound2D(victoryFanfare);
            PlayMusic(victoryMusic);
        }
        
        private void OnGameOver()
        {
            PlaySound2D(gameOverSound);
            PlayMusic(gameOverMusic);
        }
        
        // =====================================
        // public api - sound playback
        // =====================================
        
        // play a sound at a 3D position
        public void PlaySound(SoundData sound, Vector3 position)
        {
            if (sound == null) return;
            
            AudioClip clip = sound.GetClip();
            if (clip == null) return;
            
            AudioSource source = GetPooledSource();
            ConfigureSource(source, sound);
            source.transform.position = position;
            source.clip = clip;
            source.Play();
        }
        
        // play a sound in 2D (no positional audio)
        public void PlaySound2D(SoundData sound)
        {
            if (sound == null) return;
            
            AudioClip clip = sound.GetClip();
            if (clip == null) return;
            
            AudioSource source = GetPooledSource();
            ConfigureSource(source, sound);
            source.spatialBlend = 0f; // force 2D
            source.clip = clip;
            source.Play();
        }
        
        // play a raw audio clip at a position (for attack-specific sounds)
        public void PlayClip(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            
            AudioSource source = GetPooledSource();
            source.clip = clip;
            source.volume = volume;
            source.pitch = 1f;
            source.spatialBlend = 1f;
            source.transform.position = position;
            source.Play();
        }
        
        // =====================================
        // public api - music
        // =====================================
        
        // play music with optional crossfade
        public void PlayMusic(SoundData music)
        {
            if (music == null) return;
            
            AudioClip clip = music.GetClip();
            if (clip == null) return;
            
            if (_musicFadeCoroutine != null)
            {
                StopCoroutine(_musicFadeCoroutine);
            }
            
            _musicFadeCoroutine = StartCoroutine(CrossfadeMusic(clip, music.volume));
        }
        
        // stop music with optional fadeout
        public void StopMusic(float fadeOutDuration = 1f)
        {
            if (_musicFadeCoroutine != null)
            {
                StopCoroutine(_musicFadeCoroutine);
            }
            
            _musicFadeCoroutine = StartCoroutine(FadeOutMusic(fadeOutDuration));
        }
        
        // =====================================
        // public api - ui sounds
        // =====================================
        
        // call this from UI buttons for hover sound
        public void PlayUIHover()
        {
            PlaySound2D(uiHover);
        }
        
        // call this from UI buttons for click sound
        public void PlayUISelect()
        {
            PlaySound2D(uiSelect);
        }
        
        // =====================================
        // boss idle loop management
        // =====================================
        
        private void StartBossIdleLoop(Vector3 position)
        {
            if (bossIdle == null) return;
            
            AudioClip clip = bossIdle.GetClip();
            if (clip == null) return;
            
            if (_bossIdleSource == null)
            {
                GameObject idleObj = new GameObject("Boss Idle Audio");
                idleObj.transform.SetParent(transform);
                _bossIdleSource = idleObj.AddComponent<AudioSource>();
            }
            
            ConfigureSource(_bossIdleSource, bossIdle);
            _bossIdleSource.loop = true;
            _bossIdleSource.transform.position = position;
            _bossIdleSource.clip = clip;
            _bossIdleSource.Play();
        }
        
        private void StopBossIdleLoop()
        {
            if (_bossIdleSource != null && _bossIdleSource.isPlaying)
            {
                _bossIdleSource.Stop();
            }
        }
        
        // update boss idle position to follow boss
        public void UpdateBossIdlePosition(Vector3 position)
        {
            if (_bossIdleSource != null)
            {
                _bossIdleSource.transform.position = position;
            }
        }
        
        // =====================================
        // private helpers
        // =====================================
        
        private AudioSource GetPooledSource()
        {
            // round-robin through the pool
            AudioSource source = _sfxPool[_currentPoolIndex];
            _currentPoolIndex = (_currentPoolIndex + 1) % _sfxPool.Count;
            
            // if source is still playing, we've run out of pool space
            // the new sound will interrupt the old one
            return source;
        }
        
        private void ConfigureSource(AudioSource source, SoundData sound)
        {
            source.volume = sound.GetVolume();
            source.pitch = sound.GetPitch();
            source.spatialBlend = sound.is3D ? 1f : 0f;
            source.minDistance = sound.minDistance;
            source.maxDistance = sound.maxDistance;
            source.priority = sound.priority;
            source.loop = sound.loop;
            source.outputAudioMixerGroup = sound.mixerGroup;
        }
        
        private System.Collections.IEnumerator CrossfadeMusic(AudioClip newClip, float targetVolume)
        {
            // determine which source to fade to
            AudioSource fadeOutSource = _currentMusicSource;
            AudioSource fadeInSource = _currentMusicSource == _musicSourceA ? _musicSourceB : _musicSourceA;
            
            // setup new music on fade-in source
            fadeInSource.clip = newClip;
            fadeInSource.volume = 0f;
            fadeInSource.Play();
            
            float elapsed = 0f;
            float startVolume = fadeOutSource.volume;
            
            while (elapsed < musicCrossfadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / musicCrossfadeDuration;
                
                fadeOutSource.volume = Mathf.Lerp(startVolume, 0f, t);
                fadeInSource.volume = Mathf.Lerp(0f, targetVolume, t);
                
                yield return null;
            }
            
            fadeOutSource.Stop();
            fadeOutSource.volume = 0f;
            fadeInSource.volume = targetVolume;
            
            _currentMusicSource = fadeInSource;
            _musicFadeCoroutine = null;
        }
        
        private System.Collections.IEnumerator FadeOutMusic(float duration)
        {
            float startVolume = _currentMusicSource.volume;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _currentMusicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }
            
            _currentMusicSource.Stop();
            _currentMusicSource.volume = 0f;
            _musicFadeCoroutine = null;
        }
    }
}
