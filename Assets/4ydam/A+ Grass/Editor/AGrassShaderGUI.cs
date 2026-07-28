using UnityEngine;
using UnityEditor;

public class AGrassShaderGUI : ShaderGUI
{
    private Texture2D _bannerTex;
    private Texture2D _logoTex;
    private bool _texturesLoaded;
    private GUIStyle _sectionBoxStyle;
    private GUIStyle _headerLabelStyle;
    private GUIStyle _infoBadgeStyle;
    private bool _stylesInitialized;
    private Texture2D _sectionBgTex;
    private const string DocUrl = "https://4ydam.com/assets/a+grass/overview/";

    private static readonly Color SeparatorColor = new Color(58 / 255f, 58 / 255f, 58 / 255f, 1f);
    private static readonly Color SectionBgColor = new Color(0f, 0f, 0f, 0.2f);

    private void LoadBrandingTextures()
    {
        if (_texturesLoaded) return;
        _bannerTex = AGrassBannerDrawer.FindTexture("A+Grass-Banner");
        _logoTex   = AGrassBannerDrawer.FindTexture("A+Grass-Logo");
        _texturesLoaded = true;
    }

    private void InitStyles()
    {
        if (_stylesInitialized
            && _sectionBoxStyle  != null
            && _sectionBgTex     != null) return;

        _sectionBgTex = new Texture2D(1, 1);
        _sectionBgTex.hideFlags = HideFlags.DontSave;
        _sectionBgTex.SetPixel(0, 0, SectionBgColor);
        _sectionBgTex.Apply();

        _sectionBoxStyle = new GUIStyle
        {
            padding = new RectOffset(3, 6, 5, 8),
            margin  = new RectOffset(0, 5, 0, 5)
        };
        _sectionBoxStyle.normal.background = _sectionBgTex;

        _headerLabelStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 20,
            fontStyle = FontStyle.Bold,
            margin    = new RectOffset(8, 0, 5, 20)
        };
        _headerLabelStyle.normal.textColor = Color.white;
        _headerLabelStyle.hover.textColor = Color.white;
        _headerLabelStyle.active.textColor = Color.white;
        _headerLabelStyle.focused.textColor = Color.white;
        _headerLabelStyle.onNormal.textColor = Color.white;
        _headerLabelStyle.onHover.textColor = Color.white;
        _headerLabelStyle.onActive.textColor = Color.white;
        _headerLabelStyle.onFocused.textColor = Color.white;

        _infoBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding   = new RectOffset(0, 0, 0, 0),
            margin    = new RectOffset(10, 10, 0, 7)
        };
        _infoBadgeStyle.normal.textColor = Color.black;
        _infoBadgeStyle.hover.textColor = Color.black;
        _infoBadgeStyle.active.textColor = Color.black;
        _infoBadgeStyle.focused.textColor = Color.black;
        _infoBadgeStyle.onNormal.textColor = Color.black;
        _infoBadgeStyle.onHover.textColor = Color.black;
        _infoBadgeStyle.onActive.textColor = Color.black;
        _infoBadgeStyle.onFocused.textColor = Color.black;

        _stylesInitialized = true;
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        LoadBrandingTextures();
        InitStyles();

        DrawBanner();
        EditorGUILayout.Space(5);

        GUILayout.BeginVertical(_sectionBoxStyle);
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(8f);
                DrawRoundInfoBadge(14f,
                    "Shader for the A+ Grass system by 4ydam. Assign this material to your grass mesh renderer.");
                GUILayout.Space(6f);
                GUILayout.Label("A+ Grass Shader", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Documentation", GUILayout.Width(108)))
                    Application.OpenURL(DocUrl);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();

        MaterialProperty receiveShadowsProp = FindProperty("_ReceiveShadows", properties);

            GUILayout.BeginVertical(_sectionBoxStyle);
            {
                GUILayout.Label("Base Texture", _headerLabelStyle);
                DrawPropertyRow(materialEditor, FindProperty("_TextureSample", properties),
                    "Texture Sample", "The main grass blade texture (RGBA). Alpha is used as the cutout mask. Whatever you input in here will render as your grass.", true);
                DrawSeparator();
                DrawPropertyRow(materialEditor, FindProperty("_TextureRamp", properties),
                    "Texture Ramp", "Ramp texture applied for better color application along the blade, reactive to the lighting.", true);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(_sectionBoxStyle);
            {
                GUILayout.Label("Colour", _headerLabelStyle);
                DrawPropertyRow(materialEditor, FindProperty("_ColorTint", properties),
                    "Colour Tint", "The base colour of the grass.", true, 0f, Color.white, true);
                DrawSeparator();

                var useGradientProp = FindProperty("_UseGradient", properties);
                DrawPropertyRow(materialEditor, useGradientProp,
                    "Use Gradient", "Enable a vertical gradient to give the blade a two-tone look from base to tip.");

                if (useGradientProp.floatValue > 0.5f)
                {
                    DrawPropertyRow(materialEditor, FindProperty("_GradientTopColor", properties),
                        "Gradient Top Colour", "Colour at the tip of the grass blade.", true, 0f, Color.white, true);
                    DrawPropertyRow(materialEditor, FindProperty("_GradientBottomColor", properties),
                        "Gradient Bottom Colour", "Colour at the bottom of the grass blade.", true, 0f, new Color(0.5f, 0.5f, 0.5f, 1f), true);
                    DrawPropertyRow(materialEditor, FindProperty("_GradientOffset", properties),
                        "Gradient Offset", "Shifts the gradient boundary up or down the blade.", true, 0f);
                    DrawPropertyRow(materialEditor, FindProperty("_GradientContrast", properties),
                        "Gradient Contrast", "Controls how sharply the two gradient colours transition.", true, 1f);
                }
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(_sectionBoxStyle);
            {
                GUILayout.Label("Colour Variation", _headerLabelStyle);
                DrawPropertyRow(materialEditor, FindProperty("_ColorNoiseTexture", properties),
                    "Colour Noise Texture", "Noise texture applied to the grass to add colour variation.", true);
                DrawPropertyRow(materialEditor, FindProperty("_ColorNoiseScale", properties),
                    "Colour Noise Scale", "World-space scale of the colour noise. Lower = larger patches.", true, 0.1f);
                DrawPropertyRow(materialEditor, FindProperty("_ColorNoiseStrength", properties),
                    "Colour Noise Strength", "The strength of the colour noise effect.", true, 0.6f);
                DrawPropertyRow(materialEditor, FindProperty("_ColorNoiseLowColor", properties),
                    "Colour Noise Low Colour", "Tint applied where the colour noise is darkest.", true, 0f, Color.white, true);
                DrawPropertyRow(materialEditor, FindProperty("_ColorNoiseHighColor", properties),
                    "Colour Noise High Colour", "Tint applied where the colour noise is brightest.", true, 0f, Color.white, true);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(_sectionBoxStyle);
            {
                GUILayout.Label("Interaction", _headerLabelStyle);
                DrawPropertyRow(materialEditor, FindProperty("_InteractionStrength", properties),
                    "Interaction Strength", "How intensely the grass bends in response to the interactor.", true, 0.2f);
                DrawPropertyRow(materialEditor, FindProperty("_PushDownAmount", properties),
                    "Push Down Amount", "How far the grass is pressed down during interaction.", true, 0.1f);

                DrawSeparator();

                DrawPropertyRow(materialEditor, FindProperty("_BendPivotOffset", properties),
                    "Bend Pivot Offset", "Offsets the pivot point used when bending the grass blade. 0 bends from the base, 1 bends from the tip.", true, 0f);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(_sectionBoxStyle);
            {
                GUILayout.Label("Trail", _headerLabelStyle);
                var useGradientForTint = FindProperty("_UseGradient", properties);
                if (useGradientForTint.floatValue > 0.5f)
                {
                    DrawPropertyRow(materialEditor, FindProperty("_TrailTintTopColor", properties),
                        "Trail Top Colour", "Colour blended onto the blade tip where interaction pushes the grass down.", true, 0f, Color.black, true);
                    DrawPropertyRow(materialEditor, FindProperty("_TrailTintColor", properties),
                        "Trail Bottom Colour", "Colour blended onto the blade base where interaction pushes the grass down.", true, 0f, Color.black, true);
                }
                else
                {
                    DrawPropertyRow(materialEditor, FindProperty("_TrailTintColor", properties),
                        "Trail Colour", "Colour blended onto the grass where it is pushed down by interaction. Stronger at blade tips.", true, 0f, Color.black, true);
                }
                DrawSeparator();
                DrawPropertyRow(materialEditor, FindProperty("_TrailTintStrength", properties),
                    "Trail Strength", "Multiplier for how strongly the trail tint colour is applied.", true, 1f);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(_sectionBoxStyle);
            {
                GUILayout.Label("Wind", _headerLabelStyle);
                DrawPropertyRow(materialEditor, FindProperty("_WindNoiseTexture", properties),
                    "Wind Noise Texture", "Primary wind noise texture that drives horizontal blade movement.", true);
                DrawSeparator();
                DrawPropertyRow(materialEditor, FindProperty("_WindScroll", properties),
                    "Wind Scroll", "Scrolling speed of the primary wind noise texture.", true, 0.05f);
                DrawPropertyRow(materialEditor, FindProperty("_WindJitter", properties),
                    "Wind Jitter", "Intensity of high-frequency turbulence on the primary wind layer.", true, 0.3f);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(_sectionBoxStyle);
            {
                GUILayout.Label("Secondary Wind", _headerLabelStyle);
                DrawPropertyRow(materialEditor, FindProperty("_WindNoiseTexture2", properties),
                    "2nd Wind Noise Texture", "Secondary wind noise texture blended with the primary for richer motion.", true);
                DrawSeparator();
                DrawPropertyRow(materialEditor, FindProperty("_WindScroll2", properties),
                    "2nd Wind Scroll", "Scrolling speed of the secondary wind noise texture.", true, 0.3f);
                DrawPropertyRow(materialEditor, FindProperty("_WindJitter2", properties),
                    "2nd Wind Jitter", "Intensity of high-frequency turbulence on the secondary wind layer.", true, 0.5f);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(_sectionBoxStyle);
            {
                DrawPropertyRow(materialEditor, FindProperty("_WindBlend", properties),
                    "Winds Blend", "Balance between the primary and secondary wind layers (0 is fully primary, 1 is fully secondary)", true, 0f);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(_sectionBoxStyle);
            {
                GUILayout.Label("Perspective Correction", _headerLabelStyle);

                var usePerspCorrProp = FindProperty("_UsePerspectiveCorrection", properties);
                DrawPropertyRow(materialEditor, usePerspCorrProp,
                    "Use Perspective Correction", "Enable perspective correction to make grass look fuller when viewed from a top-down camera angle.");

                if (usePerspCorrProp.floatValue > 0.5f)
                {
                    DrawPropertyRow(materialEditor, FindProperty("_PerspectiveCorrectionStrength", properties),
                        "Correction Strength", "Overall strength of the perspective correction effect. Higher values push blade tips further towards the camera.", true, 0.35f);
                    DrawPropertyRow(materialEditor, FindProperty("_PerspectiveTopDownStart", properties),
                        "Correction Radius", "Camera angle threshold where correction begins. Lower values start the effect at shallower angles; higher values restrict it to steeper top-down views.", true, 0.45f);
                    DrawPropertyRow(materialEditor, FindProperty("_PerspectiveHeightStart", properties),
                        "Height Start", "Normalized blade height where perspective correction begins. Increase this to keep lower blade sections less affected on taller grass.", true, 0f);
                    DrawPropertyRow(materialEditor, FindProperty("_PerspectiveMaxOffset", properties),
                        "Max Offset", "Maximum world-space distance a vertex can be pushed by the correction. Acts as a safety clamp to prevent over-bending.", true, 0.2f);
                }
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(_sectionBoxStyle);
            {
                GUILayout.Label("Distance Fade", _headerLabelStyle);

                var useDistanceFadeProp = FindProperty("_UseDistanceFade", properties);
                DrawPropertyRow(materialEditor, useDistanceFadeProp,
                    "Use Distance Fade", "Enable distance-based alpha fade for grass chunks based on main camera distance.");

                if (useDistanceFadeProp.floatValue > 0.5f)
                {
                    DrawPropertyRow(materialEditor, FindProperty("_DistanceFadeMode", properties),
                        "Fade Mode", "Smooth Fade uses a soft transition between Fade Start and Fade End. Dither Fade uses screen-space dithering for a less noticeable fade-out.");
                    DrawPropertyRow(materialEditor, FindProperty("_DistanceFadeStart", properties),
                        "Fade Start", "Distance from camera where fading starts.", true, 60f);
                    DrawPropertyRow(materialEditor, FindProperty("_DistanceFadeEnd", properties),
                        "Fade End", "Distance from camera where grass is fully faded. Keep this greater than Fade Start.", true, 90f);
                    DrawPropertyRow(materialEditor, FindProperty("_CullAtFadeEnd", properties),
                        "Cull At Fade End", "When enabled, grass is hard-culled at and beyond Fade End instead of only fading to near-zero alpha.");
                }
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(_sectionBoxStyle);
            {
                GUILayout.Label("Rendering", _headerLabelStyle);

                var cullProp = FindProperty("_CullMode", properties);
                EditorGUILayout.Space(2f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(8f);
                DrawRoundInfoBadge(14f, "Controls which face of the mesh is rendered. Both renders front and back faces, Front renders only front-facing triangles, Back renders only back-facing triangles.");
                GUILayout.Space(6f);
                int cullValue = (int)cullProp.floatValue;
                string[] renderFaceOptions = { "Both", "Front", "Back" };
                int[] renderFaceValues = { 0, 2, 1 };
                int selectedIndex = System.Array.IndexOf(renderFaceValues, cullValue);
                if (selectedIndex < 0) selectedIndex = 0;
                EditorGUI.BeginChangeCheck();
                selectedIndex = EditorGUILayout.Popup("Render Face", selectedIndex, renderFaceOptions);
                if (EditorGUI.EndChangeCheck())
                {
                    cullProp.floatValue = renderFaceValues[selectedIndex];
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2f);


                DrawPropertyRow(materialEditor, FindProperty("_AlphaCutoff", properties),
                    "Alpha Cutoff", "Controls the threshold below which pixels are discarded based on the texture alpha. Higher values cut away more of the blade.", true, 0.5f);

                DrawPropertyRow(materialEditor, receiveShadowsProp,
                    "Receive Shadows", "When enabled, the grass receives shadows from other objects in the scene (If 'Cast Shadows' is disabled on the mesh in Mesh Renderer, this setting will do nothing)");
            }
            GUILayout.EndVertical();

        EditorGUILayout.Space(8);
        materialEditor.RenderQueueField();
        materialEditor.EnableInstancingField();
        materialEditor.DoubleSidedGIField();

        foreach (var obj in materialEditor.targets)
        {
            var mat = obj as Material;
            if (mat != null)
            {
                bool receiveShadows = receiveShadowsProp.floatValue >= 0.5f;
                if (receiveShadows)
                    mat.DisableKeyword("_RECEIVE_SHADOWS_OFF");
                else
                    mat.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            }
        }
    }

    private void DrawBanner()
    {
        var bannerRect = GUILayoutUtility.GetRect(0, 75, GUILayout.ExpandWidth(true));
        bannerRect.x     += 2;
        bannerRect.width -= 4;

        if (_bannerTex != null)
            GUI.DrawTexture(bannerRect, _bannerTex, ScaleMode.ScaleAndCrop);

        GUI.BeginClip(bannerRect);
        {
            if (_logoTex != null)
            {
                float logoAspect = _logoTex.width / (float)_logoTex.height;
                float maxLogoHeight = 74f;
                float maxLogoWidth = maxLogoHeight * logoAspect;
                float availableWidth = bannerRect.width * 0.8f;
                float logoWidth = Mathf.Min(maxLogoWidth, availableWidth);
                float logoHeight = logoWidth / logoAspect;
                var logoRect = new Rect(
                    (bannerRect.width - logoWidth) * 0.5f,
                    (bannerRect.height - logoHeight) * 0.5f,
                    logoWidth,
                    logoHeight);
                GUI.DrawTexture(logoRect, _logoTex, ScaleMode.ScaleToFit);
            }
        }
        GUI.EndClip();
    }

    private void DrawPropertyRow(MaterialEditor materialEditor,
        MaterialProperty prop, string label, string tooltip = "", bool showReset = false, float resetValue = 0f, Color resetColor = default, bool useColorReset = false)
    {
        EditorGUILayout.Space(2f);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(8f);
        DrawRoundInfoBadge(14f, tooltip);
        GUILayout.Space(6f);
        materialEditor.ShaderProperty(prop, new GUIContent(label, tooltip));
        if (showReset)
        {
            if (GUILayout.Button("R", GUILayout.Width(20f), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                if (prop.propertyType == UnityEngine.Rendering.ShaderPropertyType.Texture)
                {
                    prop.textureScaleAndOffset = new Vector4(1f, 1f, 0f, 0f);
                }
                else if (prop.propertyType == UnityEngine.Rendering.ShaderPropertyType.Color)
                {
                    prop.colorValue = useColorReset ? resetColor : Color.white;
                }
                else
                {
                    prop.floatValue = resetValue;
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2f);
    }

    private void DrawRoundInfoBadge(float size, string tooltip)
    {
        float lineHeight = Mathf.Max(EditorGUIUtility.singleLineHeight, size);
        var slotRect = GUILayoutUtility.GetRect(
            size,
            lineHeight,
            GUILayout.Width(size),
            GUILayout.Height(lineHeight));

        var badgeRect = new Rect(
            slotRect.x,
            slotRect.y + ((lineHeight - size) * 0.5f) + 1f,
            size,
            size);

        if (Event.current.type == EventType.Repaint)
        {
            Vector2 center = badgeRect.center;
            float radius = Mathf.Min(badgeRect.width, badgeRect.height) * 0.5f;

            Handles.BeginGUI();
            Handles.color = Color.white;
            Handles.DrawSolidDisc(center, Vector3.forward, radius);
            Handles.EndGUI();
        }

        GUI.Label(badgeRect, "i", _infoBadgeStyle);
        if (!string.IsNullOrEmpty(tooltip))
        {
            GUI.Label(badgeRect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
        }
    }

    private static void DrawSeparator()
    {
        var rect = GUILayoutUtility.GetRect(8f, 19f, GUILayout.ExpandWidth(true));
        rect.x      += 8f;
        rect.width  -= 4f;
        rect.y      += 8f;
        rect.height  = 3f;
        EditorGUI.DrawRect(rect, SeparatorColor);
    }
}
