using UnityEngine;
using TMPro;
using Unity.Collections;
using Category5.Player;

namespace Category5.UI
{
    // world space name tag that floats above a player
    // this component should be added to the player prefab as a child object
    // it automatically faces the camera and fades based on distance
    public class PlayerNameTag : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Header("Settings")]
        [SerializeField] private float minDistance = 3f; // hide when closer than this
        [SerializeField] private float fadeDistance = 5f; // start fading at this distance
        [SerializeField] private bool hideForLocalPlayer = true;
        
        private PlayerController _playerController;
        private Transform _cameraTransform;
        private bool _isLocalPlayer;
        private bool _isInitialized;
        
        private void Start()
        {
            // get player controller from parent
            _playerController = GetComponentInParent<PlayerController>();
            
            if (_playerController == null)
            {
                Debug.LogError("PlayerNameTag: No PlayerController found in parent hierarchy!");
                return;
            }
            
            // wait for network spawn before initializing
            // the player controller will call Initialize() after OnNetworkSpawn
        }
        
        // called by PlayerController after OnNetworkSpawn
        public void Initialize()
        {
            if (_isInitialized) return;
            
            _playerController = GetComponentInParent<PlayerController>();
            if (_playerController == null) return;
            
            _isLocalPlayer = _playerController.IsOwner;
            
            // subscribe to name changes
            _playerController.PlayerName.OnValueChanged += OnNameChanged;
            
            // set initial name
            UpdateNameDisplay();
            
            // hide for local player if setting is enabled
            if (_isLocalPlayer && hideForLocalPlayer)
            {
                gameObject.SetActive(false);
            }
            
            _isInitialized = true;
        }
        
        private void OnDestroy()
        {
            if (_playerController != null)
            {
                _playerController.PlayerName.OnValueChanged -= OnNameChanged;
            }
        }
        
        private void OnNameChanged(FixedString64Bytes oldName, FixedString64Bytes newName)
        {
            UpdateNameDisplay();
        }
        
        private void UpdateNameDisplay()
        {
            if (nameText != null && _playerController != null)
            {
                nameText.text = _playerController.GetPlayerName();
            }
        }
        
        private void LateUpdate()
        {
            if (!_isInitialized) return;
            
            // update camera reference if needed
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
            
            if (_cameraTransform == null) return;
            
            // billboard - always face the camera
            transform.rotation = Quaternion.LookRotation(transform.position - _cameraTransform.position);
            
            // calculate distance based fade
            if (canvasGroup != null)
            {
                float distance = Vector3.Distance(_cameraTransform.position, transform.position);
                
                if (distance < minDistance)
                {
                    canvasGroup.alpha = 0f;
                }
                else if (distance < fadeDistance)
                {
                    canvasGroup.alpha = (distance - minDistance) / (fadeDistance - minDistance);
                }
                else
                {
                    canvasGroup.alpha = 1f;
                }
            }
        }
        
        // set visibility (used when player dies/respawns)
        public void SetVisible(bool visible)
        {
            if (_isLocalPlayer && hideForLocalPlayer)
            {
                gameObject.SetActive(false);
                return;
            }
            
            gameObject.SetActive(visible);
        }
    }
}
