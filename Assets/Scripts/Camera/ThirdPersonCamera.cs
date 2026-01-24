using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Category5.PowerUps;
using Category5.Player;
using System.Collections.Generic;

namespace Category5
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Target Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0, 2f, 0); // Offset from target (e.g. look at head)

        [Header("Orbit Settings")]
        [SerializeField] private float distance = 5f;
        [SerializeField] private float sensitivityX = 1f;
        [SerializeField] private float sensitivityY = 1f;
        [SerializeField] private float minVerticalAngle = -20f;
        [SerializeField] private float maxVerticalAngle = 60f;

        [Header("Collision")]
        [SerializeField] private LayerMask collisionLayers;
        [SerializeField] private float collisionRadius = 0.2f;
        
        [Header("Spectator Mode")]
        [SerializeField] private float spectatorSensitivityMultiplier = 1f;
        
        [Header("Screen Shake")]
        [SerializeField] private float shakeDecay = 5f; // how fast shake fades out
        [SerializeField] private bool usePerlinNoise = true; // perlin noise vs random shake

        private float _rotationX;
        private float _rotationY;
        private InputSystem_Actions _inputActions;
        private Vector2 _lookInput;
        
        // screen shake state
        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeFrequency;
        private float _shakeTimer;
        private float _shakeElapsed;
        private Vector3 _shakeOffset;
        
        // spectator mode state
        private bool _isSpectating = false;
        private Transform _originalTarget; // the player we belong to
        private List<PlayerController> _spectateTargets = new List<PlayerController>();
        private int _currentSpectateIndex = 0;
        
        // event for ui to show spectating info
        public event System.Action<string> OnSpectateTargetChanged;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
            
            // Initialize rotation based on current transform
            Vector3 angles = transform.eulerAngles;
            _rotationX = angles.y;
            _rotationY = angles.x;
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
            _inputActions.Player.Jump.performed += OnCycleSpectateTarget;
        }

        private void OnDisable()
        {
            _inputActions.Player.Jump.performed -= OnCycleSpectateTarget;
            _inputActions.Player.Disable();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            
            // check if our original player is dead and we should be spectating
            bool shouldSpectate = IsOriginalPlayerDead() && !IsGameOver() && !IsInPowerUpSelection();
            
            if (shouldSpectate && !_isSpectating)
            {
                EnterSpectatorMode();
            }
            else if (!shouldSpectate && _isSpectating)
            {
                ExitSpectatorMode();
            }
            
            // update spectate targets list periodically when spectating
            if (_isSpectating)
            {
                UpdateSpectateTargets();
            }
            
            // don't process camera input if game is paused, in power-up selection, or game over
            if (Category5.UI.PauseMenu.GameIsPaused || IsInPowerUpSelection() || IsGameOver())
            {
                // still update camera position to follow target, but don't read input
                HandleCameraPosition();
                return;
            }
            
            // in spectator mode, always allow camera input
            // when alive, normal gameplay applies
            HandleInput();
            HandleCameraPosition();
        }
        
        private void OnCycleSpectateTarget(InputAction.CallbackContext context)
        {
            if (!_isSpectating) return;
            if (Category5.UI.PauseMenu.GameIsPaused) return;
            
            CycleSpectateTarget();
        }
        
        private void EnterSpectatorMode()
        {
            _isSpectating = true;
            _originalTarget = target;
            
            Debug.Log("ThirdPersonCamera: Entering spectator mode");
            
            // find alive players to spectate
            UpdateSpectateTargets();
            
            // switch to first available target
            if (_spectateTargets.Count > 0)
            {
                _currentSpectateIndex = 0;
                SetSpectateTarget(_spectateTargets[0]);
            }
        }
        
        private void ExitSpectatorMode()
        {
            _isSpectating = false;
            
            Debug.Log("ThirdPersonCamera: Exiting spectator mode");
            
            // return to original target
            if (_originalTarget != null)
            {
                target = _originalTarget;
            }
            
            _spectateTargets.Clear();
            OnSpectateTargetChanged?.Invoke(null);
        }
        
        private void UpdateSpectateTargets()
        {
            _spectateTargets.Clear();
            
            if (NetworkManager.Singleton == null) return;
            
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                {
                    var player = client.PlayerObject?.GetComponent<PlayerController>();
                    if (player != null && !player.IsDead.Value && player.transform != _originalTarget)
                    {
                        _spectateTargets.Add(player);
                    }
                }
            }
            
            // if current target died, switch to next available
            if (_spectateTargets.Count > 0)
            {
                // validate current index
                if (_currentSpectateIndex >= _spectateTargets.Count)
                {
                    _currentSpectateIndex = 0;
                }
                
                // check if current target is still valid
                var currentTarget = target?.GetComponent<PlayerController>();
                if (currentTarget == null || currentTarget.IsDead.Value || currentTarget.transform == _originalTarget)
                {
                    SetSpectateTarget(_spectateTargets[_currentSpectateIndex]);
                }
            }
        }
        
        private void CycleSpectateTarget()
        {
            if (_spectateTargets.Count <= 1) return;
            
            _currentSpectateIndex = (_currentSpectateIndex + 1) % _spectateTargets.Count;
            SetSpectateTarget(_spectateTargets[_currentSpectateIndex]);
        }
        
        private void SetSpectateTarget(PlayerController player)
        {
            if (player == null) return;
            
            target = player.transform;
            
            // get player name or id for ui
            string playerName = $"Player {player.OwnerClientId + 1}";
            Debug.Log($"ThirdPersonCamera: Now spectating {playerName}");
            
            OnSpectateTargetChanged?.Invoke(playerName);
        }
        
        // check if power-up selection is active
        private bool IsInPowerUpSelection()
        {
            if (Category5.Items.ItemManager.Instance != null)
            {
                return Category5.Items.ItemManager.Instance.CurrentPhase.Value == Category5.Core.GamePhase.PowerUpSelection;
            }
            return false;
        }
        
        // check if game is over
        private bool IsGameOver()
        {
            if (Category5.Items.ItemManager.Instance != null)
            {
                return Category5.Items.ItemManager.Instance.CurrentPhase.Value == Category5.Core.GamePhase.GameOver ||
                       Category5.Items.ItemManager.Instance.CurrentPhase.Value == Category5.Core.GamePhase.Victory;
            }
            return false;
        }
        
        // check if our original player (the one we belong to) is dead
        private bool IsOriginalPlayerDead()
        {
            var originalPlayer = _originalTarget != null ? _originalTarget : target;
            if (originalPlayer == null) return false;
            var playerController = originalPlayer.GetComponent<PlayerController>();
            return playerController != null && playerController.IsDead.Value;
        }
        
        // public accessor for spectating state
        public bool IsSpectating => _isSpectating;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            _originalTarget = newTarget; // also set as original when first assigned
        }

        private void HandleInput()
        {
            _lookInput = _inputActions.Player.Look.ReadValue<Vector2>();

            _rotationX += _lookInput.x * sensitivityX;
            _rotationY -= _lookInput.y * sensitivityY;
            _rotationY = Mathf.Clamp(_rotationY, minVerticalAngle, maxVerticalAngle);
        }

        private void HandleCameraPosition()
        {
            // update screen shake
            UpdateShake();
            
            Quaternion rotation = Quaternion.Euler(_rotationY, _rotationX, 0);
            Vector3 desiredPosition = target.position + offset - (rotation * Vector3.forward * distance);

            // Simple collision check
            Vector3 direction = desiredPosition - (target.position + offset);
            if (Physics.SphereCast(target.position + offset, collisionRadius, direction.normalized, out RaycastHit hit, distance, collisionLayers))
            {
                // If we hit something, move camera to hit point (plus a little buffer)
                desiredPosition = hit.point + (hit.normal * collisionRadius);
            }

            // apply screen shake offset
            desiredPosition += _shakeOffset;

            transform.rotation = rotation;
            transform.position = desiredPosition;
        }
        
        // =====================================
        // screen shake system
        // =====================================
        
        // trigger screen shake with specified parameters
        public void TriggerShake(float intensity, float duration, float frequency)
        {
            // if new shake is stronger than current, use it
            // otherwise let current shake finish
            if (intensity > _shakeIntensity || _shakeTimer <= 0)
            {
                _shakeIntensity = intensity;
                _shakeDuration = duration;
                _shakeFrequency = frequency;
                _shakeTimer = duration;
                _shakeElapsed = 0f;
            }
        }
        
        // update shake each frame
        private void UpdateShake()
        {
            if (_shakeTimer <= 0)
            {
                _shakeOffset = Vector3.zero;
                return;
            }
            
            _shakeTimer -= Time.unscaledDeltaTime;
            _shakeElapsed += Time.unscaledDeltaTime;
            
            // calculate current intensity with decay
            float currentIntensity = _shakeIntensity * (_shakeTimer / _shakeDuration);
            
            if (usePerlinNoise)
            {
                // perlin noise based shake for more smoother feel
                float noiseX = (Mathf.PerlinNoise(_shakeElapsed * _shakeFrequency, 0f) - 0.5f) * 2f;
                float noiseY = (Mathf.PerlinNoise(0f, _shakeElapsed * _shakeFrequency) - 0.5f) * 2f;
                float noiseZ = (Mathf.PerlinNoise(_shakeElapsed * _shakeFrequency, _shakeElapsed * _shakeFrequency) - 0.5f) * 2f;
                
                _shakeOffset = new Vector3(noiseX, noiseY, noiseZ * 0.5f) * currentIntensity;
            }
            else
            {
                // random shake for more violent impacts
                _shakeOffset = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f),
                    Random.Range(-0.5f, 0.5f)
                ) * currentIntensity;
            }
        }
    }
}
