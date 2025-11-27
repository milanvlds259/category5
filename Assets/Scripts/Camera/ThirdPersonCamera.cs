using UnityEngine;
using UnityEngine.InputSystem;
using Category5.PowerUps;

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

        private float _rotationX;
        private float _rotationY;
        private InputSystem_Actions _inputActions;
        private Vector2 _lookInput;

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
        }

        private void OnDisable()
        {
            _inputActions.Player.Disable();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            
            // don't process camera input if game is paused or in power-up selection
            if (Category5.UI.PauseMenu.GameIsPaused || IsInPowerUpSelection())
            {
                // still update camera position to follow target, but don't read input
                HandleCameraPosition();
                return;
            }

            HandleInput();
            HandleCameraPosition();
        }
        
        // check if power-up selection is active
        private bool IsInPowerUpSelection()
        {
            return PowerUpManager.Instance != null && 
                   PowerUpManager.Instance.CurrentPhase.Value == GamePhase.PowerUpSelection;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
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
