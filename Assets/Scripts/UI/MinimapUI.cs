using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Category5.Player;
using Unity.Netcode;

namespace Category5.UI
{
    // borderlands-style circular radar minimap
    // displays icons for players, enemies, and bosses within radar range
    // rotates with camera so "up" is always camera forward direction
    public class MinimapUI : MonoBehaviour
    {
        [Header("radar settings")]
        [Tooltip("world units radius for radar detection")]
        [SerializeField] private float radarRange = 50f;
        
        [Tooltip("ui radius in pixels for the radar display")]
        [SerializeField] private float radarRadius = 80f;
        
        [Tooltip("how often to update icon positions (seconds)")]
        [SerializeField] private float updateInterval = 0.05f;

        [Header("ui references")]
        [Tooltip("the rotating container that spins with camera (icons go inside)")]
        [SerializeField] private RectTransform iconContainer;
        
        [Tooltip("the static player icon at center (arrow pointing up)")]
        [SerializeField] private RectTransform playerIcon;
        
        [Tooltip("prefab for enemy/player icons (simple image)")]
        [SerializeField] private GameObject iconPrefab;

        [Header("icon settings")]
        [SerializeField] private float baseIconSize = 12f;
        [SerializeField] private Color playerColor = new Color(0.2f, 0.6f, 1f); // blue
        [SerializeField] private Color enemyColor = new Color(1f, 0.2f, 0.2f); // red
        [SerializeField] private Color bossColor = new Color(1f, 0.6f, 0f); // orange
        [SerializeField] private float bossIconMultiplier = 1.5f;

        // tracking
        private Dictionary<MinimapTrackable, RectTransform> _activeIcons = new Dictionary<MinimapTrackable, RectTransform>();
        private List<MinimapTrackable> _toRemove = new List<MinimapTrackable>();
        private List<RectTransform> _iconPool = new List<RectTransform>();
        
        // local player reference
        private Transform _localPlayerTransform;
        private PlayerController _localPlayerController;
        private Camera _mainCamera;
        
        private float _updateTimer;

        private void Start()
        {
            _mainCamera = Camera.main;
            
            // try to find local player on start
            FindLocalPlayer();
        }

        private void LateUpdate()
        {
            // throttle updates for performance
            _updateTimer -= Time.deltaTime;
            if (_updateTimer > 0f) return;
            _updateTimer = updateInterval;

            // ensure we have a local player reference
            if (_localPlayerTransform == null)
            {
                FindLocalPlayer();
                if (_localPlayerTransform == null) return;
            }

            // rotate the icon container opposite to camera yaw
            // this makes "up" on the radar always match camera forward
            if (_mainCamera != null && iconContainer != null)
            {
                float cameraYaw = _mainCamera.transform.eulerAngles.y;
                iconContainer.localRotation = Quaternion.Euler(0f, 0f, cameraYaw);
            }

            // update all trackable icons
            UpdateIcons();
        }

        private void FindLocalPlayer()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
                return;

            // find the local player's PlayerController
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject != null)
                {
                    var controller = client.PlayerObject.GetComponent<PlayerController>();
                    if (controller != null && controller.IsOwner)
                    {
                        _localPlayerController = controller;
                        _localPlayerTransform = controller.transform;
                        return;
                    }
                }
            }
        }

        private void UpdateIcons()
        {
            Vector3 playerPos = _localPlayerTransform.position;
            
            // mark icons for removal (trackables that are destroyed or disabled/out of range)
            _toRemove.Clear();
            foreach (var kvp in _activeIcons)
            {
                if (kvp.Key == null || !kvp.Key.enabled)
                {
                    _toRemove.Add(kvp.Key);
                }
            }
            
            // remove dead trackables
            foreach (var trackable in _toRemove)
            {
                ReturnIconToPool(_activeIcons[trackable]);
                _activeIcons.Remove(trackable);
            }

            // iterate all trackables
            var allTrackables = MinimapTrackable.AllTrackables;
            for (int i = 0; i < allTrackables.Count; i++)
            {
                MinimapTrackable trackable = allTrackables[i];
                if (trackable == null) continue;

                // skip local player's own trackable (we have a static icon for that)
                if (trackable.transform == _localPlayerTransform) continue;

                // calculate distance
                Vector3 offset = trackable.transform.position - playerPos;
                float distance = new Vector2(offset.x, offset.z).magnitude;

                // check if within radar range
                if (distance > radarRange)
                {
                    // out of range - remove icon if exists
                    if (_activeIcons.TryGetValue(trackable, out RectTransform existingIcon))
                    {
                        ReturnIconToPool(existingIcon);
                        _activeIcons.Remove(trackable);
                    }
                    continue;
                }

                // get or create icon
                RectTransform icon;
                if (!_activeIcons.TryGetValue(trackable, out icon))
                {
                    icon = GetIconFromPool();
                    _activeIcons[trackable] = icon;
                    
                    // configure icon appearance
                    ConfigureIcon(icon, trackable);
                }

                // calculate position on radar
                // normalize offset to radar radius
                float normalizedDist = distance / radarRange;
                Vector2 radarOffset = new Vector2(offset.x, offset.z).normalized * (normalizedDist * radarRadius);
                
                // set icon position (z offset becomes y on radar, x stays x)
                icon.anchoredPosition = new Vector2(radarOffset.x, radarOffset.y);
            }
        }

        private void ConfigureIcon(RectTransform icon, MinimapTrackable trackable)
        {
            Image image = icon.GetComponent<Image>();
            if (image == null) return;

            // set color based on type
            Color color;
            float sizeMultiplier = trackable.IconSizeMultiplier;
            
            switch (trackable.TrackableType)
            {
                case TrackableType.Player:
                    color = playerColor;
                    break;
                case TrackableType.Boss:
                    color = bossColor;
                    sizeMultiplier *= bossIconMultiplier;
                    break;
                case TrackableType.Enemy:
                default:
                    color = enemyColor;
                    break;
            }

            // allow trackable to override color
            if (trackable.IconColor != Color.clear && trackable.IconColor != Color.white)
            {
                color = trackable.IconColor;
            }

            image.color = color;
            
            // set size
            float size = baseIconSize * sizeMultiplier;
            icon.sizeDelta = new Vector2(size, size);
        }

        private RectTransform GetIconFromPool()
        {
            // try to reuse pooled icon
            if (_iconPool.Count > 0)
            {
                RectTransform pooled = _iconPool[_iconPool.Count - 1];
                _iconPool.RemoveAt(_iconPool.Count - 1);
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            // create new icon
            if (iconPrefab == null)
            {
                // fallback: create a simple circle icon programmatically
                GameObject iconGo = new GameObject("MinimapIcon");
                iconGo.transform.SetParent(iconContainer, false);
                
                RectTransform rt = iconGo.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(baseIconSize, baseIconSize);
                
                Image img = iconGo.AddComponent<Image>();
                // use default white sprite (will be tinted by color)
                
                return rt;
            }
            else
            {
                GameObject iconGo = Instantiate(iconPrefab, iconContainer);
                return iconGo.GetComponent<RectTransform>();
            }
        }

        private void ReturnIconToPool(RectTransform icon)
        {
            if (icon == null) return;
            
            icon.gameObject.SetActive(false);
            _iconPool.Add(icon);
        }

        // public methods for external configuration
        public void SetRadarRange(float range)
        {
            radarRange = Mathf.Max(1f, range);
        }

        public float GetRadarRange() => radarRange;
    }
}
