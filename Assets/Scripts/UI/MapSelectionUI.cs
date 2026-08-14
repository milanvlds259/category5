using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Category5.Map;
using Category5.Core;

namespace Category5.UI
{
    // full-screen tactical map overlay showing the storm room layout
    // opened by interacting with the map table in the van
    // host clicks a connected room to select the next destination
	// pd i copypasted a lot of ts so it might be buggy
    public class MapSelectionUI : MonoBehaviour
    {
        // static state for input conflict prevention
        public static bool IsOpen { get; private set; }

        [Header("settings")]
        [SerializeField] private float mapRadius = 280f;
        [SerializeField] private float fadeInDuration = 0.2f;

        [Header("blueprint")]
        [Tooltip("optional background image — set automatically from blueprint if assigned")]
        [SerializeField] private Image backgroundImage;

        [Header("title")]
        [Tooltip("optional font for the 'STORM MAP' title — leave null for default")]
        [SerializeField] private TMP_FontAsset titleFont;

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _mapContainer;
        private GameObject _roomNodePrefab;
        private MapLayout _layout;
        private int _currentRoomIndex;
        private int _selectedRoomIndex = -1;

        private Dictionary<int, MapSelectionNode> _nodes = new Dictionary<int, MapSelectionNode>();
        private List<Selectable> _selectables = new List<Selectable>();
        private float _fadeTarget = 0f;

        private void Awake()
        {
            CreateUI();

            // start hidden via canvas group alpha (same pattern as other UI)
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        // =====================================
        // public API
        // =====================================

        public void Open()
        {
            _layout = RoomManager.Instance?.Layout;
            if (_layout == null)
            {
                Debug.LogWarning("[MapSelectionUI] no layout available");
                return;
            }

            _currentRoomIndex = RoomManager.Instance.CurrentRoomIndex.Value;
            _selectedRoomIndex = -1;

            BuildMap();
            EvaluateSelectionState();

            IsOpen = true;
            _fadeTarget = 1f;
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            // make this a screen-space overlay on top of everything
            _canvas.sortingOrder = 999;

            // unlock cursor so player can click nodes
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // select first selectable for gamepad nav
            if (_selectables.Count > 0)
                _selectables[0].Select();
        }

        public void Close()
        {
            _fadeTarget = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            // re-lock cursor when closing
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_canvasGroup.alpha <= 0.01f)
            {
                IsOpen = false;
            }
        }

        // called by MapSelectionNode when a room is clicked
        public void OnNodeSelected(int roomIndex)
        {
            if (!IsHostOrSolo()) return;
            if (_selectedRoomIndex >= 0) return;

            if (!IsSelectable(roomIndex)) return;

            _selectedRoomIndex = roomIndex;
            Close();

            // start prep timer via RoomManager
            RoomManager.Instance?.StartPrepTimerForRoom(roomIndex);
        }

        // =====================================
        // state evaluation
        // =====================================

        private void EvaluateSelectionState()
        {
            var connected = _layout.GetConnectedRooms(_currentRoomIndex);
            var selectableSet = new HashSet<int>(connected);

            foreach (var kvp in _nodes)
            {
                int idx = kvp.Key;
                var node = kvp.Value;

                if (idx == _currentRoomIndex)
                {
                    node.SetAsCurrent();
                }
                else if (selectableSet.Contains(idx))
                {
                    if (IsHostOrSolo())
                        node.SetSelectable(true);
                    else
                        node.SetVisualState(new Color(0f, 0.7f, 0.9f, 0.6f));
                }
                else if (idx == _layout.EyeRoomIndex)
                {
                    node.SetAsEyeRoom();
                }
                else
                {
                    node.SetVisualState(new Color(0.4f, 0.4f, 0.4f, 0.5f));
                }
            }

            // auto-select if only one option
            if (IsHostOrSolo() && connected.Count == 1)
            {
                OnNodeSelected(connected[0]);
            }
        }

        private bool IsSelectable(int roomIndex)
        {
            if (_layout == null) return false;
            var connected = _layout.GetConnectedRooms(_currentRoomIndex);
            return connected.Contains(roomIndex);
        }

        private bool IsHostOrSolo()
        {
            var net = Unity.Netcode.NetworkManager.Singleton;
            return net == null || !net.IsListening || net.IsServer;
        }

        // =====================================
        // blueprint lookup
        // =====================================

        // tries to find the active blueprint from multiple sources
        private static StormBlueprint GetBlueprint()
        {
            if (GameFlowManager.Instance != null)
            {
                var storm = GameFlowManager.Instance.GetCurrentStorm();
                if (storm != null)
                {
                    if (storm.blueprint != null)
                        return storm.blueprint;
                    Debug.LogWarning($"[MapSelectionUI] GameFlowManager storm '{storm.name}' has no blueprint assigned");
                }
                else
                {
                    Debug.Log("[MapSelectionUI] GameFlowManager.Instance exists but GetCurrentStorm() returned null");
                }
            }
            else
            {
                Debug.Log("[MapSelectionUI] GameFlowManager.Instance is null");
            }

            var mapGen = FindFirstObjectByType<MapGenerator>();
            if (mapGen != null)
            {
                if (mapGen.DefaultStorm != null)
                {
                    if (mapGen.DefaultStorm.blueprint != null)
                        return mapGen.DefaultStorm.blueprint;
                    Debug.LogWarning($"[MapSelectionUI] MapGenerator.DefaultStorm '{mapGen.DefaultStorm.name}' has no blueprint assigned");
                }
                else
                {
                    Debug.LogWarning("[MapSelectionUI] MapGenerator found but DefaultStorm is null — assign a StormData to MapGenerator.defaultStorm in the inspector");
                }
            }
            else
            {
                Debug.LogWarning("[MapSelectionUI] no MapGenerator found in scene");
            }

            return null;
        }

        // =====================================
        // map building
        // =====================================

        private void BuildMap()
        {
            ClearMap();

            if (_layout == null) return;

            // apply blueprint background if available
            var bp = GetBlueprint();
            if (bp != null)
            {
                Debug.Log($"[MapSelectionUI] using blueprint '{bp.name}'");

                if (backgroundImage != null)
                {
                    if (bp.mapBackground != null)
                        backgroundImage.sprite = bp.mapBackground;

                    backgroundImage.color = bp.mapTint;
                }
            }

            // place eye room at center
            PlaceNode(_layout.EyeRoomIndex, Vector2.zero, true);

            // place ring rooms
            for (int ring = 0; ring < _layout.RingCount; ring++)
            {
                var ringRooms = _layout.GetRingRooms(ring);
                if (ringRooms == null || ringRooms.Count == 0) continue;

                float radius = GetRingRadius(ring);
                float angleStep = 360f / ringRooms.Count;
                float startAngle = 90f;

                for (int i = 0; i < ringRooms.Count; i++)
                {
                    float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
                    Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    PlaceNode(ringRooms[i], pos, false);
                }
            }

            // draw connection lines
            DrawAllConnections();
        }

        private void ClearMap()
        {
            foreach (var kvp in _nodes)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            _nodes.Clear();
            _selectables.Clear();

            // destroy old connection lines
            for (int i = _mapContainer.childCount - 1; i >= 0; i--)
            {
                var child = _mapContainer.GetChild(i);
                if (child.GetComponent<MapSelectionNode>() == null)
                    Destroy(child.gameObject);
            }
        }

        private void PlaceNode(int roomIndex, Vector2 pos, bool isEyeRoom)
        {
            if (_nodes.ContainsKey(roomIndex)) return;

            var nodeObj = CreateNodeElement(roomIndex, isEyeRoom);
            var node = nodeObj.GetComponent<MapSelectionNode>();

            // get blueprint visuals for this room's eyewall
            Color? bpColor = null;
            Sprite bpIcon = null;
            var bp = GetBlueprint();
            if (bp != null && _layout != null)
            {
                int eyewall = _layout.GetRoom(roomIndex).eyewallIndex;
                bpColor = bp.GetColorForEyewall(eyewall);
                bpIcon = bp.GetIconForEyewall(eyewall);
            }

            node.Initialize(roomIndex, isEyeRoom, bpColor, bpIcon);

            RectTransform rt = nodeObj.GetComponent<RectTransform>();
            rt.SetParent(_mapContainer, false);
            rt.anchoredPosition = pos;

            _nodes[roomIndex] = node;
            _selectables.Add(nodeObj.GetComponent<Selectable>());
        }

        private float GetRingRadius(int ringIndex)
        {
            int totalRings = _layout.RingCount;
            if (totalRings <= 1) return mapRadius * 0.6f;
            return mapRadius * (0.35f + 0.65f * (float)ringIndex / (totalRings - 1));
        }

        // =====================================
        // connection lines
        // =====================================

        private void DrawAllConnections()
        {
            HashSet<(int, int)> drawn = new HashSet<(int, int)>();

            foreach (var room in _layout.AllRooms)
            {
                DrawLineIfConnected(room.roomIndex, room.leftRoomIndex, drawn);
                DrawLineIfConnected(room.roomIndex, room.rightRoomIndex, drawn);
                DrawLineIfConnected(room.roomIndex, room.inwardRoomIndex, drawn);
            }
        }

        private void DrawLineIfConnected(int from, int to, HashSet<(int, int)> drawn)
        {
            if (to < 0) return;

            var key = from < to ? (from, to) : (to, from);
            if (!drawn.Add(key)) return;

            if (!_nodes.ContainsKey(from) || !_nodes.ContainsKey(to)) return;

            Vector2 a = _nodes[from].GetComponent<RectTransform>().anchoredPosition;
            Vector2 b = _nodes[to].GetComponent<RectTransform>().anchoredPosition;

            bool isSelectablePath = (from == _currentRoomIndex || to == _currentRoomIndex) && IsHostOrSolo();

            CreateConnectionLine(a, b, isSelectablePath);
        }

        private void CreateConnectionLine(Vector2 from, Vector2 to, bool highlighted)
        {
            var lineObj = new GameObject("Connection", typeof(RectTransform), typeof(Image));
            var lineRt = lineObj.GetComponent<RectTransform>();
            lineRt.SetParent(_mapContainer, false);

            Vector2 diff = to - from;
            float distance = diff.magnitude;

            lineRt.anchoredPosition = from;
            lineRt.sizeDelta = new Vector2(distance, highlighted ? 3f : 2f);
            lineRt.pivot = new Vector2(0f, 0.5f);
            lineRt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);

            var lineImg = lineObj.GetComponent<Image>();
            lineImg.color = highlighted
                ? new Color(0f, 0.85f, 1f, 0.6f)
                : new Color(0.35f, 0.35f, 0.45f, 0.4f);
            lineImg.raycastTarget = false;
        }

        // =====================================
        // UI creation (procedural)
        // =====================================

        private void CreateUI()
        {
            // canvas
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999;

            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;

            // full-screen dark background (blocks clicks behind)
            var bgObj = CreateUIElement("Background", gameObject.transform);
            var bgRt = bgObj.GetComponent<RectTransform>();
            StretchFull(bgRt);
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.12f, 0.12f, 0.92f);
            bgImg.raycastTarget = true;

            // map container (centered, holds nodes and lines)
            var mapObj = CreateUIElement("MapContainer", gameObject.transform);
            _mapContainer = mapObj.GetComponent<RectTransform>();
            _mapContainer.anchorMin = new Vector2(0.5f, 0.5f);
            _mapContainer.anchorMax = new Vector2(0.5f, 0.5f);
            _mapContainer.sizeDelta = new Vector2(mapRadius * 2.6f, mapRadius * 2.6f);

            // faint circular background behind the map
            var mapBg = mapObj.AddComponent<Image>();
            mapBg.color = new Color(0.08f, 0.1f, 0.18f, 0.5f);
            mapBg.raycastTarget = false;

            // title (top center)
            CreateTitleUI();

            // close button (top right)
            CreateCloseButton();

            // status text (bottom)
            CreateStatusText();

            // legend (bottom right)
            CreateLegend();
        }

        private void CreateTitleUI()
        {
            var titleObj = CreateUIElement("Title", gameObject.transform);
            var titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -30f);
            titleRt.sizeDelta = new Vector2(400f, 50f);

            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "STORM MAP";
            if (titleFont != null) titleText.font = titleFont;
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.7f, 0.85f, 1f);
            titleText.raycastTarget = false;
        }

        private void CreateCloseButton()
        {
            var btnObj = CreateUIElement("CloseButton", gameObject.transform);
            var btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 1f);
            btnRt.anchorMax = new Vector2(1f, 1f);
            btnRt.anchoredPosition = new Vector2(-40f, -30f);
            btnRt.sizeDelta = new Vector2(36f, 36f);

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.3f, 0.3f, 0.4f, 0.8f);

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(Close);

            var btnTextObj = CreateUIElement("X", btnObj.transform);
            var btnTextRt = btnTextObj.GetComponent<RectTransform>();
            StretchFull(btnTextRt);
            var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "✕";
            btnText.fontSize = 18;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = new Color(0.8f, 0.8f, 0.9f);
            btnText.raycastTarget = false;
        }

        private TextMeshProUGUI _statusText;

        private void CreateStatusText()
        {
            var statusObj = CreateUIElement("StatusText", gameObject.transform);
            var statusRt = statusObj.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0.5f, 0f);
            statusRt.anchorMax = new Vector2(0.5f, 0f);
            statusRt.anchoredPosition = new Vector2(0f, 30f);
            statusRt.sizeDelta = new Vector2(500f, 40f);

            _statusText = statusObj.AddComponent<TextMeshProUGUI>();
            _statusText.fontSize = 16;
            _statusText.alignment = TextAlignmentOptions.Center;
            _statusText.color = new Color(0.6f, 0.75f, 0.9f);
            _statusText.raycastTarget = false;
        }

        private void CreateLegend()
        {
            var legendObj = CreateUIElement("Legend", gameObject.transform);
            var legendRt = legendObj.GetComponent<RectTransform>();
            legendRt.anchorMin = new Vector2(0f, 0f);
            legendRt.anchorMax = new Vector2(0f, 0f);
            legendRt.anchoredPosition = new Vector2(20f, 20f);
            legendRt.sizeDelta = new Vector2(220f, 90f);

            var legendBg = legendObj.AddComponent<Image>();
            legendBg.color = new Color(0.05f, 0.05f, 0.1f, 0.6f);
            legendBg.raycastTarget = false;

            string[] labels = { "Current", "Selectable", "Eye Room" };
            Color[] colors = {
                new Color(1f, 0.85f, 0.2f),
                new Color(0f, 0.85f, 1f),
                new Color(1f, 0.2f, 0.2f)
            };

            for (int i = 0; i < 3; i++)
            {
                // color dot
                var dotObj = CreateUIElement($"Dot_{i}", legendObj.transform);
                var dotRt = dotObj.GetComponent<RectTransform>();
                dotRt.anchorMin = new Vector2(0f, 1f);
                dotRt.anchorMax = new Vector2(0f, 1f);
                dotRt.anchoredPosition = new Vector2(12f, -14f - i * 26f);
                dotRt.sizeDelta = new Vector2(10f, 10f);
                var dotImg = dotObj.AddComponent<Image>();
                dotImg.color = colors[i];
                dotImg.raycastTarget = false;

                // label
                var lblObj = CreateUIElement($"Label_{i}", legendObj.transform);
                var lblRt = lblObj.GetComponent<RectTransform>();
                lblRt.anchorMin = new Vector2(0f, 1f);
                lblRt.anchorMax = new Vector2(0f, 1f);
                lblRt.anchoredPosition = new Vector2(28f, -14f - i * 26f);
                lblRt.sizeDelta = new Vector2(180f, 20f);
                var lblText = lblObj.AddComponent<TextMeshProUGUI>();
                lblText.text = labels[i];
                lblText.fontSize = 12;
                lblText.alignment = TextAlignmentOptions.Left;
                lblText.color = new Color(0.7f, 0.75f, 0.85f);
                lblText.raycastTarget = false;
            }
        }

        // =====================================
        // helpers
        // =====================================

        private GameObject CreateNodeElement(int roomIndex, bool isEyeRoom)
        {
            var obj = new GameObject($"Room_{roomIndex}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(MapSelectionNode));
            var img = obj.GetComponent<Image>();
            img.raycastTarget = true;

            // label
            var labelObj = CreateUIElement("Label", obj.transform);
            var labelRt = labelObj.GetComponent<RectTransform>();
            StretchFull(labelRt);
            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = isEyeRoom ? "★" : $"{roomIndex}";
            labelText.fontSize = isEyeRoom ? 16f : 12f;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;
            labelText.fontStyle = isEyeRoom ? FontStyles.Bold : FontStyles.Normal;
            labelText.raycastTarget = false;

            return obj;
        }

        private static GameObject CreateUIElement(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.GetComponent<RectTransform>().SetParent(parent, false);
            return obj;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            // fade in/out
            if (_canvasGroup != null && _fadeTarget >= 0f)
            {
                _canvasGroup.alpha = Mathf.MoveTowards(
                    _canvasGroup.alpha, _fadeTarget,
                    (1f / fadeInDuration) * Time.unscaledDeltaTime);

                if (_fadeTarget <= 0f && _canvasGroup.alpha <= 0.01f)
                {
                    IsOpen = false;
                }
            }

            // escape to close
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            IsOpen = false;
        }
    }
}
