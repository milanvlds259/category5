using UnityEngine;
using UnityEngine.InputSystem;
using Category5.Audio;

namespace Category5.Player.WindRiding
{
    // core wind riding mechanic - attached to the player prefab
    // drives the player along a wind tunnel spline with lateral sway and speed lean
    // not a NetworkBehaviour: riding state syncs to remotes via the IsWindRiding animator param
    // through OwnerPlayerNetworkAnimator (owner authoritative)
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(CharacterController))]
    public class WindRiderController : MonoBehaviour
    {
        [Header("Riding Settings")]
        [SerializeField] private WindRideSettings settings = new WindRideSettings();

        // public state
        public bool IsWindRiding { get; set; }

        // the tunnel we are currently riding
        public WindTunnel ActiveTunnel => _activeTunnel;

        public bool IsRidingForward => _ridingForward;

        public bool IsRidingTunnel => _currentMode == RidingMode.Tunnel;
        public bool IsRidingCloud => _currentMode == RidingMode.Cloud;
        public bool IsRidingGlide => _currentMode == RidingMode.Gliding;

        // current normalized progress along the spline (0-1)
        public float Progress => _t;

        // current speed in m/s
        public float CurrentSpeed => _currentSpeed;

        // current sway offset normalized (-1 to 1)
        public float NormalizedSway
        {
            get
            {
                if (_currentMode == RidingMode.Cloud)
                {
                    // For cloud surfing, use lateral velocity as the sway indicator
                    return settings.steeringResponsiveness > 0f 
                        ? Mathf.Clamp(_swayVelocity / settings.steeringResponsiveness, -1f, 1f) 
                        : 0f;
                }
                return settings.maxSwayOffset > 0f ? _currentSway / settings.maxSwayOffset : 0f;
            }
        }

        // cached references
        private PlayerController _playerController;
        private CharacterController _characterController;
        private PlayerModelManager _modelManager;
        private InputSystem_Actions _inputActions;

        // riding state
        public enum RidingMode { None, Tunnel, Cloud, Gliding }
        private RidingMode _currentMode = RidingMode.None;
        private WindTunnel _activeTunnel;
        private float _t;
        private bool _ridingForward;
        private float _currentSpeed;
        private float _currentSway;
        private float _targetSway;
        private float _swayVelocity;
        private Vector3 _ridingVelocity;
        private ThirdPersonCamera _camera;

        // expose settings for camera or other systems that need to read them
        public WindRideSettings Settings => settings;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _characterController = GetComponent<CharacterController>();
            _modelManager = GetComponent<PlayerModelManager>();
            _camera = FindFirstObjectByType<ThirdPersonCamera>();
        }

        private void OnEnable()
        {
            if (_inputActions == null)
                _inputActions = new InputSystem_Actions();
            _inputActions.Player.Enable();
        }

        private void OnDisable()
        {
            // if we were riding when disabled, force exit cleanly
            if (IsWindRiding)
                ForceEndRiding();

            _inputActions?.Player.Disable();
        }

        // called by WindLaunchPad when the local player jumps on the pad
        public void StartRiding(WindTunnel tunnel, bool forward, float launchForce)
        {
            // Allow starting a tunnel ride even if already wind riding (e.g. transitioning from cloud)
            // but ignore if we are already in a tunnel
            if (_currentMode == RidingMode.Tunnel) return;
            
            if (tunnel == null)
            {
                Debug.LogError("WindRiderController: cannot start riding with null tunnel");
                return;
            }

            tunnel.RefreshSplineData();

            _activeTunnel = tunnel;
            _ridingForward = forward;
            _currentMode = RidingMode.Tunnel;
            IsWindRiding = true;
            Debug.Log($"[WindRide] StartRiding Tunnel called on {gameObject.name}");

            // figure out where on the spline we are closest to
            _t = forward ? 0f : 1f;
            _currentSpeed = settings.baseSpeed;
            _currentSway = 0f;
            _targetSway = 0f;
            _swayVelocity = 0f;
            _ridingVelocity = Vector3.zero;

            // tell playercontroller we are riding (it will skip its own movement)
            _playerController.SetExternalVelocity(Vector3.up * launchForce);

            // Put the player on the playerintunnel layer to ignore cloud boundaries
            gameObject.layer = LayerMask.NameToLayer("PlayerInTunnel");

            WindRideEvents.InvokeRideStarted(_playerController, transform.position);
        }

        public void StartCloudRiding()
        {
            if (IsWindRiding) return;

            _currentMode = RidingMode.Cloud;
            IsWindRiding = true;
            
            // Inherit speed if we are already moving fast (e.g. from a dash or high speed glide)
            float horizontalSpeed = _playerController.CurrentMovementSpeed;
            _currentSpeed = Mathf.Max(settings.baseSpeed, horizontalSpeed);

            _currentSway = 0f;
            _swayVelocity = 0f;
            _ridingVelocity = Vector3.zero;
            _playerController.SetExternalVelocity(new Vector3(0, 0.01f, 0));

            // ignore cloud boundaries
            gameObject.layer = LayerMask.NameToLayer("PlayerInTunnel");
            WindRideEvents.InvokeRideStarted(_playerController, transform.position);
            Debug.Log($"[WindRide] StartCloudRiding at speed: {_currentSpeed}");
        }

        public void StartGliding()
        {
            if (_currentMode == RidingMode.Gliding) return;

            _currentMode = RidingMode.Gliding;
            IsWindRiding = true;

            // Initial speed based on current horizontal movement or base glide speed
            float horizontalSpeed = _playerController.CurrentMovementSpeed;
            _currentSpeed = Mathf.Max(settings.glideBaseSpeed, horizontalSpeed);

            _currentSway = 0f;
            _swayVelocity = 0f;
            _ridingVelocity = Vector3.zero;

            // Ensure camera reference
            if (_camera == null) _camera = FindFirstObjectByType<ThirdPersonCamera>();

            // Put the player on the playerintunnel layer to ignore cloud boundaries
            gameObject.layer = LayerMask.NameToLayer("PlayerInTunnel");

            WindRideEvents.InvokeRideStarted(_playerController, transform.position);
            Debug.Log($"[WindRide] StartGliding at speed: {_currentSpeed}");
        }

        public void EndCloudRiding()
{
            if (_currentMode != RidingMode.Cloud) return;

            // Preserve full momentum when jumping/exiting clouds
            Vector3 exitVelocity = transform.forward * _currentSpeed;
            Vector3 exitPos = transform.position;

            IsWindRiding = false;
            _currentMode = RidingMode.None;
            _swayVelocity = 0f;
            _ridingVelocity = Vector3.zero;

            _playerController.SetExternalVelocity(exitVelocity);
            gameObject.layer = LayerMask.NameToLayer("Player");
            WindRideEvents.InvokeRideEnded(_playerController, exitPos, exitVelocity);
            Debug.Log($"[WindRide] EndCloudRiding with exit velocity: {exitVelocity.magnitude}");
        }

        private void Update()
        {
            if (!IsWindRiding) return;

            // only the owner (or offline player) drives the riding logic
            // remote players see it via networked animator param + NetworkTransform
            if (!_playerController.IsOwner && !IsOffline()) return;

            // don't advance while paused
            if (Category5.UI.PauseMenu.GameIsPaused) return;

            HandleSwayInput();
            HandleSpeedInput();

            if (_currentMode == RidingMode.Tunnel)
            {
                AdvanceAlongSpline();
                ApplyMovement();
                RotateToTangent();
                FireProgressEvents();
                CheckExit();
            }
            else if (_currentMode == RidingMode.Cloud)
            {
                ApplyCloudMovement();
                RotateToVelocity();
            }
            else if (_currentMode == RidingMode.Gliding)
            {
                UpdateGliding();
            }
        }

        private void UpdateGliding()
        {
            if (_camera == null) _camera = FindFirstObjectByType<ThirdPersonCamera>();
            if (_camera == null) return;

            // Calculate pitch influence (-1 to 1 range, where 1 is looking down)
            // _camera.Pitch range is typically around minVerticalAngle to maxVerticalAngle
            // Let's normalize it so maxVerticalAngle (looking up) is -1 and minVerticalAngle (looking down) is 1
            float pitch = _camera.Pitch;
            
            // Normalize pitch: assuming min -20 (down) to max 60 (up) from ThirdPersonCamera.cs
            // Actually, we want a target dive angle. Let's use 0 as level, >0 as down, <0 as up.
            // In ThirdPersonCamera: _rotationY -= lookInput.y, so up is negative, down is positive? 
            // Let's check ThirdPersonCamera.cs: _rotationY is clamped between minVerticalAngle (-20) and maxVerticalAngle (60).
            // Usually -20 is looking up and 60 is looking down? No, usually positive is down in these setups.
            // Let's re-read ThirdPersonCamera: _rotationY -= _lookInput.y. If I move mouse up, _lookInput.y is positive, _rotationY decreases.
            // So negative _rotationY is looking UP, positive is looking DOWN.
            // Min -20 (up), Max 60 (down).

            float diveFactor = Mathf.Clamp(pitch / settings.glideMaxDiveAngle, -1f, 1f);

            // Speed logic: acceleration when diving, deceleration when leveling out
            if (diveFactor > 0.1f)
            {
                _currentSpeed += diveFactor * settings.glideAcceleration * Time.deltaTime;
            }
            else
            {
                _currentSpeed -= settings.glideDeceleration * Time.deltaTime;
            }
            _currentSpeed = Mathf.Clamp(_currentSpeed, settings.glideMinSpeed, settings.glideMaxSpeed);

            // Vertical velocity logic: gravity + lift
            // Lift is strongest when diveFactor is negative (looking up)
            float verticalVelocity = settings.glideGravity;
            if (diveFactor < 0)
            {
                // Looking up reduces falling speed
                verticalVelocity *= (1f - (Mathf.Abs(diveFactor) * settings.glidePitchLift));
            }
            else
            {
                // Diving increases falling speed
                verticalVelocity += diveFactor * settings.glideGravity * 2f; // Fall faster when diving
            }

            // Apply movement
            Vector3 forward = _camera.transform.forward;
            forward.y = 0;
            forward.Normalize();

            // Lateral steering (sway) inherited from common input handling
            Vector3 right = _camera.transform.right;
            right.y = 0;
            right.Normalize();

            Vector3 moveDir = forward * _currentSpeed + right * _swayVelocity + Vector3.up * verticalVelocity;
            _characterController.Move(moveDir * Time.deltaTime);
            _ridingVelocity = moveDir;

            // Rotate player to look where they are going
            RotateToVelocity();

            // Exit gliding if we hit the ground
            if (_characterController.isGrounded)
            {
                EndGliding();
            }
        }

        private void EndGliding()
        {
            if (_currentMode != RidingMode.Gliding) return;

            Vector3 exitVelocity = _ridingVelocity;
            Vector3 exitPos = transform.position;

            IsWindRiding = false;
            _currentMode = RidingMode.None;
            _swayVelocity = 0f;
            _ridingVelocity = Vector3.zero;

            _playerController.SetExternalVelocity(exitVelocity);
            gameObject.layer = LayerMask.NameToLayer("Player");
            WindRideEvents.InvokeRideEnded(_playerController, exitPos, exitVelocity);
            Debug.Log($"[WindRide] EndGliding with exit velocity: {exitVelocity.magnitude}");
        }

        private void ApplyCloudMovement()
{
            // Calculate forward and right based on horizontal plane to prevent downward pitch feedback
            Vector3 forward = transform.forward;
            forward.y = 0;
            forward.Normalize();
            
            Vector3 right = transform.right;
            right.y = 0;
            right.Normalize();

            // Forward and lateral movement
            Vector3 moveDir = forward * _currentSpeed + right * _swayVelocity;
            Vector3 frameMove = moveDir * Time.deltaTime;

            // Height correction (hovering above cloud)
            RaycastHit hit;
            LayerMask cloudLayer = 1 << 8; // CloudSurface
            if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out hit, 5f, cloudLayer, QueryTriggerInteraction.Collide))
            {
                float targetHeight = hit.point.y + settings.cloudHoverHeight;
                float heightDiff = targetHeight - transform.position.y;
                
                if (heightDiff > 0)
                {
                    frameMove.y = heightDiff;
                }
                else
                {
                    frameMove.y += heightDiff * settings.cloudFollowStiffness * Time.deltaTime;
                }
            }

            _characterController.Move(frameMove);
            _ridingVelocity = moveDir;
        }

        private void RotateToVelocity()
        {
            // For cloud riding, rotate to follow the horizontal velocity vector
            Vector3 horizontalVelocity = _ridingVelocity;
            horizontalVelocity.y = 0;

            if (horizontalVelocity.sqrMagnitude > 0.001f)
            {
                Quaternion baseRot = Quaternion.LookRotation(horizontalVelocity, Vector3.up);

                // Add banking roll (increased for cloud surfing to feel more evident)
                float leanTarget = -(_swayVelocity / settings.steeringResponsiveness) * settings.maxLeanAngle * settings.leanWeight * 1.5f;
                leanTarget = Mathf.Clamp(leanTarget, -settings.maxLeanAngle, settings.maxLeanAngle);
                Quaternion leanRot = Quaternion.Euler(0, 0, leanTarget);
                
                Quaternion targetRot = baseRot * leanRot;

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    settings.playerRotationSpeed * Time.deltaTime
                );
            }
        }

        private void HandleSwayInput()
        {
            Vector2 moveInput = _inputActions.Player.Move.ReadValue<Vector2>();
            float lateralInput = moveInput.x;

            // Surfing Handling: Build lateral velocity instead of direct position snapping
            if (Mathf.Abs(lateralInput) > 0.05f)
            {
                _swayVelocity += lateralInput * settings.steeringResponsiveness * Time.deltaTime;
            }

            // Apply inertia/friction (decay velocity)
            _swayVelocity *= settings.steeringInertia;

            // In Tunnel mode, we track a fixed path offset and clamp it
            if (_currentMode == RidingMode.Tunnel)
            {
                // Update sway position based on velocity
                _currentSway += _swayVelocity * Time.deltaTime;

                // Clamp sway position to bounds
                float limit = settings.maxSwayOffset;
                if (_currentSway > limit)
                {
                    _currentSway = limit;
                    _swayVelocity = 0f;
                }
                else if (_currentSway < -limit)
                {
                    _currentSway = -limit;
                    _swayVelocity = 0f;
                }
            }
            else if (_currentMode == RidingMode.Cloud)
            {
                // In Cloud mode, we don't track a center path offset (_currentSway)
                // We just let _swayVelocity drive the free-form movement
                _currentSway = 0f; 
            }

            float newNorm = NormalizedSway;
            WindRideEvents.InvokeSwayChanged(_playerController, newNorm);
        }

        private void HandleSpeedInput()
        {
            Vector2 moveInput = _inputActions.Player.Move.ReadValue<Vector2>();
            float forwardInput = moveInput.y; // W = 1, S = -1

            // Surfing Logic: Auto-accelerate if not braking
            if (forwardInput < -0.1f)
            {
                // Braking (leaning back)
                _currentSpeed -= settings.brakingDeceleration * Time.deltaTime;
            }
            else
            {
                // Auto-acceleration (even with no input or forward input)
                _currentSpeed += settings.acceleration * Time.deltaTime;
            }

            // Clamp current speed between min and max multipliers of base speed
            float minS = settings.baseSpeed * settings.minSpeedMultiplier;
            float maxS = settings.baseSpeed * settings.maxSpeedMultiplier;
            _currentSpeed = Mathf.Clamp(_currentSpeed, minS, maxS);
        }

        private void AdvanceAlongSpline()
        {
            float splineLength = _activeTunnel.SplineLength;
            if (splineLength <= 0f) return;

            float direction = _ridingForward ? 1f : -1f;
            float deltaT = (_currentSpeed / splineLength) * Time.deltaTime * direction;
            _t += deltaT;
        }

        private void ApplyMovement()
        {
            // target position on the spline with sway offset
            float clampedT = Mathf.Clamp01(_t);
            Vector3 pathPos = _activeTunnel.EvaluatePosition(clampedT);
            float direction = _ridingForward ? -1f : 1f;
            Vector3 right = _activeTunnel.GetRightVector(clampedT) * direction;
            Vector3 targetPos = pathPos + right * _currentSway;

            // wind force along the tangent
            Vector3 tangent = _activeTunnel.EvaluateTangent(clampedT);
            Vector3 windForce = tangent * _currentSpeed * direction;

            // gravity
            Vector3 gravityForce = Vector3.up * _playerController.Gravity * Time.deltaTime;

            // wind lift to counteract gravity, scaled along the tangent
            // the lift follows the tangent direction so uphill sections feel natural
            float liftStrength = Mathf.Abs(_playerController.Gravity) * settings.windLiftMultiplier;
            Vector3 liftForce = Vector3.up * liftStrength * Time.deltaTime;

            // spring force pulling toward the ideal path position
            Vector3 displacement = targetPos - transform.position;
            Vector3 springForce = displacement * settings.pathFollowStiffness;

            // combine forces
            _ridingVelocity = windForce + springForce;
            Vector3 frameMove = _ridingVelocity * Time.deltaTime + gravityForce + liftForce;

            _characterController.Move(frameMove);
        }

        private void RotateToTangent()
        {
            float clampedT = Mathf.Clamp01(_t);
            Vector3 tangent = _activeTunnel.EvaluateTangent(clampedT);
            float direction = _ridingForward ? 1f : -1f;
            Vector3 facing = tangent * direction;

            if (facing.sqrMagnitude > 0.001f)
            {
                Quaternion baseRot = Quaternion.LookRotation(facing, Vector3.up);

                // Add banking roll based on lateral velocity
                float leanTarget = -(_swayVelocity / settings.steeringResponsiveness) * settings.maxLeanAngle * settings.leanWeight;
                leanTarget = Mathf.Clamp(leanTarget, -settings.maxLeanAngle, settings.maxLeanAngle);
                Quaternion leanRot = Quaternion.Euler(0, 0, leanTarget);
                
                Quaternion targetRot = baseRot * leanRot;

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    settings.playerRotationSpeed * Time.deltaTime
                );
            }
        }

        private void FireProgressEvents()
        {
            float normalizedProgress = _ridingForward ? _t : (1f - _t);
            WindRideEvents.InvokeRideProgress(_playerController, Mathf.Clamp01(normalizedProgress), _currentSpeed);
        }

        private void CheckExit()
        {
            bool reachedEnd = _ridingForward ? _t >= 1f : _t <= 0f;
            if (reachedEnd)
            {
                EndRiding();
            }
        }

        private void EndRiding()
        {
            // calculate exit velocity preserving momentum along the tangent
            float exitT = _ridingForward ? 1f : 0f;
            Vector3 tangent = _activeTunnel.EvaluateTangent(Mathf.Clamp01(exitT));
            float direction = _ridingForward ? 1f : -1f;
            Vector3 exitVelocity = tangent * direction * _currentSpeed * settings.exitMomentumMultiplier;

            Vector3 exitPos = transform.position;

            IsWindRiding = false;
            _activeTunnel = null;
            _currentMode = RidingMode.None;
            _currentSway = 0f;
            _targetSway = 0f;
            _ridingVelocity = Vector3.zero;

            // hand velocity back to playercontroller so physics picks up naturally
            _playerController.SetExternalVelocity(exitVelocity);

            // Put the player on the playerintunnel layer to ignore cloud boundaries
            gameObject.layer = LayerMask.NameToLayer("Player");

            WindRideEvents.InvokeRideEnded(_playerController, exitPos, exitVelocity);
        }

        // force exit without momentum (used when component is disabled mid-ride)
        private void ForceEndRiding()
        {
            IsWindRiding = false;
            _activeTunnel = null;
            _currentMode = RidingMode.None;
            _currentSway = 0f;
            _targetSway = 0f;
            _ridingVelocity = Vector3.zero;
        }

        private bool IsOffline()
        {
            return Unity.Netcode.NetworkManager.Singleton == null ||
                   !Unity.Netcode.NetworkManager.Singleton.IsListening;
        }
    }
}
