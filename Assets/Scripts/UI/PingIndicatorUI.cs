using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Category5.UI
{
    // displays local client's network ping (for obvious reasons does not work on host)
    // color-coded: green (<50ms), yellow (50-100ms), red (>100ms)
    // updates every 0.5 seconds to avoid performance overhead
    public class PingIndicatorUI : MonoBehaviour
    {
        [Header("ui references")]
        [Tooltip("text component displaying ping value")]
        [SerializeField] private TextMeshProUGUI pingText;
        
        [Header("settings")]
        [Tooltip("how often to update ping display (seconds)")]
        [SerializeField] private float updateInterval = 0.5f;
        
        [Header("color thresholds (ms)")]
        [SerializeField] private int goodPingThreshold = 50;
        [SerializeField] private int okayPingThreshold = 100;
        [SerializeField] private Color goodColor = Color.green;
        [SerializeField] private Color okayColor = Color.yellow;
        [SerializeField] private Color badColor = Color.red;
        
        private float _updateTimer;

        private void LateUpdate()
        {
            // throttle updates for performance
            _updateTimer -= Time.deltaTime;
            if (_updateTimer > 0f) return;
            _updateTimer = updateInterval;
            
            UpdatePingDisplay();
        }

        private void UpdatePingDisplay()
        {
            // only show ping when connected as client
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
            {
                if (pingText != null)
                    pingText.text = "";
                return;
            }

            // get transport component
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                if (pingText != null)
                    pingText.text = "";
                return;
            }

            // get round-trip time from transport
            int ping = (int)transport.GetCurrentRtt(NetworkManager.ServerClientId);
            
            ping = Mathf.Clamp(ping, 0, 999);
            
            // update text and color
            if (pingText != null)
            {
                pingText.text = $"{ping} ms";
                pingText.color = GetPingColor(ping);
            }
        }

        private Color GetPingColor(int ping)
        {
            if (ping < goodPingThreshold)
                return goodColor;
            else if (ping < okayPingThreshold)
                return okayColor;
            else
                return badColor;
        }
    }
}
