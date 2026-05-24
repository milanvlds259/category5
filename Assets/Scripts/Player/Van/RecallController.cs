using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

namespace Category5.Player.Van
{
    public class RecallController : NetworkBehaviour
    {
        private const float RECALL_CHANNEL_TIME = 5f;

        private enum RecallState { Idle, Channeling }

        [Header("Settings")]
        [SerializeField] private float channelTime = RECALL_CHANNEL_TIME;

        private RecallState _state = RecallState.Idle;
        private float _channelTimer = 0f;
        private PlayerController _playerController;
        private WindRiding.WindRiderController _windRider;
        private InputSystem_Actions _inputActions;
        private bool _inputBound;

        // public state
        public bool IsChanneling => _state == RecallState.Channeling;
        public float ChannelProgress => _state == RecallState.Channeling ? Mathf.Clamp01(_channelTimer / channelTime) : 0f;

        // events
        public event System.Action OnRecallStarted;
        public event System.Action OnRecallCompleted;
        public event System.Action OnRecallInterrupted;
        public event System.Action<float> OnRecallProgress;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _windRider = GetComponent<WindRiding.WindRiderController>();
        }

        private void OnEnable()
        {
            if (_inputActions == null)
                _inputActions = new InputSystem_Actions();
            _inputActions.Player.Enable();
            _inputActions.Player.Recall.started += OnRecallStarted;
            _inputActions.Player.Recall.canceled += OnRecallCanceled;
            _inputBound = true;
        }

        private void OnDisable()
        {
            if (_inputBound)
            {
                _inputActions.Player.Recall.started -= OnRecallStarted;
                _inputActions.Player.Recall.canceled -= OnRecallCanceled;
                _inputActions.Player.Disable();
                _inputBound = false;
            }
            InterruptRecall();
        }

        private void Update()
        {
            if (!IsOwner && !IsOffline()) return;

            if (_state != RecallState.Channeling) return;

            if (_playerController.IsDead.Value)
            {
                InterruptRecall();
                return;
            }

            // Check for movement input interruption
            Vector2 moveInput = _inputActions.Player.Move.ReadValue<Vector2>();
            if (moveInput.sqrMagnitude > 0.05f)
            {
                InterruptRecall();
                return;
            }

            // Check for wind riding
            if (_windRider != null && _windRider.IsWindRiding)
            {
                InterruptRecall();
                return;
            }

            // Advance channel timer
            _channelTimer += Time.deltaTime;
            OnRecallProgress?.Invoke(ChannelProgress);

            if (_channelTimer >= channelTime)
            {
                CompleteRecall();
            }
        }

        private void OnRecallStarted(InputAction.CallbackContext context)
        {
            if (!IsOwner && !IsOffline()) return;
            if (_playerController.IsDead.Value) return;
            if (_state == RecallState.Channeling) return;
            if (_windRider != null && _windRider.IsWindRiding) return;
            if (Category5.UI.PauseMenu.GameIsPaused) return;

            _state = RecallState.Channeling;
            _channelTimer = 0f;
            OnRecallStarted?.Invoke();
            OnRecallProgress?.Invoke(0f);
            Debug.Log("[Recall] Started channeling");
        }

        private void OnRecallCanceled(InputAction.CallbackContext context)
        {
            if (_state != RecallState.Channeling) return;
            InterruptRecall();
        }

        private void CompleteRecall()
        {
            if (!IsOwner && !IsOffline()) return;

            _state = RecallState.Idle;
            _channelTimer = 0f;
            OnRecallCompleted?.Invoke();
            Debug.Log("[Recall] Channel complete, requesting recall");

            if (IsOffline())
            {
                TeleportToVan();
            }
            else
            {
                RequestRecallToVanServerRpc();
            }
        }

        public void InterruptRecall()
        {
            if (_state != RecallState.Channeling) return;

            _state = RecallState.Idle;
            _channelTimer = 0f;
            OnRecallInterrupted?.Invoke();
            OnRecallProgress?.Invoke(0f);
        }

        [Rpc(SendTo.Server)]
        private void RequestRecallToVanServerRpc(RpcParams rpcParams = default)
        {
            if (_playerController.IsDead.Value) return;
            if (_windRider != null && _windRider.IsWindRiding) return;

            TeleportToVan();
        }

        private void TeleportToVan()
        {
            var spawnPoint = Core.PlayerSpawnPoint.GetNextVanSpawnPoint();
            if (spawnPoint == null)
            {
                Debug.LogError("[Recall] No van spawn point found");
                return;
            }

            _playerController.RecallTeleport(spawnPoint.transform.position, spawnPoint.transform.rotation);
        }

        private bool IsOffline()
        {
            return NetworkManager.Singleton == null ||
                   !NetworkManager.Singleton.IsListening;
        }
    }
}
