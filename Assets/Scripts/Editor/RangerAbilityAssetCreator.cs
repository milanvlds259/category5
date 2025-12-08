using UnityEngine;
using UnityEditor;
using System.IO;
using Category5;

namespace Category5.Editor
{
    public static class RangerAbilityAssetCreator
    {
        [MenuItem("Category5/Create Ranger Ability Assets")]
        public static void CreateRangerAbilities()
        {
            string folderPath = "Assets/Data/Abilities/Ranger";
            
            // ensure folder exists
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }
            
            // quickbow (q)
            CreateQuickbowAbility(folderPath);
            
            // spiralbow (e)
            CreateSpiralbowAbility(folderPath);
            
            // critshot (r)
            CreateCritshotAbility(folderPath);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("Created 3 Ranger ability assets in " + folderPath);
        }
        
        private static void CreateQuickbowAbility(string folderPath)
        {
            AbilityData ability = ScriptableObject.CreateInstance<AbilityData>();
            
            ability.abilityName = "Quickbow";
            ability.description = "Increases attack speed and charge speed for 5 seconds. Fully charged shots fire a rapid burst of 5 arrows.";
            ability.cooldownDuration = 10f;
            ability.baseDamage = 0f; // no direct damage, it's a buff
            ability.castTime = 0f;
            ability.manaCost = 0f;
            
            string assetPath = Path.Combine(folderPath, "Quickbow.asset");
            AssetDatabase.CreateAsset(ability, assetPath);
        }
        
        private static void CreateSpiralbowAbility(string folderPath)
        {
            AbilityData ability = ScriptableObject.CreateInstance<AbilityData>();
            
            ability.abilityName = "Spiralbow";
            ability.description = "Fire a tracker arrow that creates a damage-over-time zone on impact. Enemies in the zone take damage every 0.5s and are slowed by 40%.";
            ability.cooldownDuration = 12f;
            ability.baseDamage = 10f; // damage per tick
            ability.castTime = 0f;
            ability.manaCost = 0f;
            
            string assetPath = Path.Combine(folderPath, "Spiralbow.asset");
            AssetDatabase.CreateAsset(ability, assetPath);
        }
        
        private static void CreateCritshotAbility(string folderPath)
        {
            AbilityData ability = ScriptableObject.CreateInstance<AbilityData>();
            
            ability.abilityName = "Critshot";
            ability.description = "Fire an instant-charged piercing arrow that deals 3x damage. Pierces through all enemies and walls, only stops on bosses.";
            ability.cooldownDuration = 30f;
            ability.baseDamage = 45f; // 3x normal arrow damage (15 base * 3)
            ability.castTime = 0f;
            ability.manaCost = 0f;
            
            string assetPath = Path.Combine(folderPath, "Critshot.asset");
            AssetDatabase.CreateAsset(ability, assetPath);
        }
    }
}
