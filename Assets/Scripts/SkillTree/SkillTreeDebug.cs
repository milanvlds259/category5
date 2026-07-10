#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Category5.Core;

namespace Category5.SkillTree
{
    /// <summary>
    /// Editor-only debug menu for the skill tree system.
    /// Access via: Menu > Category5 > Debug > ...
    /// </summary>
    public static class SkillTreeDebug
    {
        private const int GRANT_AMOUNT = 100;

        [MenuItem("Category5/Debug/Grant 100 Skill Points")]
        public static void GrantSkillPoints()
        {
            // Ensure save data is loaded
            SaveSystem.Load();

            if (SkillPointManager.Instance == null)
            {
                // If SkillPointManager isn't in the scene, manipulate save data directly
                SaveSystem.Data.skillPoints += GRANT_AMOUNT;
                SaveSystem.Save();
                Debug.Log($"[SkillTreeDebug] Granted {GRANT_AMOUNT} skill points directly to save data. " +
                          $"Total: {SaveSystem.Data.skillPoints}");
            }
            else
            {
                SkillPointManager.Instance.AddPoints(GRANT_AMOUNT);
                Debug.Log($"[SkillTreeDebug] Granted {GRANT_AMOUNT} skill points via SkillPointManager. " +
                          $"Total: {SkillPointManager.Instance.CurrentSkillPoints}");
            }
        }

        [MenuItem("Category5/Debug/Grant 1000 Skill Points")]
        public static void Grant1000SkillPoints()
        {
            SaveSystem.Load();

            if (SkillPointManager.Instance == null)
            {
                SaveSystem.Data.skillPoints += 1000;
                SaveSystem.Save();
                Debug.Log($"[SkillTreeDebug] Granted 1000 skill points. Total: {SaveSystem.Data.skillPoints}");
            }
            else
            {
                SkillPointManager.Instance.AddPoints(1000);
                Debug.Log($"[SkillTreeDebug] Granted 1000 skill points. Total: {SkillPointManager.Instance.CurrentSkillPoints}");
            }
        }

        [MenuItem("Category5/Debug/Reset All Skill Tree Progress")]
        public static void ResetAllProgress()
        {
            if (!EditorUtility.DisplayDialog("Reset All Skill Tree Progress",
                "This will wipe ALL skill tree progress including skill points and unlocked nodes for ALL characters. Are you sure?",
                "Yes, reset everything", "Cancel"))
            {
                return;
            }

            SaveSystem.ResetSaveData();
            Debug.Log("[SkillTreeDebug] All skill tree progress has been reset.");
        }

        [MenuItem("Category5/Debug/Print Save Data")]
        public static void PrintSaveData()
        {
            SaveSystem.Load();
            var data = SaveSystem.Data;
            string report = $"=== Save Data ===\n" +
                            $"Skill Points: {data.skillPoints}\n" +
                            $"Class Unlocks: {data.classUnlocks.Count}\n";

            foreach (var entry in data.classUnlocks)
            {
                report += $"  Class {entry.classId}: {entry.unlockedNodeIds?.Count ?? 0} nodes unlocked\n";
            }

            report += $"Respec Data: {data.classRespecs.Count}\n";
            foreach (var entry in data.classRespecs)
            {
                report += $"  Class {entry.classId}: {entry.freeResetsRemaining} free resets remaining\n";
            }

            report += $"Last Run: Enemies={data.lastRun.enemiesKilled}, Bosses={data.lastRun.bossesKilled}, " +
                      $"Rounds={data.lastRun.roundsSurvived}, Won={data.lastRun.completedRun}\n";
            report += $"Save Path: {SaveSystem.GetSavePath()}";

            Debug.Log(report);
        }
    }
}
#endif