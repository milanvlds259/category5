using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

[CustomEditor(typeof(GrassManager))]
public class GrassManagerEditor : Editor
{
    public VisualTreeAsset visualTreeAsset;
    private const string DocumentationUrl = "https://4ydam.com/assets/a+grass/getting-started/";
    private enum ManagerTab { Interaction, Weather }
    private ManagerTab _activeTab = ManagerTab.Interaction;

    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();

        if (visualTreeAsset == null)
        {
            root.Add(new HelpBox("Assign a VisualTreeAsset to GrassManagerEditor.", HelpBoxMessageType.Warning));
            return root;
        }

        root.Add(new IMGUIContainer(AGrassBannerDrawer.DrawBanner));

        visualTreeAsset.CloneTree(root);

        var tabBar = new Toolbar();
        tabBar.style.alignSelf = Align.Stretch;
        tabBar.style.width = Length.Percent(100f);
        tabBar.style.marginBottom = 6f;

        var interactionTab = new ToolbarToggle { text = "⚙ Interaction" };
        var weatherTab = new ToolbarToggle { text = "⛈ Weather" };
        Color selectedTabColor = Color.white;
        Color normalTabColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        Color tabTextColor = Color.black;
        interactionTab.style.flexGrow = 1f;
        interactionTab.style.flexBasis = 0f;
        interactionTab.style.height = 28f;
        interactionTab.style.unityTextAlign = TextAnchor.MiddleCenter;
        weatherTab.style.flexGrow = 1f;
        weatherTab.style.flexBasis = 0f;
        weatherTab.style.height = 28f;
        weatherTab.style.unityTextAlign = TextAnchor.MiddleCenter;
        interactionTab.value = true;
        weatherTab.value = false;
        IMGUIContainer runtimeInteractionBar = null;
        UpdateTabStyles(interactionTab, weatherTab, selectedTabColor, normalTabColor, tabTextColor);
        IMGUIContainer runtimeCpuBar = null;

        interactionTab.RegisterValueChangedCallback(evt =>
        {
            if (!evt.newValue)
                return;

            _activeTab = ManagerTab.Interaction;
            weatherTab.SetValueWithoutNotify(false);
            UpdateTabStyles(interactionTab, weatherTab, selectedTabColor, normalTabColor, tabTextColor);
            ApplyTabState(root, runtimeInteractionBar, runtimeCpuBar);
        });

        weatherTab.RegisterValueChangedCallback(evt =>
        {
            if (!evt.newValue)
                return;

            _activeTab = ManagerTab.Weather;
            interactionTab.SetValueWithoutNotify(false);
            UpdateTabStyles(interactionTab, weatherTab, selectedTabColor, normalTabColor, tabTextColor);
            ApplyTabState(root, runtimeInteractionBar, runtimeCpuBar);
        });

        tabBar.Add(interactionTab);
        tabBar.Add(weatherTab);

        var header = root.Q<VisualElement>("Header_VisualElement");
        if (header != null)
        {
            int headerIndex = root.IndexOf(header);
            root.Insert(headerIndex + 1, tabBar);
        }
        else
        {
            root.Add(tabBar);
        }

        root.Bind(serializedObject);

        runtimeInteractionBar = new IMGUIContainer(() =>
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Interaction CPU Usage", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("CPU usage is shown during Play Mode.", MessageType.Info);
            }
            else
            {
                var manager = target as GrassManager;
                if (manager != null)
                {
                    float cpu01 = Mathf.Clamp01(manager.InteractionCpuUsagePercent / 100f);
                    string cpuLabel = string.Format("CPU {0:0.0}%  ({1:0.000} ms/frame)", manager.InteractionCpuUsagePercent, manager.InteractionCpuUsageMs);
                    Rect cpuRect = GUILayoutUtility.GetRect(18f, 18f, "TextField");
                    EditorGUI.ProgressBar(cpuRect, cpu01, cpuLabel);
                }
            }

            EditorGUILayout.EndVertical();
        });
        runtimeInteractionBar.schedule.Execute(() => runtimeInteractionBar.MarkDirtyRepaint()).Every(200);
        root.Add(runtimeInteractionBar);

        runtimeCpuBar = new IMGUIContainer(() =>
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Culling CPU Usage", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("CPU usage is shown during Play Mode.", MessageType.Info);
            }
            else
            {
                var manager = target as GrassManager;
                if (manager != null)
                {
                    float usage01 = Mathf.Clamp01(manager.CullingCpuUsagePercent / 100f);
                    string barLabel = string.Format("{0:0.0}%  ({1:0.000} ms/frame)", manager.CullingCpuUsagePercent, manager.CullingCpuUsageMs);
                    Rect barRect = GUILayoutUtility.GetRect(18f, 18f, "TextField");
                    EditorGUI.ProgressBar(barRect, usage01, barLabel);
                    EditorGUILayout.Space(2f);
                    EditorGUILayout.LabelField(
                        string.Format("Tracked Renderers: {0}   Culled: {1}", manager.TrackedGrassRendererCount, manager.CulledGrassRendererCount),
                        EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        });
        runtimeCpuBar.schedule.Execute(() => runtimeCpuBar.MarkDirtyRepaint()).Every(200);
        root.Add(runtimeCpuBar);
        ApplyTabState(root, runtimeInteractionBar, runtimeCpuBar);

        var trueCullingRow = root.Q<VisualElement>("EnableRendererDistanceCulling_VisualElement");
        var trueCullingToggle = trueCullingRow?.Q<Toggle>();
        bool trueCullingEnabled = serializedObject.FindProperty("enableRendererDistanceCulling").boolValue;
        ApplyTrueCullingState(root, runtimeCpuBar, trueCullingEnabled);

        if (trueCullingToggle != null)
        {
            trueCullingToggle.RegisterValueChangedCallback(evt =>
            {
                ApplyTrueCullingState(root, runtimeCpuBar, evt.newValue);
            });
        }

        var documentationButton = root.Q<Button>("documentationButton");
        if (documentationButton != null)
        {
            documentationButton.clicked += () => Application.OpenURL(DocumentationUrl);
        }

        var recoverySpeedResetButton = root.Q<VisualElement>("RecoverySpeed_VisualElement")?.Q<Button>();
        if (recoverySpeedResetButton != null)
        {
            recoverySpeedResetButton.clicked += () =>
            {
                serializedObject.FindProperty("recoverySpeed").floatValue = 0.9f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        var mapWorldSizeResetButton = root.Q<VisualElement>("MapWorldSize_VisualElement")?.Q<Button>();
        if (mapWorldSizeResetButton != null)
        {
            mapWorldSizeResetButton.clicked += () =>
            {
                serializedObject.FindProperty("interactionMapWorldSize").floatValue = 32f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        var mapRecenterThresholdResetButton = root.Q<VisualElement>("MapRecenterThreshold_VisualElement")?.Q<Button>();
        if (mapRecenterThresholdResetButton != null)
        {
            mapRecenterThresholdResetButton.clicked += () =>
            {
                serializedObject.FindProperty("recenterThresholdPercent").floatValue = 0.25f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        var recoveryDelayResetButton = root.Q<VisualElement>("RecoveryDelay_VisualElement")?.Q<Button>();
        if (recoveryDelayResetButton != null)
        {
            recoveryDelayResetButton.clicked += () =>
            {
                serializedObject.FindProperty("recoveryDelay").floatValue = 0.35f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        var wind1MultiplierResetButton = root.Q<VisualElement>("wind1Multi_VisualElement")?.Q<Button>();
        if (wind1MultiplierResetButton != null)
        {
            wind1MultiplierResetButton.clicked += () =>
            {
                serializedObject.FindProperty("globalWind1Multiplier").floatValue = 1f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        var wind2MultiplierResetButton = root.Q<VisualElement>("wind2Multi_VisualElement")?.Q<Button>();
        if (wind2MultiplierResetButton != null)
        {
            wind2MultiplierResetButton.clicked += () =>
            {
                serializedObject.FindProperty("globalWind2Multiplier").floatValue = 1f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        var windDirectionResetButton = root.Q<VisualElement>("windDirection_VisualElement")?.Q<Button>();
        if (windDirectionResetButton != null)
        {
            windDirectionResetButton.clicked += () =>
            {
                serializedObject.FindProperty("globalWindDirection").floatValue = 45f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        var windTransitionSpeedResetButton = root.Q<VisualElement>("windTransitionSpeed_VisualElement")?.Q<Button>();
        if (windTransitionSpeedResetButton != null)
        {
            windTransitionSpeedResetButton.clicked += () =>
            {
                serializedObject.FindProperty("windTransitionSpeed").floatValue = 0.1f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        var rendererCullUpdateIntervalResetButton = root.Q<VisualElement>("RendererCullUpdateInterval_VisualElement")?.Q<Button>();
        if (rendererCullUpdateIntervalResetButton != null)
        {
            rendererCullUpdateIntervalResetButton.clicked += () =>
            {
                serializedObject.FindProperty("rendererCullUpdateInterval").floatValue = 0.2f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        var rendererCacheRefreshIntervalResetButton = root.Q<VisualElement>("RendererCacheRefreshInterval_VisualElement")?.Q<Button>();
        if (rendererCacheRefreshIntervalResetButton != null)
        {
            rendererCacheRefreshIntervalResetButton.clicked += () =>
            {
                serializedObject.FindProperty("rendererCacheRefreshInterval").floatValue = 1.5f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        var rendererCullPaddingResetButton = root.Q<VisualElement>("RendererCullPadding_VisualElement")?.Q<Button>();
        if (rendererCullPaddingResetButton != null)
        {
            rendererCullPaddingResetButton.clicked += () =>
            {
                serializedObject.FindProperty("rendererCullPadding").floatValue = 0.5f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        return root;
    }

    private void ApplyTabState(VisualElement root, IMGUIContainer runtimeInteractionBar, IMGUIContainer runtimeCpuBar)
    {
        var interactionArea = root.Q<VisualElement>("InteractionAreaSettings_VisualElement");
        var windSettings = root.Q<VisualElement>("WindSettings_VisualElement");
        var recoverySettings = root.Q<VisualElement>("RevoverySettings_VisualElement");
        var cullingSettings = root.Q<VisualElement>("TrueCullingSettings_VisualElement");
        var gizmosSettings = root.Q<VisualElement>("Gizmos_VisualElement");

        bool showInteraction = _activeTab == ManagerTab.Interaction;
        bool showWeather = _activeTab == ManagerTab.Weather;
        bool trueCullingEnabled = serializedObject.FindProperty("enableRendererDistanceCulling").boolValue;

        if (interactionArea != null)
            interactionArea.style.display = showInteraction ? DisplayStyle.Flex : DisplayStyle.None;
        if (recoverySettings != null)
            recoverySettings.style.display = showInteraction ? DisplayStyle.Flex : DisplayStyle.None;
        if (cullingSettings != null)
            cullingSettings.style.display = showInteraction ? DisplayStyle.Flex : DisplayStyle.None;
        if (gizmosSettings != null)
            gizmosSettings.style.display = showWeather ? DisplayStyle.Flex : DisplayStyle.None;

        if (windSettings != null)
            windSettings.style.display = showWeather ? DisplayStyle.Flex : DisplayStyle.None;

        if (runtimeInteractionBar != null)
            runtimeInteractionBar.style.display = showInteraction ? DisplayStyle.Flex : DisplayStyle.None;

        if (runtimeCpuBar != null)
            runtimeCpuBar.style.display = (showInteraction && trueCullingEnabled) ? DisplayStyle.Flex : DisplayStyle.None;

        ApplyTrueCullingState(root, runtimeCpuBar, trueCullingEnabled);
    }

    private void ApplyTrueCullingState(VisualElement root, IMGUIContainer runtimeCpuBar, bool enabled)
    {
        bool showInteraction = _activeTab == ManagerTab.Interaction;

        var rendererCullUpdateInterval = root.Q<VisualElement>("RendererCullUpdateInterval_VisualElement");
        var rendererCacheRefreshInterval = root.Q<VisualElement>("RendererCacheRefreshInterval_VisualElement");
        var rendererCullPadding = root.Q<VisualElement>("RendererCullPadding_VisualElement");

        DisplayStyle detailDisplay = (showInteraction && enabled) ? DisplayStyle.Flex : DisplayStyle.None;

        if (rendererCullUpdateInterval != null)
            rendererCullUpdateInterval.style.display = detailDisplay;
        if (rendererCacheRefreshInterval != null)
            rendererCacheRefreshInterval.style.display = detailDisplay;
        if (rendererCullPadding != null)
            rendererCullPadding.style.display = detailDisplay;

        if (runtimeCpuBar != null)
            runtimeCpuBar.style.display = detailDisplay;
    }

    private static void UpdateTabStyles(ToolbarToggle interactionTab, ToolbarToggle weatherTab, Color selectedColor, Color normalColor, Color textColor)
    {
        interactionTab.style.backgroundColor = interactionTab.value ? selectedColor : normalColor;
        weatherTab.style.backgroundColor = weatherTab.value ? selectedColor : normalColor;
        interactionTab.style.color = textColor;
        weatherTab.style.color = textColor;
    }
}
