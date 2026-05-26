using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Category5.Enemies;
using Category5.Player;

namespace Category5.Items
{
    public class MarkOfTheAlphaBehaviour : ItemBehaviour
    {
        [Header("Per-Stack Values (indexed by tier-1)")]
        [SerializeField] private float[] critChancePerStack = { 0.04f, 0.05f, 0.06f, 0.075f, 0.09f };
        [SerializeField] private float[] manaRegenPerStack  = { 0.15f, 0.18f, 0.22f, 0.27f, 0.33f };
        [SerializeField] private int[]   maxStacks           = { 4, 5, 6, 7, 8 };

        [Header("AOE")]
        [SerializeField] private float baseRadius       = 5f;
        [SerializeField] private float radiusPerStack   = 1.5f;

        [Header("Timing")]
        [SerializeField] private float combatTimeout    = 8f;
        [SerializeField] private float auraTickInterval = 0.5f;

        [Header("Aura Visual")]
        [SerializeField] private LineRenderer auraRing;
        [SerializeField] private int           ringSegments = 32;
        [SerializeField] private float         ringHeight   = 0.1f;
        [SerializeField] private Color         ringColor    = new Color(1f, 0.84f, 0f, 0.6f);
        [SerializeField] private float         ringWidth    = 0.15f;

        private int    _stacks;
        private float  _lastKillTime;
        private float  _currentRadius;
        private Coroutine _auraCoroutine;
        private Coroutine _combatTimerCoroutine;
        private HashSet<PlayerStats> _buffedPlayers = new HashSet<PlayerStats>();

        private static readonly int PlayerLayer = 3;

        protected override void OnInitialize()
        {
            if (IsServer)
            {
                EnemyBase.OnEnemyKilledBy += OnEnemyKilled;
                PlayerController.OnPlayerTookDamage += OnTookDamage;
            }

            if (auraRing == null)
                auraRing = CreateAuraRing();

            if (auraRing != null)
                auraRing.gameObject.SetActive(false);
        }

        private LineRenderer CreateAuraRing()
        {
            var ringObj = new GameObject("AuraRing");
            ringObj.transform.SetParent(transform);
            ringObj.transform.localPosition = Vector3.zero;
            ringObj.transform.localRotation = Quaternion.identity;

            var lr = ringObj.AddComponent<LineRenderer>();
            lr.loop = false;
            lr.useWorldSpace = false;
            lr.startWidth = ringWidth;
            lr.endWidth = ringWidth;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = ringColor;
            lr.endColor = ringColor;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        protected override void OnTierChanged(int oldTier, int newTier)
        {
        }

        public override void OnRemoved()
        {
            if (IsServer)
            {
                EnemyBase.OnEnemyKilledBy -= OnEnemyKilled;
                if (PlayerController != null)
                    PlayerController.OnPlayerTookDamage -= OnTookDamage;
                ResetAura();
            }

            if (_auraCoroutine != null)
            {
                StopCoroutine(_auraCoroutine);
                _auraCoroutine = null;
            }
            if (_combatTimerCoroutine != null)
            {
                StopCoroutine(_combatTimerCoroutine);
                _combatTimerCoroutine = null;
            }

            if (auraRing != null)
                auraRing.gameObject.SetActive(false);
        }

        private void OnEnemyKilled(ulong killerClientId, Vector3 pos, GameObject enemy)
        {
            if (killerClientId != OwnerClientId) return;

            int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);
            if (_stacks >= maxStacks[idx]) return;

            _stacks++;
            _lastKillTime = Time.time;
            _currentRadius = baseRadius + _stacks * radiusPerStack;

            if (_stacks == 1)
            {
                _auraCoroutine = StartCoroutine(AuraTick());
                _combatTimerCoroutine = StartCoroutine(CombatTimer());
            }
            else
            {
                StopCoroutine(_combatTimerCoroutine);
                _combatTimerCoroutine = StartCoroutine(CombatTimer());
            }

            SyncVisualStacks();
        }

        private void OnTookDamage(int damage)
        {
            if (_stacks == 0) return;
            ResetAura();
        }

        private void ResetAura()
        {
            ClearAllBuffs();
            _stacks = 0;
            _currentRadius = 0f;

            if (_auraCoroutine != null)
            {
                StopCoroutine(_auraCoroutine);
                _auraCoroutine = null;
            }
            if (_combatTimerCoroutine != null)
            {
                StopCoroutine(_combatTimerCoroutine);
                _combatTimerCoroutine = null;
            }

            SyncVisualStacks();
        }

        private void ClearAllBuffs()
        {
            foreach (var ps in _buffedPlayers)
            {
                if (ps != null)
                {
                    ps.SetDynamicCritChanceBonus(0f);
                    ps.SetDynamicManaRegenBonus(0f);
                }
            }
            _buffedPlayers.Clear();
        }

        private IEnumerator AuraTick()
        {
            var buffer = new Collider[32];
            var layerMask = 1 << PlayerLayer;

            while (_stacks > 0)
            {
                yield return new WaitForSeconds(auraTickInterval);

                if (!IsServer || _stacks <= 0) continue;

                int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _currentRadius, buffer, layerMask);

                var currentPlayers = new HashSet<PlayerStats>();
                for (int i = 0; i < hitCount; i++)
                {
                    var pc = buffer[i].GetComponentInParent<PlayerController>();
                    if (pc == null || pc.IsDead.Value) continue;

                    var ps = pc.GetComponent<PlayerStats>();
                    if (ps == null) continue;

                    currentPlayers.Add(ps);
                }

                int idx = Mathf.Clamp(CurrentTier - 1, 0, 4);
                float critBonus = _stacks * critChancePerStack[idx];
                float manaBonus = _stacks * manaRegenPerStack[idx];

                foreach (var ps in currentPlayers)
                {
                    ps.SetDynamicCritChanceBonus(critBonus);
                    ps.SetDynamicManaRegenBonus(manaBonus);
                }

                foreach (var ps in _buffedPlayers)
                {
                    if (!currentPlayers.Contains(ps) && ps != null)
                    {
                        ps.SetDynamicCritChanceBonus(0f);
                        ps.SetDynamicManaRegenBonus(0f);
                    }
                }

                _buffedPlayers = currentPlayers;
            }

            _auraCoroutine = null;
        }

        private IEnumerator CombatTimer()
        {
            yield return new WaitForSeconds(combatTimeout);

            if (_stacks > 0 && Time.time - _lastKillTime >= combatTimeout)
                ResetAura();

            _combatTimerCoroutine = null;
        }

        private void SyncVisualStacks()
        {
            if (manager != null)
                manager.MarkOfTheAlphaAuraStacksClientRpc(_stacks);
            UpdateAuraVisual();
        }

        public void OnAuraStacksSynced(int stacks)
        {
            _stacks = stacks;
            _currentRadius = stacks > 0 ? baseRadius + stacks * radiusPerStack : 0f;
            UpdateAuraVisual();
        }

        private void UpdateAuraVisual()
        {
            if (auraRing == null) return;

            if (_stacks <= 0)
            {
                auraRing.gameObject.SetActive(false);
                return;
            }

            auraRing.gameObject.SetActive(true);
            DrawRing(_currentRadius);
        }

        private void DrawRing(float radius)
        {
            if (auraRing == null) return;

            auraRing.positionCount = ringSegments + 1;
            float angleStep = 360f / ringSegments;

            for (int i = 0; i <= ringSegments; i++)
            {
                float angle = Mathf.Deg2Rad * angleStep * i;
                float x = Mathf.Sin(angle) * radius;
                float z = Mathf.Cos(angle) * radius;
                auraRing.SetPosition(i, new Vector3(x, ringHeight, z));
            }
        }

        public override object[] GetFormatValues(int tier)
        {
            int idx = Mathf.Clamp(tier - 1, 0, 4);
            return new object[]
            {
                Mathf.RoundToInt(critChancePerStack[idx] * 100f),
                Mathf.RoundToInt(manaRegenPerStack[idx] * 100f),
                baseRadius + radiusPerStack,
                maxStacks[idx],
                (int)combatTimeout
            };
        }
    }
}
