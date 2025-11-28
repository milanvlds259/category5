using UnityEngine;
using UnityEditor;
using Category5.Core;

namespace Category5.Boss.Editor
{
    public static class BossAttackAssetCreator
    {
        [MenuItem("Category5/Create Boss Attack Assets")]
        public static void CreateBossAttackAssets()
        {
            string folder = "Assets/Data/BossAttacks";
            
            // create folder if it doesnt exist
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/Data", "BossAttacks");
            }
            
            // create ground slam attack
            CreateGroundSlamAttack(folder);
            
            // create lightning sweep attack
            CreateLightningSweepAttack(folder);
            
            // create thunder clap attack
            CreateThunderClapAttack(folder);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("Boss attack assets created in " + folder);
            EditorUtility.DisplayDialog("Boss Attacks Created", 
                "Created 3 boss attack assets:\n\n" +
                "• Ground Slam (60% weight)\n" +
                "• Lightning Sweep (30% weight)\n" +
                "• Thunder Clap (10% weight, <30% HP only)\n\n" +
                "Assets saved to: " + folder, 
                "OK");
        }
        
        private static void CreateGroundSlamAttack(string folder)
        {
            string path = folder + "/GroundSlam.asset";
            
            // skip if already exists
            if (AssetDatabase.LoadAssetAtPath<BossAttackData>(path) != null)
            {
                Debug.Log("Ground Slam already exists, skipping...");
                return;
            }
            
            var attack = ScriptableObject.CreateInstance<BossAttackData>();
            
            // identity
            attack.attackName = "Ground Slam";
            attack.attackType = BossAttackType.Slam;
            
            // selection - 60% weight per design doc
            attack.selectionWeight = 60f;
            attack.healthThreshold = 1f; // always available
            attack.minRange = 0f;
            attack.maxRange = 8f; // close range attack
            
            // timing
            attack.telegraphDuration = 1.5f;
            attack.attackDuration = 0.5f;
            attack.cooldownDuration = 1f;
            
            // damage - 40 damage aoe per design doc
            attack.damage = 40;
            attack.damageRadius = 3f; // 3m radius per design doc
            attack.damageOffset = Vector3.zero; // centered on boss
            
            // no lunge for slam
            attack.hasLunge = false;
            
            // feedback
            attack.isHeavyAttack = true;
            attack.telegraphColor = new Color(1f, 0.2f, 0.2f, 0.5f); // red
            attack.gizmoColor = new Color(1f, 0.5f, 0f, 1f); // orange for slam
            
            AssetDatabase.CreateAsset(attack, path);
        }
        
        private static void CreateLightningSweepAttack(string folder)
        {
            string path = folder + "/LightningSweep.asset";
            
            if (AssetDatabase.LoadAssetAtPath<BossAttackData>(path) != null)
            {
                Debug.Log("Lightning Sweep already exists, skipping...");
                return;
            }
            
            var attack = ScriptableObject.CreateInstance<BossAttackData>();
            
            // identity
            attack.attackName = "Lightning Sweep";
            attack.attackType = BossAttackType.Swipe;
            
            // selection - 30% weight per design doc
            attack.selectionWeight = 30f;
            attack.healthThreshold = 1f; // always available
            attack.minRange = 0f;
            attack.maxRange = 15f; // mid range attack
            
            // timing
            attack.telegraphDuration = 1.5f;
            attack.attackDuration = 1.5f; // slower sweep
            attack.cooldownDuration = 1f;
            
            // damage - 30 damage per design doc
            attack.damage = 30;
            attack.isSweep = true;
            attack.sweepAngle = 180f; // 180 degree sweep per design doc
            attack.sweepLength = 10f;
            attack.sweepWidth = 2f;
            
            // feedback
            attack.isHeavyAttack = false;
            attack.telegraphColor = new Color(1f, 1f, 0.2f, 0.5f); // yellow per design doc
            attack.gizmoColor = new Color(1f, 1f, 0f, 1f); // yellow for sweep
            
            AssetDatabase.CreateAsset(attack, path);
        }
        
        private static void CreateThunderClapAttack(string folder)
        {
            string path = folder + "/ThunderClap.asset";
            
            if (AssetDatabase.LoadAssetAtPath<BossAttackData>(path) != null)
            {
                Debug.Log("Thunder Clap already exists, skipping...");
                return;
            }
            
            var attack = ScriptableObject.CreateInstance<BossAttackData>();
            
            // identity
            attack.attackName = "Thunder Clap";
            attack.attackType = BossAttackType.AoE;
            
            // selection - 10% weight, only when below 30% hp per design doc
            attack.selectionWeight = 10f;
            attack.healthThreshold = 0.3f; // only below 30% hp
            attack.minRange = 0f;
            attack.maxRange = 100f; // any range
            
            // timing - longer telegraph for warning
            attack.telegraphDuration = 2f;
            attack.attackDuration = 0.5f;
            attack.cooldownDuration = 1.5f;
            
            // damage - 50 damage massive aoe per design doc
            attack.damage = 50;
            attack.damageRadius = 8f; // large aoe
            attack.damageOffset = Vector3.zero; // centered on boss
            
            // feedback
            attack.isHeavyAttack = true;
            attack.telegraphColor = new Color(1f, 0.1f, 0.1f, 0.7f); // bright red for danger
            attack.gizmoColor = new Color(1f, 0f, 1f, 1f); // magenta for enrage attack
            
            AssetDatabase.CreateAsset(attack, path);
        }
    }
}
