using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Category5;

namespace Category5.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float jumpHeight = 3f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float rotationSpeed = 15f;

        [Header("Ground Check")]
        [SerializeField] private float groundCheckRadius = 0.2f;
        [SerializeField] private Vector3 groundCheckOffset = new Vector3(0, 0.1f, 0);
        [SerializeField] private LayerMask groundLayers = 1; // Default layer

        private CharacterController _controller;
        private InputSystem_Actions _inputActions;
        private Vector2 _moveInput;
        private Vector3 _velocity;
        private bool _isGrounded;
        private bool _isOffline = false;
        
        // Jump Buffering
        private float _jumpBufferTime = 0.2f;
        private float _jumpBufferCounter;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _inputActions = new InputSystem_Actions();
        }

        private void Start()
        {
            // If NetworkManager is missing or not running, we are in offline mode
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                _isOffline = true;
                Debug.Log("PlayerController: Offline mode detected. Enabling local control.");
                
                var camera = FindFirstObjectByType<Category5.ThirdPersonCamera>();
                if (camera != null)
                {
                    camera.SetTarget(transform);
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            _isOffline = false; // We are definitely networked now
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            // Assign camera target
            var camera = FindFirstObjectByType<Category5.ThirdPersonCamera>();
            if (camera != null)
            {
                camera.SetTarget(transform);
            }
        }

        private void OnEnable()
        {
            if (_inputActions != null)
            {
                _inputActions.Player.Enable();
                _inputActions.Player.Jump.performed += OnJump;
            }
        }

        private void OnDisable()
        {
            if (_inputActions != null)
            {
                _inputActions.Player.Jump.performed -= OnJump;
                _inputActions.Player.Disable();
            }
        }

        private void Update()
        {
            if (!IsOwner && !_isOffline) return;

            HandleMovement();
            HandleGravity();
        }

        private void HandleMovement()
        {
            _moveInput = _inputActions.Player.Move.ReadValue<Vector2>();
            
            // Camera-relative movement
            Vector3 move = Vector3.zero;
            if (Camera.main != null)
            {
                Vector3 cameraForward = Camera.main.transform.forward;
                Vector3 cameraRight = Camera.main.transform.right;

                cameraForward.y = 0;
                cameraRight.y = 0;
                cameraForward.Normalize();
                cameraRight.Normalize();

                move = (cameraForward * _moveInput.y + cameraRight * _moveInput.x);
            }
            else
            {
                // fallback to world space if no camera found
                move = new Vector3(_moveInput.x, 0, _moveInput.y);
            }
            
            if (move.magnitude > 1f) move.Normalize();

            if (move != Vector3.zero)
            {
                // rotate towards move direction
                Quaternion targetRotation = Quaternion.LookRotation(move);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                
                _controller.Move(move * moveSpeed * Time.deltaTime);
            }
        }

        private void HandleGravity()
        {
            // Custom ground check is more reliable than CharacterController.isGrounded
            _isGrounded = Physics.CheckSphere(transform.position + groundCheckOffset, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);

            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f; // Small downward force to keep grounded
            }

            // Process Jump Buffer
            if (_jumpBufferCounter > 0)
            {
                _jumpBufferCounter -= Time.deltaTime;
                
                if (_isGrounded)
                {
                    // v = sqrt(h * -2 * g)
                    _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    _jumpBufferCounter = 0;
                }
            }

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            // Instead of jumping immediately, we buffer the input
            _jumpBufferCounter = _jumpBufferTime;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position + groundCheckOffset, groundCheckRadius);
        }
    }
}
