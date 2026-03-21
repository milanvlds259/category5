using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Category5.Core;
using Category5.Player;

namespace Category5.Boss
{
    public class TestBoss : BossBase
    {
        [Header("visuals")]
        [SerializeField] private Renderer meshRenderer;
        [SerializeField] private Color idleColor = Color.gray;
        [SerializeField] private Color telegraphColor = Color.yellow;
        [SerializeField] private Color attackColor = Color.red;
        [SerializeField] private Color cooldownColor = Color.blue;

        // attacks come from bossData.availableAttacks — no separate list needed here
        private IReadOnlyList<BossAttackData> Attacks => bossData != null ? bossData.availableAttacks : null;

        [Header("attack debugging")]
        [SerializeField] private bool showAttackGizmos = true;
        [SerializeField] private Color gizmoColor = Color.red;
        
        [Header("target filtering")]
        [Tooltip("layers that boss attacks can damage - should only include Player layer")]
        [SerializeField] private LayerMask targetLayers = ~0; // default to all, set to Player layer in inspector
        
        // current attack state
        private BossAttackData _currentAttack;
        private int _currentAttackIndex = -1;
        
        // lunge state
        private bool _isLunging = false;
        private Vector3 _lungeDirection;
        private float _lungeDistanceTraveled;
        
        // sweep state
        private bool _isSweeping = false;
        private float _sweepProgress = 0f;
        private float _sweepStartAngle;
        
        // track which targets have been hit this attack to prevent multi-hits
        private HashSet<GameObject> _hitTargetsThisAttack = new HashSet<GameObject>();
        
        // position locked at telegraph start for projectile attacks so the indicator stays static
        private Vector3 _lockedProjectileTargetPos;
        
        // telegraph visual instance
        private GameObject _telegraphInstance;
        // procedural ground indicator, always spawned regardless of prefab assignment
        private BossTelegraphIndicator _telegraphIndicator;

        // =====================================
        // events for artists/designers to hook into
        // =====================================
        public static event System.Action<BossAttackData, Vector3> OnAttackTelegraphStart;
        public static event System.Action<BossAttackData, Vector3> OnAttackExecute;
        public static event System.Action<BossAttackData> OnAttackEnd;
        public static event System.Action<BossAttackData, Vector3, GameObject> OnAttackHitTarget;

        private void Awake()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<Renderer>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            currentState.OnValueChanged += OnStateChanged;
            
            // sync current attack index to clients
            if (!IsServer && _currentAttackIndex >= 0 && Attacks != null && _currentAttackIndex < Attacks.Count)
            {
                _currentAttack = Attacks[_currentAttackIndex];
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            currentState.OnValueChanged -= OnStateChanged;
            CleanupTelegraph();
        }

        // copies bossData fields into runtime fields and picks up testboss-specific visuals
        protected override void InitializeFromData()
        {
            base.InitializeFromData();

            if (bossData == null) return;

            // apply boss color to mesh if one is set
            if (bossData.bossColor != Color.white && meshRenderer != null)
                meshRenderer.material.color = bossData.bossColor;
        }

        // returns the index of an attack in bossData.availableAttacks
        private int IndexOfAttack(BossAttackData attack)
        {
            if (bossData == null || bossData.availableAttacks == null) return -1;
            for (int i = 0; i < bossData.availableAttacks.Length; i++)
            {
                if (bossData.availableAttacks[i] == attack) return i;
            }
            return -1;
        }

        // =====================================
        // attack selection
        // =====================================
        
        protected override void SelectNextAttack()
        {
            if (Attacks == null || Attacks.Count == 0)
            {
                Debug.LogWarning("TestBoss: no attacks configured — assign BossAttackData assets to bossData.availableAttacks");
                return;
            }
            
            _currentAttack = ChooseWeightedAttack();
            _currentAttackIndex = _currentAttack != null ? IndexOfAttack(_currentAttack) : -1;
            
            if (_currentAttack == null)
            {
                // fallback to first attack if none valid
                _currentAttack = Attacks[0];
                _currentAttackIndex = 0;
            }
            
            // Debug.Log($"TestBoss: Selected attack '{_currentAttack.attackName}'");
            
            // override telegraph duration based on attack
            stateTimer = _currentAttack.telegraphDuration;
            
            // set attack type for vfx hooks
            currentAttackType = _currentAttack.attackType;
            
            // prepare lunge direction
            _isLunging = false;
            _lungeDistanceTraveled = 0f;
            _lungeDirection = GetDirectionToTarget();
            
            // prepare sweep
            _isSweeping = false;
            _sweepProgress = 0f;
            _sweepStartAngle = transform.eulerAngles.y - (_currentAttack.sweepAngle / 2f);
            
            // spawn telegraph visual
            SpawnTelegraphVisual();
            
            // lock target position now for projectile telegraph indicator (static circle on ground)
            if (_currentAttack.hasProjectile && currentTarget != null)
                _lockedProjectileTargetPos = currentTarget.position;
            
            // sync attack selection to clients
            SyncAttackSelectionClientRpc(_currentAttackIndex, _lockedProjectileTargetPos);
        }
        
        [ClientRpc]
        private void SyncAttackSelectionClientRpc(int attackIndex, Vector3 lockedTargetPos)
        {
            if (IsServer) return; // server already has the attack
            
            if (attackIndex >= 0 && Attacks != null && attackIndex < Attacks.Count)
            {
                _currentAttack = Attacks[attackIndex];
                _currentAttackIndex = attackIndex;
                currentAttackType = _currentAttack.attackType;
                _lockedProjectileTargetPos = lockedTargetPos;
                
                // spawn telegraph on clients too
                SpawnTelegraphVisual();
            }
        }
        
        private BossAttackData ChooseWeightedAttack()
        {
            float healthPercent = (float)CurrentHealth.Value / MaxHealth;
            float distanceToTarget = GetDistanceToTarget();
            
            // build list of valid attacks and their weights
            List<BossAttackData> validAttacks = new List<BossAttackData>();
            List<float> weights = new List<float>();
            float totalWeight = 0f;
            
            foreach (var attack in Attacks)
            {
                // check health threshold
                if (healthPercent > attack.healthThreshold) continue;
                
                // check range
                if (distanceToTarget < attack.minRange || distanceToTarget > attack.maxRange) continue;
                
                validAttacks.Add(attack);
                weights.Add(attack.selectionWeight);
                totalWeight += attack.selectionWeight;
            }
            
            if (validAttacks.Count == 0) return null;
            
            // weighted random selection
            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;
            
            for (int i = 0; i < validAttacks.Count; i++)
            {
                currentWeight += weights[i];
                if (randomValue <= currentWeight)
                {
                    return validAttacks[i];
                }
            }
            
            return validAttacks[validAttacks.Count - 1];
        }

        // =====================================
        // telegraph phase
        // =====================================
        
        private void SpawnTelegraphVisual()
        {
            CleanupTelegraph();

            if (_currentAttack == null) return;

            // always spawn the procedural indicator so players can see the telegraph
            // use collider bottom for correct ground-level Y, falling back to pivot if none found
            Collider bossCol = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
            float groundY = bossCol != null ? bossCol.bounds.min.y + 0.02f : transform.position.y + 0.02f;

            if (_currentAttack.hasProjectile)
            {
                // static circle at the locked target position — doesn't follow the player
                // this gives the player a chance to dodge out of where they were standing
                Vector3 indicatorPos = new Vector3(_lockedProjectileTargetPos.x, groundY, _lockedProjectileTargetPos.z);
                _telegraphIndicator = BossTelegraphIndicator.Create(
                    BossTelegraphIndicator.IndicatorShape.Circle,
                    1.5f,
                    0f,
                    _currentAttack.telegraphColor,
                    _currentAttack.telegraphDuration,
                    indicatorPos);
                // no SetFollowTarget — indicator stays locked to where the player was
                
                // still allow an optional charging-up vfx at the boss's spawn point
                if (_currentAttack.telegraphPrefab != null)
                {
                    Vector3 spawnPos = transform.position + transform.TransformDirection(_currentAttack.projectileSpawnOffset);
                    _telegraphInstance = Instantiate(_currentAttack.telegraphPrefab, spawnPos, transform.rotation);
                }
                
                OnAttackTelegraphStart?.Invoke(_currentAttack, _lockedProjectileTargetPos);
            }
            else if (_currentAttack.isSweep)
            {
                // arc fan originates from the boss's position, centered on its forward direction
                Vector3 indicatorPos = new Vector3(transform.position.x, groundY, transform.position.z);
                _telegraphIndicator = BossTelegraphIndicator.Create(
                    BossTelegraphIndicator.IndicatorShape.Arc,
                    _currentAttack.sweepLength,
                    _currentAttack.sweepAngle,
                    _currentAttack.telegraphColor,
                    _currentAttack.telegraphDuration,
                    indicatorPos);
                // sweep originates from boss center - no XZ offset
                _telegraphIndicator.SetFollowTarget(transform, Vector3.zero);
            }
            else
            {
                // disc centered at the damage offset position, XZ only (always flat on ground)
                Vector3 attackCenter = GetTelegraphPosition();
                Vector3 indicatorPos = new Vector3(attackCenter.x, groundY, attackCenter.z);
                _telegraphIndicator = BossTelegraphIndicator.Create(
                    BossTelegraphIndicator.IndicatorShape.Circle,
                    _currentAttack.damageRadius,
                    0f,
                    _currentAttack.telegraphColor,
                    _currentAttack.telegraphDuration,
                    indicatorPos);
                // circle follows boss with the attack's XZ offset in local space
                _telegraphIndicator.SetFollowTarget(transform, _currentAttack.damageOffset);
            }

            // orient flat along world XZ but rotated to match boss facing
            _telegraphIndicator.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            // optionally also spawn our own vfx prefab on top
            if (_currentAttack.telegraphPrefab != null)
            {
                Vector3 spawnPos = GetTelegraphPosition();
                _telegraphInstance = Instantiate(_currentAttack.telegraphPrefab, spawnPos, Quaternion.identity);

                float scale = _currentAttack.damageRadius * 2f;
                _telegraphInstance.transform.localScale = new Vector3(scale, 1f, scale);

                var renderer = _telegraphInstance.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = _currentAttack.telegraphColor;
            }

            // fire event for summer
            OnAttackTelegraphStart?.Invoke(_currentAttack, GetTelegraphPosition());
        }
        
        private Vector3 GetTelegraphPosition()
        {
            // telegraph at damage offset position (set offset to 0,0,0 for centered attacks)
            return transform.position + transform.TransformDirection(_currentAttack.damageOffset);
        }
        
        private void CleanupTelegraph()
        {
            if (_telegraphInstance != null)
            {
                Destroy(_telegraphInstance);
                _telegraphInstance = null;
            }

            if (_telegraphIndicator != null)
            {
                Destroy(_telegraphIndicator.gameObject);
                _telegraphIndicator = null;
            }
        }
        
        protected override void OnTelegraphUpdate()
        {
            base.OnTelegraphUpdate();

            // procedural indicator self-tracks the boss via SetFollowTarget
            // only the optional prefab instance needs manual tracking here
            if (_telegraphInstance != null)
            {
                _telegraphInstance.transform.position = GetTelegraphPosition();
                _telegraphInstance.transform.rotation = transform.rotation;
            }
        }

        // =====================================
        // attack execution
        // =====================================
        
        protected override void ExecuteAttack()
        {
            if (_currentAttack == null)
            {
                Debug.LogWarning("TestBoss: No current attack to execute!");
                stateTimer = 1.0f;
                return;
            }
            
            // Debug.Log($"TestBoss: Executing attack '{_currentAttack.attackName}'");
            
            // set attack duration
            stateTimer = _currentAttack.attackDuration;
            
            // clear hit targets for new attack
            _hitTargetsThisAttack.Clear();
            
            // cleanup telegraph
            CleanupTelegraph();
            
            // start attack behavior based on type
            if (_currentAttack.hasLunge)
            {
                _isLunging = true;
                _lungeDistanceTraveled = 0f;
            }
            
            if (_currentAttack.isSweep)
            {
                _isSweeping = true;
                _sweepProgress = 0f;
            }
            
            // projectile attacks: fire projectiles on the server
            if (_currentAttack.hasProjectile)
            {
                SpawnBossProjectiles();
            }
            
            // for non-lunge non-sweep non-projectile attacks, do damage check immediately
            if (!_currentAttack.hasLunge && !_currentAttack.isSweep && !_currentAttack.hasProjectile)
            {
                CheckMeleeHits();
            }
            
            // fire event for artists
            OnAttackExecute?.Invoke(_currentAttack, transform.position);
            
            // sync attack execution to clients for vfx
            ExecuteAttackClientRpc();
        }
        
        [ClientRpc]
        private void ExecuteAttackClientRpc()
        {
            if (IsServer) return;
            
            CleanupTelegraph();
            OnAttackExecute?.Invoke(_currentAttack, transform.position);
        }
        
        protected override void OnAttackUpdate()
        {
            if (_currentAttack == null) return;
            
            // handle lunge movement
            if (_isLunging && _lungeDistanceTraveled < _currentAttack.lungeDistance)
            {
                float frameDistance = _currentAttack.lungeSpeed * Time.deltaTime;
                ApplyMovement(_lungeDirection * (_currentAttack.lungeSpeed / moveSpeed));
                _lungeDistanceTraveled += frameDistance;
                
                // check for hits during lunge
                CheckMeleeHits();
            }
            else if (_isLunging)
            {
                _isLunging = false;
                CheckMeleeHits();
            }
            
            // handle sweep attack
            if (_isSweeping)
            {
                _sweepProgress += Time.deltaTime / _currentAttack.attackDuration;
                
                if (_sweepProgress <= 1f)
                {
                    CheckSweepHits();
                }
                else
                {
                    _isSweeping = false;
                }
            }
        }
        
        // =====================================
        // projectile spawning
        // =====================================
        
        private void SpawnBossProjectiles()
        {
            if (!IsServer) return;
            if (_currentAttack == null) return;
            
            if (_currentAttack.projectilePrefab == null)
            {
                Debug.LogError($"BossProjectile: '{_currentAttack.attackName}' has hasProjectile=true but no projectilePrefab assigned!");
                return;
            }
            
            int count = _currentAttack.projectileCount;
            float totalSpread = count > 1 ? _currentAttack.projectileSpreadAngle : 0f;
            
            Vector3 spawnPos = transform.position + transform.TransformDirection(_currentAttack.projectileSpawnOffset);
            
            for (int i = 0; i < count; i++)
            {
                // calculate angle offset for each projectile in the fan
                float angleOffset = count > 1
                    ? Mathf.Lerp(-totalSpread / 2f, totalSpread / 2f, (float)i / (count - 1))
                    : 0f;
                
                // aim toward current target, or just the boss's forward if no target
                Vector3 baseDir = currentTarget != null
                    ? (currentTarget.position - spawnPos).normalized
                    : transform.forward;
                
                // apply the fan spread angle on the horizontal plane
                Vector3 dir = Quaternion.AngleAxis(angleOffset, Vector3.up) * baseDir;
                dir.Normalize();
                
                GameObject proj = Instantiate(
                    _currentAttack.projectilePrefab,
                    spawnPos,
                    Quaternion.LookRotation(dir));
                
                var bp = proj.GetComponent<BossProjectile>();
                if (bp == null)
                {
                    Debug.LogError($"BossProjectile: prefab '{_currentAttack.projectilePrefab.name}' is missing a BossProjectile component!");
                    Destroy(proj);
                    continue;
                }
                
                bp.Initialize(_currentAttack.projectileSpeed, _currentAttack.damage, _currentAttack.projectileLifetime);
                proj.GetComponent<NetworkObject>().Spawn();
            }
        }
        
        protected override void StartCooldown()
        {
            base.StartCooldown();
            
            // use attack-specific cooldown if available
            if (_currentAttack != null)
            {
                stateTimer = _currentAttack.cooldownDuration;
            }
            
            // fire end event
            OnAttackEnd?.Invoke(_currentAttack);
        }

        // =====================================
        // damage checking
        // =====================================
        
        private void CheckMeleeHits()
        {
            if (!IsServer) return;
            if (_currentAttack == null) return;
            
            Vector3 attackCenter = transform.position + transform.TransformDirection(_currentAttack.damageOffset);
            
            // use layer mask to only detect players, not enemies or other objects
            Collider[] hits = Physics.OverlapSphere(attackCenter, _currentAttack.damageRadius, targetLayers);
            ProcessHits(hits, attackCenter);
        }
        
        private void CheckSweepHits()
        {
            if (!IsServer) return;
            if (_currentAttack == null) return;
            
            // calculate current sweep angle
            float currentAngle = _sweepStartAngle + (_currentAttack.sweepAngle * _sweepProgress);
            Vector3 sweepDirection = Quaternion.Euler(0f, currentAngle, 0f) * Vector3.forward;
            
            // check for targets in the sweep beam (apply offset for height adjustment)
            Vector3 beamStart = transform.position + _currentAttack.sweepOffset;
            Vector3 beamEnd = beamStart + sweepDirection * _currentAttack.sweepLength;
            
            // use capsule cast for beam width, filtered by layer mask to only detect players
            Collider[] hits = Physics.OverlapCapsule(
                beamStart, 
                beamEnd, 
                _currentAttack.sweepWidth / 2f,
                targetLayers
            );
            
            ProcessHits(hits, transform.position);
        }
        
        private void ProcessHits(Collider[] hits, Vector3 attackCenter)
        {
            foreach (var hit in hits)
            {
                // skip if already hit this attack
                if (_hitTargetsThisAttack.Contains(hit.gameObject)) continue;
                
                // only damage players, not enemies or other objects
                // this is a safety check in addition to the layer mask filtering
                if (hit.TryGetComponent<PlayerController>(out var player) && hit.gameObject != gameObject)
                {
                    // skip dead players
                    if (player.IsDead.Value) continue;
                    
                    _hitTargetsThisAttack.Add(hit.gameObject);
                    player.TakeDamage(_currentAttack.damage);
                    
                    // trigger feedback
                    TriggerBossHitFeedback(hit.transform.position, _currentAttack.isHeavyAttack);
                    
                    // fire event for artists
                    OnAttackHitTarget?.Invoke(_currentAttack, hit.transform.position, hit.gameObject);
                }
            }
        }

        // =====================================
        // visual state
        // =====================================
        
        private void OnStateChanged(BossState oldState, BossState newState)
        {
            UpdateVisuals(newState);
        }

        private void UpdateVisuals(BossState state)
        {
            if (meshRenderer == null) return;

            switch (state)
            {
                case BossState.Idle:
                    meshRenderer.material.color = idleColor;
                    break;
                case BossState.Telegraph:
                    meshRenderer.material.color = telegraphColor;
                    break;
                case BossState.Attack:
                    meshRenderer.material.color = attackColor;
                    break;
                case BossState.Cooldown:
                    meshRenderer.material.color = cooldownColor;
                    break;
            }
        }

        // =====================================
        // editor gizmos
        // =====================================
        
        protected override void OnDrawGizmosSelected()
        {
            // draw ground check sphere from base class
            base.OnDrawGizmosSelected();

            if (!showAttackGizmos) return;
            
            // show current attack range
            if (_currentAttack != null)
            {
                Gizmos.color = gizmoColor;
                
                Vector3 attackCenter = transform.position + transform.TransformDirection(_currentAttack.damageOffset);
                Gizmos.DrawWireSphere(attackCenter, _currentAttack.damageRadius);
            }
            
            // show all configured attacks in edit mode using their assigned gizmo colors
            if (!Application.isPlaying && bossData != null && bossData.availableAttacks != null)
            {
                for (int i = 0; i < bossData.availableAttacks.Length; i++)
                {
                    var attack = bossData.availableAttacks[i];
                    if (attack == null) continue;
                    
                    // use the attack's custom gizmo color
                    Gizmos.color = attack.gizmoColor;
                    
                    if (attack.isSweep)
                    {
                        // draw sweep arc
                        DrawSweepGizmo(attack);
                    }
                    else if (attack.hasProjectile)
                    {
                        // draw a ray from the spawn offset in the boss's forward direction
                        Vector3 spawnPos = transform.position + transform.TransformDirection(attack.projectileSpawnOffset);
                        Gizmos.DrawRay(spawnPos, transform.forward * 8f);
                        Gizmos.DrawWireSphere(spawnPos, 0.2f);
                        
                        // draw extra rays for spread fan if count > 1
                        if (attack.projectileCount > 1)
                        {
                            float halfSpread = attack.projectileSpreadAngle / 2f;
                            Vector3 leftDir = Quaternion.AngleAxis(-halfSpread, Vector3.up) * transform.forward;
                            Vector3 rightDir = Quaternion.AngleAxis(halfSpread, Vector3.up) * transform.forward;
                            Gizmos.DrawRay(spawnPos, leftDir * 8f);
                            Gizmos.DrawRay(spawnPos, rightDir * 8f);
                        }
                    }
                    else
                    {
                        // draw damage sphere at offset position
                        Vector3 attackCenter = transform.position + transform.TransformDirection(attack.damageOffset);
                        Gizmos.DrawWireSphere(attackCenter, attack.damageRadius);
                    }
                    
                    // draw label with attack name
#if UNITY_EDITOR
                    Vector3 labelPos = transform.position + transform.TransformDirection(attack.damageOffset) + Vector3.up * (attack.damageRadius + 0.5f);
                    UnityEditor.Handles.Label(labelPos, attack.attackName, new GUIStyle { normal = { textColor = attack.gizmoColor }, fontStyle = FontStyle.Bold });
#endif
                }
            }
        }
        
        private void DrawSweepGizmo(BossAttackData attack)
        {
            // draw arc for sweep attacks
            float startAngle = -attack.sweepAngle / 2f;
            float endAngle = attack.sweepAngle / 2f;
            int segments = 20;
            
            // apply sweep offset for accurate visualization
            Vector3 sweepOrigin = transform.position + attack.sweepOffset;
            
            Vector3 lastPoint = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(startAngle, endAngle, t);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * transform.forward;
                Vector3 point = sweepOrigin + direction * attack.sweepLength;
                
                if (i > 0)
                {
                    Gizmos.DrawLine(lastPoint, point);
                }
                
                // draw line from center to edge
                if (i == 0 || i == segments)
                {
                    Gizmos.DrawLine(sweepOrigin, point);
                }
                
                lastPoint = point;
            }
        }
    }
}
