using System.Collections.Generic;
using System.IO;
using BepInEx;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BigWalkDLSS;

internal static class MenuTheme
{
    internal static readonly Color Background = new(.09f, .11f, .09f, 1);
    internal static readonly Color Surface = new(.14f, .17f, .14f, 1);
    internal static readonly Color Raised = new(.20f, .23f, .19f, 1);
    internal static readonly Color Ochre = new(.84f, .67f, .39f, 1);
    internal static readonly Color Earth = new(.69f, .36f, .25f, 1);
    internal static readonly Color Text = new(.96f, .92f, .86f, 1);
    internal static readonly Color Muted = new(.77f, .73f, .66f, 1);
    internal static readonly Color Highlight = new(.28f, .24f, .17f, 1);
    private static readonly Dictionary<Color, Texture2D> Textures = new();
    private static Texture2D? _artwork;
    private static bool _artworkTried;

    internal static Texture2D? Artwork
    {
        get
        {
            if (_artworkTried) return _artwork;
            _artworkTried = true;
            Texture2D? texture = null;
            try
            {
                string path = Path.Combine(Paths.PluginPath, "BigWalkDLSS", "ui-landscape.png");
                if (!File.Exists(path)) return null;
                texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                if (ImageConversion.LoadImage(texture, new Il2CppStructArray<byte>(File.ReadAllBytes(path)), true))
                { _artwork = texture; texture = null; }
            }
            catch (System.Exception error) { Plugin.Instance.Log.LogWarning("Menu artwork could not be loaded: " + error.GetType().Name); }
            finally { if (texture != null) UnityEngine.Object.Destroy(texture); }
            return _artwork;
        }
    }

    internal static GUIStyle Button(GUIStyle original, bool selected = false)
    {
        var style = new GUIStyle(original)
        {
            fontSize = 15, fontStyle = FontStyle.Normal,
            border = new RectOffset(5, 5, 5, 5), padding = new RectOffset(8, 8, 4, 4)
        };
        style.normal.background = Texture(selected ? Ochre : Raised);
        style.normal.textColor = selected ? Background : Text;
        style.hover.background = Texture(selected ? Ochre : Highlight);
        style.hover.textColor = selected ? Background : Ochre;
        style.active.background = Texture(Ochre);
        style.active.textColor = Background;
        style.focused.background = Texture(Highlight);
        style.focused.textColor = Ochre;
        return style;
    }

    internal static GUIStyle Field(GUIStyle original)
    {
        var style = new GUIStyle(original)
        {
            fontSize = 16, alignment = TextAnchor.MiddleRight,
            border = new RectOffset(5, 5, 5, 5), padding = new RectOffset(8, 8, 3, 3)
        };
        style.normal.background = Texture(Background);
        style.normal.textColor = Text;
        style.hover.background = Texture(Highlight);
        style.hover.textColor = Text;
        style.focused.background = Texture(Highlight);
        style.focused.textColor = Ochre;
        return style;
    }

    internal static void Fill(Rect rectangle, Color colour)
    {
        Color previous = GUI.color;
        GUI.color = colour;
        GUI.DrawTexture(rectangle, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static Texture2D Texture(Color colour)
    {
        if (Textures.TryGetValue(colour, out var existing)) return existing;
        var texture = new Texture2D(12, 12) { hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < 12; y++)
            for (int x = 0; x < 12; x++)
            {
                float dx = Mathf.Max(4 - x, x - 7);
                float dy = Mathf.Max(4 - y, y - 7);
                bool corner = dx > 0 && dy > 0 && dx * dx + dy * dy > 16;
                texture.SetPixel(x, y, corner ? Color.clear : colour);
            }
        texture.Apply();
        Textures.Add(colour, texture);
        return texture;
    }
}
