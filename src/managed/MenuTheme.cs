using System.Collections.Generic;
using System.IO;
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
        GUI.DrawTexture(rectangle, border, ScaleMode.ScaleToFit, true);
    }

    private static Texture2D? LoadArtwork(string filename, ref Texture2D? cached, ref bool tried)
    {
        if (tried) return cached;
        tried = true;
        Texture2D? texture = null;
        try
        {
            using var resource = typeof(MenuTheme).Assembly.GetManifestResourceStream("BigWalkDLSS." + filename)
                ?? throw new InvalidDataException("Embedded artwork is missing.");
            using var bytes = new MemoryStream();
            resource.CopyTo(bytes);
            texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            if (!LoadPng(texture, bytes.ToArray()))
                throw new InvalidDataException("Unity rejected the PNG data.");
            Plugin.Instance.Log.LogInfo("Menu artwork " + filename + " loaded: " + texture.width + "x" + texture.height + ".");
            cached = texture; texture = null;
        }
        catch (System.Exception error) { Plugin.Instance.Log.LogWarning("Menu artwork " + filename + " could not be loaded: " + error.GetType().Name + ". " + error.Message); }
        finally { if (texture != null) UnityEngine.Object.Destroy(texture); }
        return cached;
    }

    private static unsafe bool LoadPng(Texture2D texture, byte[] bytes)
    {
        var nativeTexture = UnityEngine.Object.MarshalledUnityObject.MarshalNotNull(texture);
        if (nativeTexture == System.IntPtr.Zero) throw new InvalidDataException("Unity texture has no native handle.");
        // Unity 6's array wrapper calls an unavailable IL2CPP span helper.
        // Pin managed bytes and use the generated native binding directly.
        fixed (byte* pointer = bytes)
        {
            var data = new UnityEngine.Bindings.ManagedSpanWrapper(pointer, bytes.Length);
            return ImageConversion.LoadImage_Injected(nativeTexture, ref data, true);
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
