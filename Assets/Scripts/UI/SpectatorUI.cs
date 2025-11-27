using UnityEngine;
using TMPro;

namespace Category5.UI
{
    // displays spectator mode information when the local player is dead
    public class SpectatorUI : MonoBehaviour
    {
        [Header("ui references")]
        [SerializeField] private GameObject spectatorPanel;
        [SerializeField] private TextMeshProUGUI spectatingText;
        [SerializeField] private TextMeshProUGUI instructionsText;
        
        private ThirdPersonCamera _camera;
        
        private void Start()
        {
            // hide panel initially
            if (spectatorPanel != null)
            {
                spectatorPanel.SetActive(false);
            }
            
            // set instructions text
            if (instructionsText != null)
            {
                instructionsText.text = "Press JUMP to switch player";
            }
            
            // find camera and subscribe to events
            FindAndSubscribeToCamera();
        }
        
        private void Update()
        {
            // keep trying to find camera if not found
            if (_camera == null)
            {
                FindAndSubscribeToCamera();
            }
        }
        
        private void FindAndSubscribeToCamera()
        {
            if (_camera != null) return;
            
            _camera = FindFirstObjectByType<ThirdPersonCamera>();
            if (_camera != null)
            {
                _camera.OnSpectateTargetChanged += OnSpectateTargetChanged;
            }
        }
        
        private void OnDestroy()
        {
            if (_camera != null)
            {
                _camera.OnSpectateTargetChanged -= OnSpectateTargetChanged;
            }
        }
        
        private void OnSpectateTargetChanged(string playerName)
        {
            if (spectatorPanel == null) return;
            
            if (string.IsNullOrEmpty(playerName))
            {
                // exiting spectator mode
                spectatorPanel.SetActive(false);
            }
            else
            {
                // entering or switching spectate target
                spectatorPanel.SetActive(true);
                
                if (spectatingText != null)
                {
                    spectatingText.text = $"Spectating: {playerName}";
                }
            }
        }
    }
}
