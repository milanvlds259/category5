using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Category5.Core;
using Category5.Map;

namespace Category5.UI
{
    // storm map display showing eyewall rings, room states, and player position
    // subscribes to GameFlowManager and StormRoom events for live updates
    public class StormMapUI : MonoBehaviour
    {
        [Header("map container")]
        [Tooltip("parent RectTransform for room icons — icons are positioned relative to this")]
        [SerializeField] private RectTransform mapContainer;

        [Header("icon prefabs")]
        [Tooltip("prefab for a room icon (Image component required)")]
        [SerializeField] private GameObject roomIconPrefab;
        [Tooltip("prefab for the eye/boss icon")]
        [SerializeField] private GameObject eyeIconPrefab;
        [Tooltip("prefab for the player position indicator")]
        [SerializeField] private GameObject playerIconPrefab;

        [Header("colors")]
        [SerializeField] private Color hiddenColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color visibleColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        [SerializeField] private Color activeColor = new Color(1f, 1f, 0.3f, 1f);
        [SerializeField] private Color clearedColor = new Color(0.3f, 1f, 0.3f, 0.9f);
        [SerializeField] private Color eyeColor = new Color(1f, 0.4f, 0f, 1f);

        [Header("scaling")]
        [Tooltip("world units per UI pixel — controls how zoomed in the map is")]
        [SerializeField] private float worldToUIScale = 0.5f;

        // runtime state
        private MapLayout _layout;
        private Dictionary<int, Image> _roomIcons = new Dictionary<int, Image>();
        private Image _eyeIcon;
        private RectTransform _playerIcon;
        private bool _initialized = false;

        private void OnEnable()
        {
            // subscribe to room events for live updates
            StormRoom.OnRoomCleared += HandleRoomStateChanged;
            StormRoom.OnRoomActivated += HandleRoomStateChanged;
            StormRoom.OnRoomDiscovered += HandleRoomStateChanged;
        }

        private void OnDisable()
        {
            StormRoom.OnRoomCleared -= HandleRoomStateChanged;
            StormRoom.OnRoomActivated -= HandleRoomStateChanged;
            StormRoom.OnRoomDiscovered -= HandleRoomStateChanged;
        }

        private void Start()
        {
            TryInitialize();
        }

        private void Update()
        {
            if (!_initialized)
            {
                TryInitialize();
            }

            UpdatePlayerIcon();
        }

        // =====================================
        // initialization
        // =====================================

        private void TryInitialize()
        {
            if (_initialized) return;
            if (GameFlowManager.Instance == null) return;
            if (GameFlowManager.Instance.CurrentLayout == null) return;

            _layout = GameFlowManager.Instance.CurrentLayout;
            BuildMap();
            _initialized = true;
        }

        private void BuildMap()
        {
            if (mapContainer == null)
            {
                Debug.LogError("[StormMapUI] mapContainer not assigned");
                return;
            }

            // clear any existing icons
            foreach (Transform child in mapContainer)
            {
                Destroy(child.gameObject);
            }
            _roomIcons.Clear();

            if (_layout == null) return;

            // create icons for each room
            for (int i = 0; i < _layout.TotalRooms; i++)
            {
                var roomData = _layout.GetRoom(i);

                // skip the eye — it gets its own icon
                if (roomData.eyewallIndex == -1)
                {
                    CreateEyeIcon(roomData);
                    continue;
                }

                CreateRoomIcon(roomData);
            }

            // create player icon
            if (playerIconPrefab != null)
            {
                GameObject playerObj = Instantiate(playerIconPrefab, mapContainer);
                _playerIcon = playerObj.GetComponent<RectTransform>();
            }

            // initial color update
            RefreshAllRoomColors();
        }

        private void CreateRoomIcon(StormRoomData data)
        {
            if (roomIconPrefab == null) return;

            GameObject iconObj = Instantiate(roomIconPrefab, mapContainer);
            RectTransform rt = iconObj.GetComponent<RectTransform>();
            rt.anchoredPosition = WorldToUIPosition(data.worldPosition);

            Image img = iconObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = hiddenColor;
            }

            _roomIcons[data.roomIndex] = img;
        }

        private void CreateEyeIcon(StormRoomData data)
        {
            if (eyeIconPrefab == null) return;

            GameObject iconObj = Instantiate(eyeIconPrefab, mapContainer);
            RectTransform rt = iconObj.GetComponent<RectTransform>();
            rt.anchoredPosition = WorldToUIPosition(data.worldPosition);

            Image img = iconObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = eyeColor;
            }

            _eyeIcon = img;
        }

        // =====================================
        // updates
        // =====================================

        private void HandleRoomStateChanged(StormRoom room)
        {
            if (room == null) return;
            UpdateRoomColor(room.RoomIndex, room.CurrentState);
        }

        private void UpdateRoomColor(int roomIndex, StormRoomState state)
        {
            if (!_roomIcons.TryGetValue(roomIndex, out Image img)) return;
            if (img == null) return;

            img.color = StateToColor(state);
        }

        private void RefreshAllRoomColors()
        {
            if (_layout == null) return;

            var allRooms = FindObjectsByType<StormRoom>(FindObjectsSortMode.None);
            foreach (var room in allRooms)
            {
                if (room == null) continue;
                UpdateRoomColor(room.RoomIndex, room.CurrentState);
            }
        }

        private void UpdatePlayerIcon()
        {
            if (_playerIcon == null) return;

            // find the local player
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null) return;

            _playerIcon.anchoredPosition = WorldToUIPosition(localPlayer.transform.position);
        }

        // =====================================
        // helpers
        // =====================================

        private Vector2 WorldToUIPosition(Vector3 worldPos)
        {
            // convert world position to UI position relative to map container
            // assumes map container is centered at world origin
            return new Vector2(worldPos.x * worldToUIScale, worldPos.z * worldToUIScale);
        }

        private Color StateToColor(StormRoomState state)
        {
            switch (state)
            {
                case StormRoomState.Active: return activeColor;
                case StormRoomState.Cleared: return clearedColor;
                default: return activeColor;
            }
        }

        private Player.PlayerController FindLocalPlayer()
        {
            var players = FindObjectsByType<Player.PlayerController>(FindObjectsSortMode.None);
            foreach (var player in players)
            {
                if (player.IsOwner) return player;
            }
            // fallback to first player if no owner found (offline mode)
            return players.Length > 0 ? players[0] : null;
        }
    }
}
