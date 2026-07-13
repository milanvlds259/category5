using System;
using System.Collections.Generic;

namespace Category5.Core
{
    /// <summary>
    /// Root save data container serialized to JSON by SaveSystem.
    /// All metaprogression state lives here.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Account-wide skill point currency (shared across all characters).</summary>
        public int skillPoints = 0;

        /// <summary>
        /// Per-class unlocked node IDs. Key = classId, Value = array of unlocked nodeIds.
        /// Uses a serializable wrapper list for JSON compatibility.
        /// </summary>
        public List<ClassNodeUnlockData> classUnlocks = new List<ClassNodeUnlockData>();

        /// <summary>
        /// Per-class free respec counter. Key = classId, Value = remaining free resets.
        /// Uses a serializable wrapper list for JSON compatibility.
        /// </summary>
        public List<ClassRespecData> classRespecs = new List<ClassRespecData>();

        /// <summary>Statistics from the most recent run (for display and currency calculation).</summary>
        public RunStatistics lastRun = new RunStatistics();

        // --- Convenience accessors ---

        /// <summary>Returns the array of unlocked node IDs for the given class, or an empty array if none.</summary>
        public int[] GetUnlockedNodes(int classId)
        {
            foreach (var entry in classUnlocks)
            {
                if (entry.classId == classId)
                    return entry.unlockedNodeIds?.ToArray() ?? Array.Empty<int>();
            }
            return Array.Empty<int>();
        }

        /// <summary>Sets the unlocked node IDs for a class, replacing any existing entry.</summary>
        public void SetUnlockedNodes(int classId, List<int> nodeIds)
        {
            for (int i = 0; i < classUnlocks.Count; i++)
            {
                if (classUnlocks[i].classId == classId)
                {
                    classUnlocks[i].unlockedNodeIds = nodeIds;
                    return;
                }
            }
            classUnlocks.Add(new ClassNodeUnlockData { classId = classId, unlockedNodeIds = nodeIds });
        }

        /// <summary>Returns the remaining free respec count for a class, defaulting to 1.</summary>
        public int GetFreeRespecs(int classId)
        {
            foreach (var entry in classRespecs)
            {
                if (entry.classId == classId)
                    return entry.freeResetsRemaining;
            }
            return 1;
        }

        /// <summary>Sets the remaining free respec count for a class.</summary>
        public void SetFreeRespecs(int classId, int count)
        {
            for (int i = 0; i < classRespecs.Count; i++)
            {
                if (classRespecs[i].classId == classId)
                {
                    classRespecs[i].freeResetsRemaining = count;
                    return;
                }
            }
            classRespecs.Add(new ClassRespecData { classId = classId, freeResetsRemaining = count });
        }
    }

    /// <summary>Serializable wrapper for per-class unlocked nodes (JSON-friendly).</summary>
    [Serializable]
    public class ClassNodeUnlockData
    {
        public int classId;
        public List<int> unlockedNodeIds;
    }

    /// <summary>Serializable wrapper for per-class free respec count (JSON-friendly).</summary>
    [Serializable]
    public class ClassRespecData
    {
        public int classId;
        public int freeResetsRemaining;
    }

    /// <summary>
    /// Statistics tracked during a single run.
    /// Used to calculate skill point rewards at run end.
    /// </summary>
    [Serializable]
    public class RunStatistics
    {
        public int enemiesKilled = 0;
        public int bossesKilled = 0;
        public int roundsSurvived = 0;
        public bool completedRun = false;
    }
}