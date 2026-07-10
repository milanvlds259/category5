using UnityEngine;
using System;
using Category5.Core;
using Category5.Audio;

namespace Category5.SkillTree
{
    /// <summary>
    /// Tracks run statistics locally on each client using existing game events.
    /// At run end (victory or game over), awards skill points via SkillPointManager and saves.
    /// Also makes the earned amount available for VictoryUI/GameOverUI to display.
    /// </summary>
    public class RunStatisticsTracker : MonoBehaviour
    {
        public static RunStatisticsTracker Instance { get; private set; }

        /// <summary>Statistics for the current run. Reset when a new game scene loads.</summary>
        public RunStatistics CurrentStats { get; private set; } = new RunStatistics();

        /// <summary>Skill points earned from the most recent run (set at run end, read by UI).</summary>
        public int LastRunReward { get; private set; } = 0;

        /// <summary>Fired when skill points are awarded at run end. Passes the amount earned.</summary>
        public event Action<int> OnRunRewardAwarded;

        private bool _runEnded = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            UnsubscribeFromEvents();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void SubscribeToEvents()
        {
            EnemyEvents.OnEnemyDeath += OnEnemyDeath;
            BossEvents.OnBossDeath += OnBossDeath;
            GameEvents.OnVictory += OnVictory;
            GameEvents.OnGameOver += OnGameOver;
            GameEvents.OnRoundStart += OnRoundStart;
        }

        private void UnsubscribeFromEvents()
        {
            EnemyEvents.OnEnemyDeath -= OnEnemyDeath;
            BossEvents.OnBossDeath -= OnBossDeath;
            GameEvents.OnVictory -= OnVictory;
            GameEvents.OnGameOver -= OnGameOver;
            GameEvents.OnRoundStart -= OnRoundStart;
        }

        private void OnEnemyDeath(Vector3 position, Category5.Core.ElementType element)
        {
            if (_runEnded) return;
            CurrentStats.enemiesKilled++;
        }

        private void OnBossDeath(Vector3 position)
        {
            if (_runEnded) return;
            CurrentStats.bossesKilled++;
        }

        private void OnRoundStart(int roundNumber)
        {
            // Track the highest round reached
            if (roundNumber > CurrentStats.roundsSurvived)
            {
                CurrentStats.roundsSurvived = roundNumber;
            }
        }

        private void OnVictory()
        {
            if (_runEnded) return;
            CurrentStats.completedRun = true;
            AwardRewards();
        }

        private void OnGameOver()
        {
            if (_runEnded) return;
            CurrentStats.completedRun = false;
            AwardRewards();
        }

        /// <summary>Calculates and awards skill points for the current run, then saves.</summary>
        private void AwardRewards()
        {
            _runEnded = true;

            if (SkillPointManager.Instance == null)
            {
                Debug.LogError("RunStatisticsTracker: SkillPointManager not found!");
                return;
            }

            LastRunReward = SkillPointManager.Instance.AwardRunRewards(CurrentStats);

            // Store in save data for reference
            SaveSystem.Data.lastRun = new RunStatistics
            {
                enemiesKilled = CurrentStats.enemiesKilled,
                bossesKilled = CurrentStats.bossesKilled,
                roundsSurvived = CurrentStats.roundsSurvived,
                completedRun = CurrentStats.completedRun
            };
            SaveSystem.Save();

            OnRunRewardAwarded?.Invoke(LastRunReward);

            Debug.Log($"RunStatisticsTracker: Run ended. Earned {LastRunReward} skill points. " +
                      $"Enemies: {CurrentStats.enemiesKilled}, Bosses: {CurrentStats.bossesKilled}, " +
                      $"Rounds: {CurrentStats.roundsSurvived}, Won: {CurrentStats.completedRun}");
        }

        /// <summary>Resets the tracker for a new run. Called when a new game scene loads.</summary>
        public void ResetForNewRun()
        {
            CurrentStats = new RunStatistics();
            LastRunReward = 0;
            _runEnded = false;
        }

        /// <summary>Force-awards a reward with the current stats (for debug purposes).</summary>
        public void DebugAwardReward()
        {
            AwardRewards();
        }
    }
}