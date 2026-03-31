using System.Collections.Generic;
using UnityEngine;
using Category5.Enemies;
using Category5.Player;

namespace Category5.Items
{
    // reaper's quota: kill 3+ enemies within the window to earn a delayed heal
    // heal fires after the window closes — bigger kill streaks = bigger heal
    public class ReapersQuotaBehaviour : ItemBehaviour
    {
        [SerializeField] private float windowDuration       = 6f;   // seconds the kill window stays open
        [SerializeField] private int   requiredKills         = 3;    // minimum kills to trigger the heal
        [SerializeField] private int[] baseHeal              = { 40, 52, 65, 80, 100 }; // flat heal on hitting the quota
        [SerializeField] private int[] bonusHealPerExtra     = { 8, 10, 13, 16, 20 };   // extra HP per kill beyond the quota

        // timestamps of kills in the current window (server only)
        private readonly List<float> _killTimes = new List<float>();
        private float _windowCloseTime = -1f;
        private bool _windowScheduled;

        protected override void OnInitialize()
        {
            if (!IsServer) return;
            EnemyBase.OnEnemyKilledBy += OnEnemyKilled;
            Debug.Log($"[ReapersQuota] initialized for client {OwnerClientId}");
        }

        protected override void OnTierChanged(int oldTier, int newTier) { }

        public override void OnRemoved()
        {
            EnemyBase.OnEnemyKilledBy -= OnEnemyKilled;
            _killTimes.Clear();
            _windowScheduled = false;
        }

        private void OnEnemyKilled(ulong killerClientId, Vector3 pos, GameObject enemy)
        {
            Debug.Log($"[ReapersQuota] OnEnemyKilled fired — killer={killerClientId}, ourClient={OwnerClientId}");
            if (killerClientId != OwnerClientId)
            {
                Debug.Log("[ReapersQuota] kill not ours, ignoring");
                return;
            }

            float now = Time.time;

            // prune kills that have fallen outside the rolling window
            _killTimes.RemoveAll(t => now - t > windowDuration);
            _killTimes.Add(now);

            Debug.Log($"[ReapersQuota] kill counted — window has {_killTimes.Count} kills, need {requiredKills}");

            // start / extend the window close timer
            _windowCloseTime = now + windowDuration;
            if (!_windowScheduled)
            {
                _windowScheduled = true;
                Debug.Log("[ReapersQuota] starting window coroutine");
                StartCoroutine(WaitForWindow());
            }
        }

        private System.Collections.IEnumerator WaitForWindow()
        {
            // keep yielding until no more time is left — extends naturally if new kills arrive
            while (Time.time < _windowCloseTime)
                yield return new WaitForSeconds(_windowCloseTime - Time.time);

            _windowScheduled = false;

            // OnEnemyKilled already maintains the rolling window so we don't re-prune here
            // re-pruning causes all kills to disappear because Time.time is slightly past the window close
            int killCount = _killTimes.Count;
            _killTimes.Clear();

            Debug.Log($"[ReapersQuota] window closed — {killCount} kills counted, need {requiredKills}");

            if (killCount < requiredKills)
            {
                Debug.Log("[ReapersQuota] not enough kills, no heal triggered");
                yield break;
            }

            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);
            int extraKills = killCount - requiredKills;
            int healAmount = baseHeal[idx] + bonusHealPerExtra[idx] * extraKills;

            Debug.Log($"[ReapersQuota] healing player for {healAmount} HP (base={baseHeal[idx]}, extra kills={extraKills}x{bonusHealPerExtra[idx]})");

            if (PlayerController != null)
                PlayerController.Heal(healAmount);
            else
                Debug.LogError("[ReapersQuota] PlayerController is null — heal could not be applied");
        }

        public override object[] GetFormatValues(int tier)
        {
            int idx = Mathf.Clamp(tier - 1, 0, 4);
            return new object[]
            {
                requiredKills,
                (int)windowDuration,
                baseHeal[idx],
                bonusHealPerExtra[idx]
            };
        }
    }
}
