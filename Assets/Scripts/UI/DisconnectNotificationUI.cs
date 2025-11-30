using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Category5.Core;

namespace Category5.UI
{
    // displays notifications when players disconnect or connection is lost
    public class DisconnectNotificationUI : MonoBehaviour
    {
        [Header("player disconnect notification")]
        [SerializeField] private GameObject playerDisconnectPanel;
        [SerializeField] private TextMeshProUGUI playerDisconnectText;
        [SerializeField] private float playerNotificationDuration = 3f;
        
        [Header("host disconnect notification")]
        [SerializeField] private GameObject hostDisconnectPanel;
        [SerializeField] private TextMeshProUGUI hostDisconnectText;
        [SerializeField] private TextMeshProUGUI hostDisconnectSubtext;
        
        [Header("animation")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        
        private CanvasGroup _playerPanelCanvasGroup;
        private CanvasGroup _hostPanelCanvasGroup;
        private Coroutine _playerNotificationCoroutine;
        
        private void Awake()
        {
            // ensure canvas groups exist for fading
            if (playerDisconnectPanel != null)
            {
                _playerPanelCanvasGroup = playerDisconnectPanel.GetComponent<CanvasGroup>();
                if (_playerPanelCanvasGroup == null)
                {
                    _playerPanelCanvasGroup = playerDisconnectPanel.AddComponent<CanvasGroup>();
                }
            }
            
            if (hostDisconnectPanel != null)
            {
                _hostPanelCanvasGroup = hostDisconnectPanel.GetComponent<CanvasGroup>();
                if (_hostPanelCanvasGroup == null)
                {
                    _hostPanelCanvasGroup = hostDisconnectPanel.AddComponent<CanvasGroup>();
                }
            }
        }
        
        private void Start()
        {
            // hide panels initially
            HideAllPanels();
            
            // subscribe to network session events
            NetworkSessionManager.OnPlayerDisconnected += OnPlayerDisconnected;
            NetworkSessionManager.OnHostDisconnected += OnHostDisconnected;
        }
        
        private void OnDestroy()
        {
            NetworkSessionManager.OnPlayerDisconnected -= OnPlayerDisconnected;
            NetworkSessionManager.OnHostDisconnected -= OnHostDisconnected;
        }
        
        private void HideAllPanels()
        {
            if (playerDisconnectPanel != null)
            {
                playerDisconnectPanel.SetActive(false);
            }
            
            if (hostDisconnectPanel != null)
            {
                hostDisconnectPanel.SetActive(false);
            }
        }
        
        // called when another player disconnects
        private void OnPlayerDisconnected(ulong clientId, string playerName)
        {
            if (playerDisconnectPanel == null) return;
            
            // update text
            if (playerDisconnectText != null)
            {
                playerDisconnectText.text = $"{playerName} disconnected";
            }
            
            // show notification with auto-hide
            if (_playerNotificationCoroutine != null)
            {
                StopCoroutine(_playerNotificationCoroutine);
            }
            _playerNotificationCoroutine = StartCoroutine(ShowPlayerNotification());
        }
        
        // called when host disconnects (client-side only)
        private void OnHostDisconnected(string reason)
        {
            if (hostDisconnectPanel == null) return;
            
            // update text
            if (hostDisconnectText != null)
            {
                hostDisconnectText.text = "Connection Lost";
            }
            
            if (hostDisconnectSubtext != null)
            {
                hostDisconnectSubtext.text = $"{reason}\nReturning to menu...";
            }
            
            // show panel (stays visible until scene change)
            ShowHostDisconnectPanel();
            
            // unlock cursor so player can see the message
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        private IEnumerator ShowPlayerNotification()
        {
            // show and fade in
            playerDisconnectPanel.SetActive(true);
            
            if (_playerPanelCanvasGroup != null)
            {
                _playerPanelCanvasGroup.alpha = 0f;
                
                float elapsed = 0f;
                while (elapsed < fadeInDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _playerPanelCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                    yield return null;
                }
                _playerPanelCanvasGroup.alpha = 1f;
            }
            
            // wait
            yield return new WaitForSecondsRealtime(playerNotificationDuration);
            
            // fade out
            if (_playerPanelCanvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _playerPanelCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                    yield return null;
                }
            }
            
            playerDisconnectPanel.SetActive(false);
            _playerNotificationCoroutine = null;
        }
        
        private void ShowHostDisconnectPanel()
        {
            hostDisconnectPanel.SetActive(true);
            
            if (_hostPanelCanvasGroup != null)
            {
                StartCoroutine(FadeInPanel(_hostPanelCanvasGroup));
            }
        }
        
        private IEnumerator FadeInPanel(CanvasGroup canvasGroup)
        {
            canvasGroup.alpha = 0f;
            
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }
    }
}
