using System;
using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Enemies;
using Category5.Boss;

namespace Category5.WeakPoints
{
    // a designated region on an enemy or boss that takes bonus damage when struck
    // can be destroyed, respawn on timer or animation trigger, and fire break effects
    //
    // type 1 (ranged): implements idamageable so projectiles find it via GetComponent<IDamageable>
    // type 2 (melee zone): detected by position check in playercombat / abilitymanager
    [RequireComponent(typeof(Collider))]
    public class WeakPoint : NetworkBehaviour, IDamageable
    {
        [Header("identity")]
        [Tooltip("unique id within the parent entity (e.g. 'head', 'left_claw', 'core')")]
        [SerializeField] private string weakPointId; // this is for if an enemy has multiple weak points, so we can identify them by name instead of index

        [Header("type")]
        [SerializeField] private WeakPointType weakPointType = WeakPointType.Ranged;

        [Header("health")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float damageMultiplier = 2f;

        [Header("respawn")]
        [SerializeField] private WeakPointRespawnMode respawnMode = WeakPointRespawnMode.Timer;
        [SerializeField] private float respawnDelay = 5f;

        [Header("activation")]
        [Tooltip("should this weak point be active when the entity spawns")]
        [SerializeField] private bool startActive = true;

        [Header("effects")]
        [SerializeField] private WeakPointBreakEffect[] breakEffects;

        [Header("visuals")]
        [SerializeField] private Color intactColor = Color.cyan;
        [SerializeField] private Color damagedColor = Color.yellow;
        [SerializeField] private Color brokenColor = Color.red;
        [SerializeField] private float damageColorThreshold = 0.5f;

        [Header("vfx")]
        [SerializeField] private GameObject destroyVfxPrefab;
        [SerializeField] private GameObject hitVfxPrefab;

        // networked state
        public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>();
        public NetworkVariable<bool> IsActive = new NetworkVariable<bool>(true);

        // public accessors
        public string WeakPointId => weakPointId;
        public WeakPointType Type => weakPointType;
        public float DamageMultiplier => damageMultiplier;
        public int MaxHealth => maxHealth;
        public float HealthPercent => maxHealth > 0 ? (float)CurrentHealth.Value / maxHealth : 0f;
        public Color IntactColor => intactColor;
        public Color DamagedColor => damagedColor;
        public Color BrokenColor => brokenColor;
        public float DamageColorThreshold => damageColorThreshold;
        public float RespawnDelay => respawnDelay;
        public WeakPointRespawnMode RespawnMode => respawnMode;

        // cached references
        private IWeakPointHost _host;
        private Collider _collider;
        private float _respawnTimer;
        private bool _isBroken;

        // events for external subscribers (audio, ui, class-specific logic)
        public static event Action<WeakPoint, ulong, Vector3> OnWeakPointBroken;
        public static event Action<WeakPoint, int, Vector3> OnWeakPointHit;

        // =====================================
        // lifecycle
        // =====================================

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            if (_collider != null && !_collider.isTrigger)
            {
                // weak point colliders must be triggers so they don't block projectile movement
                _collider.isTrigger = true;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // find the host entity (parent enemy or boss)
            _host = GetComponentInParent<IWeakPointHost>();

            if (_host == null)
            {
                Debug.LogWarning($"[WeakPoint] {gameObject.name}: no IWeakPointHost found on parent. weak point will not function.");
                return;
            }

            if (IsServer)
            {
                CurrentHealth.Value = maxHealth;
                IsActive.Value = startActive;
                _isBroken = false;
                _respawnTimer = 0f;
            }

            // subscribe to network variable changes for visual updates (runs on all clients)
            CurrentHealth.OnValueChanged += OnHealthChanged;
            IsActive.OnValueChanged += OnActiveChanged;

            // set initial visual state
            UpdateVisualState();
        }

        public override void OnNetworkDespawn()
        {
            CurrentHealth.OnValueChanged -= OnHealthChanged;
            IsActive.OnValueChanged -= OnActiveChanged;
        }

        private void Update()
        {
            if (!IsServer) return;
            if (_host == null || _host.IsDead) return;

            // tick respawn timer
            if (_isBroken && respawnMode == WeakPointRespawnMode.Timer)
            {
                _respawnTimer -= Time.deltaTime;
                if (_respawnTimer <= 0f)
                {
                    Respawn();
                }
            }
        }

        // =====================================
        // damage (idamageable for type 1 ranged)
        // =====================================

        // called by projectiles (type 1) — attacker id comes from the parent's LastDamagerClientId
        public void TakeDamage(int damage)
        {
            if (!IsServer) return;
            if (!IsActive.Value) return;
            if (_isBroken) return;

            // find the attacker from the parent entity's kill attribution
            ulong attackerClientId = 0;
            var enemyHost = _host as EnemyBase;
            if (enemyHost != null)
            {
                attackerClientId = enemyHost.LastDamagerClientId;
            }

            ProcessDamage(damage, attackerClientId);
        }

        // called by melee zone check (type 2) or abilities — explicit attacker
        public void TakeDamage(int damage, ulong attackerClientId)
        {
            if (!IsServer) return;
            if (!IsActive.Value) return;
            if (_isBroken) return;

            ProcessDamage(damage, attackerClientId);
        }

        private void ProcessDamage(int rawDamage, ulong attackerClientId)
        {
            // reduce weak point health by raw damage
            CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - rawDamage);

            // also deal multiplied damage to the host entity
            int forwardedDamage = Mathf.RoundToInt(rawDamage * damageMultiplier);
            if (forwardedDamage > 0 && _host != null)
            {
                _host.TakeDamageFromWeakPoint(forwardedDamage, attackerClientId);
            }

            // fire hit event for all clients
            OnWeakPointHit?.Invoke(this, rawDamage, transform.position);

            // check if the weak point broke
            if (CurrentHealth.Value <= 0)
            {
                Break(attackerClientId);
            }
        }

        // =====================================
        // break
        // =====================================

        private void Break(ulong attackerClientId)
        {
            if (_isBroken) return;
            _isBroken = true;

            // set inactive
            IsActive.Value = false;

            // build break context
            var hostMono = _host as MonoBehaviour;
            if (hostMono == null) return;

            var context = WeakPointBreakContext.Create(hostMono, attackerClientId, this, transform.position);

            // apply all break effects
            if (breakEffects != null)
            {
                for (int i = 0; i < breakEffects.Length; i++)
                {
                    if (breakEffects[i] != null)
                    {
                        breakEffects[i].ApplyEffect(context);
                    }
                }
            }

            // notify the host
            _host.OnWeakPointBroken(this, attackerClientId);

            // fire static event
            OnWeakPointBroken?.Invoke(this, attackerClientId, transform.position);

            // start respawn timer if timer mode
            if (respawnMode == WeakPointRespawnMode.Timer)
            {
                _respawnTimer = respawnDelay;
            }
        }

        // =====================================
        // respawn
        // =====================================

        public void Respawn()
        {
            if (!IsServer) return;
            if (!_isBroken) return;

            _isBroken = false;
            CurrentHealth.Value = maxHealth;
            IsActive.Value = true;
            _respawnTimer = 0f;
        }

        // =====================================
        // activation (animation triggered)
        // =====================================

        // call this from animation events or code to activate a weak point
        public void Activate()
        {
            if (!IsServer) return;

            if (_isBroken && respawnMode == WeakPointRespawnMode.AnimationTriggered)
            {
                Respawn();
            }
            else
            {
                IsActive.Value = true;
            }
        }

        // call this from animation events or code to deactivate a weak point
        public void Deactivate()
        {
            if (!IsServer) return;
            IsActive.Value = false;
        }

        // =====================================
        // melee zone check (type 2)
        // =====================================

        // returns true if the given world position is inside this melee zone
        public bool IsInsideZone(Vector3 position)
        {
            if (_collider == null) return false;
            if (!_collider.enabled) return false;
            if (!IsActive.Value || _isBroken) return false;

            return _collider.bounds.Contains(position);
        }

        // =====================================
        // host reset
        // =====================================

        // resets the weak point to full health and active state
        // called by the host during round transitions
        public void ResetWeakPoint()
        {
            if (!IsServer) return;

            _isBroken = false;
            _respawnTimer = 0f;
            CurrentHealth.Value = maxHealth;
            IsActive.Value = startActive;
        }

        // =====================================
        // network variable callbacks
        // =====================================

        private void OnHealthChanged(int oldValue, int newValue)
        {
            UpdateVisualState();
        }

        private void OnActiveChanged(bool oldValue, bool newValue)
        {
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            // enable/disable the collider based on active state
            if (_collider != null)
            {
                _collider.enabled = IsActive.Value && !_isBroken;
            }
        }

        // =====================================
        // gizmos
        // =====================================

        protected virtual void OnDrawGizmosSelected()
        {
            if (_collider == null) _collider = GetComponent<Collider>();
            if (_collider == null) return;

            Color gizmoColor = IsActive.Value ? intactColor : brokenColor;
            gizmoColor.a = 0.3f;
            Gizmos.color = gizmoColor;

            if (_collider is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
            }
            else if (_collider is BoxCollider box)
            {
                Gizmos.matrix = Matrix4x4.TRS(
                    transform.TransformPoint(box.center),
                    transform.rotation,
                    Vector3.Scale(transform.lossyScale, box.size));
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            }
            else if (_collider is CapsuleCollider capsule)
            {
                // approximate with a sphere for gizmo
                Gizmos.DrawWireSphere(transform.TransformPoint(capsule.center), capsule.radius * transform.lossyScale.x);
            }
        }
    }
}
