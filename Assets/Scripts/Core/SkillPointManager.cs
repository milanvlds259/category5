using UnityEngine;
using System;

namespace Category5.Core
{
    /// <summary>
    /// Singleton manager for the account-wide Skill Point currency.
    /// Awards points from run statistics and tracks the balance in SaveData.
    /// </summary>
    public class SkillPointManager : MonoBehaviour
    {
        public static SkillPointManager Instance { get; private set; }

        [Header("Currency Reward Tuning")]
        [Tooltip("Skill points awarded per enemy killed during a run.")]
        [SerializeField] private int pointsPerEnemyKilled = 1;

        [Tooltip("Skill points awarded per boss killed during a run.")]
        [SerializeField] private int pointsPerBossKilled = 25;

        [Tooltip("Base skill points awarded per round survived. Multiplied by the round number (e.g. round 3 = base * 3).")]
        [SerializeField] private int pointsPerRoundBase = 10;

        [Tooltip("Bonus skill points awarded for completing (winning) a run.")]
        [SerializeField] private int runCompletionBonus = 50;

        [Tooltip("Bonus skill points awarded when a run ends in game over (smaller consolation reward).")]
        [SerializeField] private int runGameOverBonus = 20;

        [Header("Respec Cost Tuning")]
        [Tooltip("Starting skill point cost for a respec after free resets are used up. Each subsequent respec doubles the cost.")]
        [SerializeField] private int baseRespecCost = 50;

        [Tooltip("Number of free respecs each character starts with.")]
        [SerializeField] private int startingFreeRespecs = 1;

        /// <summary>Fired when the skill point balance changes. New balance passed as int.</summary>
        public event Action<int> OnSkillPointsChanged;

        /// <summary>Current account-wide skill point balance.</summary>
        public int CurrentSkillPoints => SaveSystem.Data.skillPoints;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Adds skill points to the account and saves. Pass negative to subtract.</summary>
        public void AddPoints(int amount)
        {
            SaveSystem.Data.skillPoints += amount;
            SaveSystem.Data.skillPoints = Mathf.Max(0, SaveSystem.Data.skillPoints);
            SaveSystem.Save();
            OnSkillPointsChanged?.Invoke(SaveSystem.Data.skillPoints);
        }

        /// <summary>
        /// Attempts to spend skill points. Returns true if successful, false if insufficient balance.
        /// Does NOT save automatically - the caller should save after committing the purchase.
        /// </summary>
        public bool TrySpendPoints(int amount)
        {
            if (SaveSystem.Data.skillPoints < amount)
            {
                return false;
            }

            SaveSystem.Data.skillPoints -= amount;
            OnSkillPointsChanged?.Invoke(SaveSystem.Data.skillPoints);
            return true;
        }

        /// <summary>Calculates the total skill points earned from a run's statistics.</summary>
        public int CalculateRunReward(RunStatistics stats)
        {
            int total = 0;
            total += stats.enemiesKilled * pointsPerEnemyKilled;
            total += stats.bossesKilled * pointsPerBossKilled;

            // rounds survived: each round gives base * round number
            for (int r = 1; r <= stats.roundsSurvived; r++)
            {
                total += pointsPerRoundBase * r;
            }

            total += stats.completedRun ? runCompletionBonus : runGameOverBonus;
            return total;
        }

        /// <summary>
        /// Awards skill points from run statistics, saves, and returns the amount awarded.
        /// </summary>
        public int AwardRunRewards(RunStatistics stats)
        {
            int reward = CalculateRunReward(stats);
            AddPoints(reward);
            return reward;
        }

        /// <summary>Returns the remaining free respec count for a class.</summary>
        public int GetFreeRespecs(int classId)
        {
            return SaveSystem.Data.GetFreeRespecs(classId);
        }

        /// <summary>
        /// Calculates the respec cost for a class.
        /// Free if free resets remain; otherwise base cost doubling each subsequent respec.
        /// </summary>
        public int GetRespecCost(int classId)
        {
            int freeRemaining = SaveSystem.Data.GetFreeRespecs(classId);
            if (freeRemaining > 0)
            {
                return 0;
            }

            // calculate how many paid respecs have been done
            int paidRespecs = Mathf.Max(0, startingFreeRespecs - freeRemaining);
            return baseRespecCost * (int)Mathf.Pow(2, paidRespecs);
        }

        /// <summary>
        /// Consumes a free respec or spends points for a paid respec.
        /// Returns true if the respec was afforded, false otherwise.
        /// </summary>
        public bool TryPayRespec(int classId)
        {
            int freeRemaining = SaveSystem.Data.GetFreeRespecs(classId);

            if (freeRemaining > 0)
            {
                SaveSystem.Data.SetFreeRespecs(classId, freeRemaining - 1);
                return true;
            }

            int cost = GetRespecCost(classId);
            if (TrySpendPoints(cost))
            {
                return true;
            }

            return false;
        }

        /// <summary>Gets the starting free respec count (for initialization).</summary>
        public int GetStartingFreeRespecs()
        {
            return startingFreeRespecs;
        }
    }
}