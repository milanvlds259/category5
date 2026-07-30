using UnityEngine;
using UnityEditor;

public static class AGrassBannerDrawer
{
    private static Texture2D _bannerTex;
    private static Texture2D _logoTex;
    private static bool _loaded;

    public static Texture2D FindTexture(string filename)
    {
        string[] guids = AssetDatabase.FindAssets($"{filename} t:Texture2D");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path)
                    .Equals(filename, System.StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        return null;
    }

    private static void LoadTextures()
    {
        if (_loaded && _bannerTex != null && _logoTex != null) return;
        _bannerTex = FindTexture("A+Grass-Banner");
        _logoTex = FindTexture("A+Grass-Logo");
        _loaded = _bannerTex != null && _logoTex != null;
    }

    public static void DrawBanner()
    {
        LoadTextures();

        var bannerRect = GUILayoutUtility.GetRect(0, 75, GUILayout.ExpandWidth(true));
        bannerRect.x += 2;
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
}
