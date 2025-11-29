
using UnityEngine;
using UnityEditor;
using Category5.Audio;

namespace Category5.Editor
{
    // editor utility to create common sound data assets
    public static class SoundDataAssetCreator
    {
        private const string BasePath = "Assets/Data/Audio";
        
        [MenuItem("Category5/Create Audio Assets/Create All Sound Data Assets")]
        public static void CreateAllSoundAssets()
        {
            CreatePlayerSounds();
            CreateBossSounds();
            CreateUISounds();
            CreateMusicAssets();
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("AudioAssetCreator: Created all SoundData assets in " + BasePath);
        }
        
        [MenuItem("Category5/Create Audio Assets/Player Sounds")]
        public static void CreatePlayerSounds()
        {
            EnsureDirectory(BasePath + "/Player");
            
            // combat sounds
            CreateSoundAsset("Player/PlayerAttackSwing", 0.8f, 0.1f, 1f, 0.1f, true);
            CreateSoundAsset("Player/PlayerLightHit", 0.9f, 0.1f, 1f, 0.15f, true);
            CreateSoundAsset("Player/PlayerHeavyHit", 1f, 0.05f, 0.9f, 0.1f, true);
            CreateSoundAsset("Player/PlayerHurt", 0.9f, 0.1f, 1f, 0.2f, true);
            CreateSoundAsset("Player/PlayerDeath", 1f, 0f, 1f, 0f, true);
            
            // movement sounds
            CreateSoundAsset("Player/PlayerDodge", 0.7f, 0.1f, 1f, 0.1f, true);
            CreateSoundAsset("Player/PlayerJump", 0.6f, 0.15f, 1.1f, 0.1f, true);
            CreateSoundAsset("Player/PlayerLand", 0.5f, 0.1f, 0.9f, 0.1f, true);
            CreateSoundAsset("Player/PlayerFootsteps", 0.4f, 0.15f, 1f, 0.2f, true);
            
            // feedback
            CreateSoundAsset("Player/PlayerHeal", 0.7f, 0.05f, 1.2f, 0.1f, false);
        }
        
        [MenuItem("Category5/Create Audio Assets/Boss Sounds")]
        public static void CreateBossSounds()
        {
            EnsureDirectory(BasePath + "/Boss");
            
            // general
            CreateSoundAsset("Boss/BossIdle", 0.3f, 0.05f, 1f, 0f, true, true); // looping
            CreateSoundAsset("Boss/BossHurt", 0.9f, 0.1f, 0.8f, 0.1f, true);
            CreateSoundAsset("Boss/BossDeath", 1f, 0f, 0.7f, 0f, true);
            CreateSoundAsset("Boss/BossSpawn", 1f, 0f, 0.8f, 0f, true);
            
            // attacks
            CreateSoundAsset("Boss/BossTelegraph", 0.7f, 0.1f, 1f, 0.1f, true);
            CreateSoundAsset("Boss/BossGroundSlam", 1f, 0f, 0.7f, 0.1f, true);
            CreateSoundAsset("Boss/BossLightningSweep", 0.9f, 0.05f, 1.1f, 0.1f, true);
            CreateSoundAsset("Boss/BossThunderClap", 1f, 0f, 0.6f, 0f, true);
            CreateSoundAsset("Boss/BossAttackHit", 0.8f, 0.1f, 0.9f, 0.1f, true);
        }
        
        [MenuItem("Category5/Create Audio Assets/UI Sounds")]
        public static void CreateUISounds()
        {
            EnsureDirectory(BasePath + "/UI");
            
            CreateSoundAsset("UI/UIHover", 0.4f, 0.05f, 1.2f, 0.1f, false);
            CreateSoundAsset("UI/UISelect", 0.6f, 0.05f, 1f, 0.05f, false);
            CreateSoundAsset("UI/PowerUpSelect", 0.8f, 0f, 1f, 0f, false);
            CreateSoundAsset("UI/PowerUpScreenAppear", 0.7f, 0f, 1f, 0f, false);
            CreateSoundAsset("UI/RoundStart", 0.9f, 0f, 1f, 0f, false);
            CreateSoundAsset("UI/VictoryFanfare", 1f, 0f, 1f, 0f, false);
            CreateSoundAsset("UI/GameOver", 0.9f, 0f, 1f, 0f, false);
        }
        
        [MenuItem("Category5/Create Audio Assets/Music")]
        public static void CreateMusicAssets()
        {
            EnsureDirectory(BasePath + "/Music");
            
            CreateSoundAsset("Music/MenuMusic", 0.5f, 0f, 1f, 0f, false, true);
            CreateSoundAsset("Music/CombatMusic", 0.6f, 0f, 1f, 0f, false, true);
            CreateSoundAsset("Music/PowerUpMusic", 0.5f, 0f, 1f, 0f, false, false);
            CreateSoundAsset("Music/VictoryMusic", 0.7f, 0f, 1f, 0f, false, true);
            CreateSoundAsset("Music/GameOverMusic", 0.6f, 0f, 1f, 0f, false, true);
        }
        
        private static void CreateSoundAsset(string name, float volume, float volumeVar, 
            float pitch, float pitchVar, bool is3D, bool loop = false)
        {
            string path = $"{BasePath}/{name}.asset";
            
            // check if already exists
            if (AssetDatabase.LoadAssetAtPath<SoundData>(path) != null)
            {
                Debug.Log($"SoundData asset already exists: {path}");
                return;
            }
            
            var soundData = ScriptableObject.CreateInstance<SoundData>();
            soundData.volume = volume;
            soundData.volumeVariation = volumeVar;
            soundData.pitch = pitch;
            soundData.pitchVariation = pitchVar;
            soundData.is3D = is3D;
            soundData.loop = loop;
            soundData.minDistance = 1f;
            soundData.maxDistance = is3D ? 30f : 100f;
            soundData.priority = loop ? 64 : 128;
            
            AssetDatabase.CreateAsset(soundData, path);
            Debug.Log($"Created SoundData: {path}");
        }
        
        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Split('/');
                string currentPath = parts[0];
                
                for (int i = 1; i < parts.Length; i++)
                {
                    string newPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                    }
                    currentPath = newPath;
                }
            }
        }
    }
}
