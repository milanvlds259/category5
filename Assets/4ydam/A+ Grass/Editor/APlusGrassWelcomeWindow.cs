using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public sealed class APlusGrassWelcomeWindow : EditorWindow
{
    private const string WindowTitle = "Welcome to A+ Grass";
    private const string DocumentationUrl = "https://4ydam.com/assets/a+grass/getting-started/";
    private const string PackageRoot = "Assets/4ydam/A+ Grass";
    private const string WelcomeVersion = "1";

    private static Texture2D bannerTexture;
    private static Texture2D logoTexture;

    private GUIStyle bodyStyle;
    private GUIStyle titleStyle;
    private GUIStyle sectionStyle;

    static APlusGrassWelcomeWindow()
    {
        EditorApplication.delayCall += TryOpenOncePerProject;
    }

    [MenuItem("Tools/4ydam/A+ Grass/Welcome Window")]
    public static void OpenFromMenu()
    {
        ShowWindow();
    }

    private static void TryOpenOncePerProject()
    {
        if (Application.isBatchMode)
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!PackageAssetsExist())
        {
            return;
        }

        string projectKey = GetProjectWelcomeKey();
        if (EditorPrefs.GetBool(projectKey, false))
        {
            return;
        }

        ShowWindow();
        EditorPrefs.SetBool(projectKey, true);
    }

    private static void ShowWindow()
    {
        var window = GetWindow<APlusGrassWelcomeWindow>(true, WindowTitle, true);
        window.minSize = new Vector2(620f, 300f);
        window.maxSize = new Vector2(620f, 300f);
        window.ShowUtility();
        window.Focus();
    }

    private static string GetProjectWelcomeKey()
    {
        return $"4ydam.APlusGrass.WelcomeShown.{WelcomeVersion}.{Application.dataPath}";
    }

    private static bool PackageAssetsExist()
    {
        return AssetDatabase.IsValidFolder(PackageRoot) && FindTexture("A+Grass-Banner") != null;
    }

    private static Texture2D FindTexture(string filename)
    {
        string[] guids = AssetDatabase.FindAssets($"{filename} t:Texture2D", new[] { PackageRoot });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (System.IO.Path.GetFileNameWithoutExtension(path).Equals(filename, StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        return null;
    }

    private void OnEnable()
    {
        if (bannerTexture == null)
        {
            bannerTexture = FindTexture("A+Grass-Banner");
        }

        if (logoTexture == null)
        {
            logoTexture = FindTexture("A+Grass-Logo");
        }

    }

    private void OnGUI()
    {
        InitializeStyles();

        EditorGUILayout.Space(12f);
        DrawBanner();
        EditorGUILayout.Space(12f);

        GUILayout.BeginVertical(sectionStyle);
        GUILayout.Label("A+ Grass", titleStyle);
        GUILayout.Label(
            "Welcome to A+ Grass! The texture sampling solution for stylized grass.\n\n" +
            "To get started, check out the documentation with video tutorials for setup instructions to make the most of A+ Grass in your project.",
            bodyStyle);

        GUILayout.Space(14f);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Open Documentation", GUILayout.Height(32f), GUILayout.Width(180f)))
        {
            Application.OpenURL(DocumentationUrl);
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Close", GUILayout.Height(32f), GUILayout.Width(100f)))
        {
            Close();
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawBanner()
    {
        var bannerRect = GUILayoutUtility.GetRect(0, 75, GUILayout.ExpandWidth(true));
        bannerRect.x += 2f;
        bannerRect.width -= 4f;

        EditorGUI.DrawRect(bannerRect, new Color(0f, 0f, 0f, 0.18f));

        if (bannerTexture != null)
        {
            GUI.DrawTexture(bannerRect, bannerTexture, ScaleMode.ScaleAndCrop);
        }

        GUI.BeginClip(bannerRect);
        {
            if (logoTexture != null)
            {
                float logoAspect = logoTexture.width / (float)logoTexture.height;
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
                GUI.DrawTexture(logoRect, logoTexture, ScaleMode.ScaleToFit);
            }
        }
        GUI.EndClip();
    }

    private void InitializeStyles()
    {
        if (bodyStyle != null)
        {
            return;
        }

        sectionStyle = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(16, 16, 16, 16),
            margin = new RectOffset(12, 12, 0, 12)
        };

        titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 22,
            wordWrap = true
        };

        bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            fontSize = 12,
            richText = false
        };
    }
}