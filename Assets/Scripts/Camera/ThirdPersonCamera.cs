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

        private float _rotationX;
        private float _rotationY;
        private InputSystem_Actions _inputActions;
        private Vector2 _lookInput;
        
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
            return PowerUpManager.Instance != null && 
                   PowerUpManager.Instance.CurrentPhase.Value == GamePhase.PowerUpSelection;
        }
        
        // check if game is over
        private bool IsGameOver()
        {
            return PowerUpManager.Instance != null && 
                   PowerUpManager.Instance.CurrentPhase.Value == GamePhase.GameOver;
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
            Quaternion rotation = Quaternion.Euler(_rotationY, _rotationX, 0);
            Vector3 desiredPosition = target.position + offset - (rotation * Vector3.forward * distance);

            // Simple collision check
            Vector3 direction = desiredPosition - (target.position + offset);
            if (Physics.SphereCast(target.position + offset, collisionRadius, direction.normalized, out RaycastHit hit, distance, collisionLayers))
            {
                // If we hit something, move camera to hit point (plus a little buffer)
                desiredPosition = hit.point + (hit.normal * collisionRadius);
            }

            transform.rotation = rotation;
            transform.position = desiredPosition;
        }
    }
}
