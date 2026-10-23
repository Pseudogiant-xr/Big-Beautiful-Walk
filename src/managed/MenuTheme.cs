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
    private static Texture2D? _border;
    private static bool _borderTried;

    internal static Texture2D? Artwork => LoadArtwork("ui-landscape.png", ref _artwork, ref _artworkTried);
    internal static Texture2D? Border => LoadArtwork("ui-border.png", ref _border, ref _borderTried);

    internal static void DrawBorder(Rect rectangle)
    {
        if (Border is not { } border) return;
        const float edge = 64, sourceEdge = .12f;
        float width = rectangle.width, height = rectangle.height;
        void Tile(float x, float y, float w, float h, float u, float v, float uw, float vh) =>
            GUI.DrawTextureWithTexCoords(new Rect(rectangle.x + x, rectangle.y + y, w, h), border, new Rect(u, v, uw, vh), true);
        Tile(0, 0, edge, edge, 0, 1 - sourceEdge, sourceEdge, sourceEdge);
        Tile(edge, 0, width - 2 * edge, edge, sourceEdge, 1 - sourceEdge, 1 - 2 * sourceEdge, sourceEdge);
        Tile(width - edge, 0, edge, edge, 1 - sourceEdge, 1 - sourceEdge, sourceEdge, sourceEdge);
        Tile(0, edge, edge, height - 2 * edge, 0, sourceEdge, sourceEdge, 1 - 2 * sourceEdge);
        Tile(width - edge, edge, edge, height - 2 * edge, 1 - sourceEdge, sourceEdge, sourceEdge, 1 - 2 * sourceEdge);
        Tile(0, height - edge, edge, edge, 0, 0, sourceEdge, sourceEdge);
        Tile(edge, height - edge, width - 2 * edge, edge, sourceEdge, 0, 1 - 2 * sourceEdge, sourceEdge);
        Tile(width - edge, height - edge, edge, edge, 1 - sourceEdge, 0, sourceEdge, sourceEdge);
    }

    private static Texture2D? LoadArtwork(string filename, ref Texture2D? cached, ref bool tried)
    {
        if (tried) return cached;
        tried = true;
        Texture2D? texture = null;
        try
        {
            string path = Path.Combine(Paths.PluginPath, "BigWalkDLSS", filename);
            if (!File.Exists(path)) return null;
            texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            if (ImageConversion.LoadImage(texture, new Il2CppStructArray<byte>(File.ReadAllBytes(path)), true))
            { cached = texture; texture = null; }
        }
        catch (System.Exception error) { Plugin.Instance.Log.LogWarning("Menu artwork could not be loaded: " + error.GetType().Name); }
        finally { if (texture != null) UnityEngine.Object.Destroy(texture); }
        return cached;
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
