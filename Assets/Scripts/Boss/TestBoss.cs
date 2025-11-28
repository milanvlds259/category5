using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Category5.Core;

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

        [Header("attacks - assign attack data assets here")]
        [Tooltip("list of available attacks - drag BossAttackData assets here")]
        [SerializeField] private List<BossAttackData> availableAttacks = new List<BossAttackData>();
        
        [Header("attack debugging")]
        [SerializeField] private bool showAttackGizmos = true;
        [SerializeField] private Color gizmoColor = Color.red;
        
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
        
        // telegraph visual instance
        private GameObject _telegraphInstance;

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
            if (!IsServer && _currentAttackIndex >= 0)
            {
                _currentAttack = availableAttacks[_currentAttackIndex];
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            currentState.OnValueChanged -= OnStateChanged;
            CleanupTelegraph();
        }

        // =====================================
        // attack selection
        // =====================================
        
        protected override void SelectNextAttack()
        {
            if (availableAttacks.Count == 0)
            {
                Debug.LogWarning("TestBoss: No attacks configured! Add BossAttackData assets to availableAttacks list.");
                return;
            }
            
            _currentAttack = ChooseWeightedAttack();
            _currentAttackIndex = availableAttacks.IndexOf(_currentAttack);
            
            if (_currentAttack == null)
            {
                // fallback to first attack if none valid
                _currentAttack = availableAttacks[0];
                _currentAttackIndex = 0;
            }
            
            Debug.Log($"TestBoss: Selected attack '{_currentAttack.attackName}'");
            
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
            
            // sync attack selection to clients
            SyncAttackSelectionClientRpc(_currentAttackIndex);
        }
        
        [ClientRpc]
        private void SyncAttackSelectionClientRpc(int attackIndex)
        {
            if (IsServer) return; // server already has the attack
            
            if (attackIndex >= 0 && attackIndex < availableAttacks.Count)
            {
                _currentAttack = availableAttacks[attackIndex];
                _currentAttackIndex = attackIndex;
                currentAttackType = _currentAttack.attackType;
                
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
            
            foreach (var attack in availableAttacks)
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
            if (_currentAttack.telegraphPrefab == null) return;
            
            Vector3 spawnPos = GetTelegraphPosition();
            _telegraphInstance = Instantiate(_currentAttack.telegraphPrefab, spawnPos, Quaternion.identity);
            
            // scale telegraph based on damage radius
            float scale = _currentAttack.damageRadius * 2f;
            _telegraphInstance.transform.localScale = new Vector3(scale, 1f, scale);
            
            // apply telegraph color if there's a renderer
            var renderer = _telegraphInstance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = _currentAttack.telegraphColor;
            }
            
            // fire event for artists
            OnAttackTelegraphStart?.Invoke(_currentAttack, spawnPos);
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
        }
        
        protected override void OnTelegraphUpdate()
        {
            base.OnTelegraphUpdate();
            
            // update telegraph position to follow boss
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
            
            Debug.Log($"TestBoss: Executing attack '{_currentAttack.attackName}'");
            
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
            
            // for non-lunge non-sweep attacks, do damage check immediately
            if (!_currentAttack.hasLunge && !_currentAttack.isSweep)
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
            
            Collider[] hits = Physics.OverlapSphere(attackCenter, _currentAttack.damageRadius);
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
            
            // use capsule cast for beam width
            Collider[] hits = Physics.OverlapCapsule(
                beamStart, 
                beamEnd, 
                _currentAttack.sweepWidth / 2f
            );
            
            ProcessHits(hits, transform.position);
        }
        
        private void ProcessHits(Collider[] hits, Vector3 attackCenter)
        {
            foreach (var hit in hits)
            {
                // skip if already hit this attack
                if (_hitTargetsThisAttack.Contains(hit.gameObject)) continue;
                
                if (hit.TryGetComponent<IDamageable>(out var target) && hit.gameObject != gameObject)
                {
                    _hitTargetsThisAttack.Add(hit.gameObject);
                    target.TakeDamage(_currentAttack.damage);
                    
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
        
        private void OnDrawGizmosSelected()
        {
            if (!showAttackGizmos) return;
            
            // show current attack range
            if (_currentAttack != null)
            {
                Gizmos.color = gizmoColor;
                
                Vector3 attackCenter = transform.position + transform.TransformDirection(_currentAttack.damageOffset);
                Gizmos.DrawWireSphere(attackCenter, _currentAttack.damageRadius);
            }
            
            // show all configured attacks in edit mode using their assigned gizmo colors
            if (!Application.isPlaying && availableAttacks != null)
            {
                for (int i = 0; i < availableAttacks.Count; i++)
                {
                    var attack = availableAttacks[i];
                    if (attack == null) continue;
                    
                    // use the attack's custom gizmo color
                    Gizmos.color = attack.gizmoColor;
                    
                    if (attack.isSweep)
                    {
                        // draw sweep arc
                        DrawSweepGizmo(attack);
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
