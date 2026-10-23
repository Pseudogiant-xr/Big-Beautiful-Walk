using System;
using System.Collections.Generic;
using System.IO;
using BigWalkDLSS;
using UnityEngine;

void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
try
{
    ImageConversion.Reject = args.Length > 0;
    var border = MenuTheme.Border;
    var landscape = MenuTheme.Artwork;
    if (ImageConversion.Reject)
    {
        Check(border == null && landscape == null, "Rejected artwork was retained");
        Check(UnityEngine.Object.Destroyed == 2 && Plugin.Instance.Log.Warnings.Count == 2, "Failed decode did not clean up and report both textures");
    }
    else
    {
        Check(border != null && landscape != null, "Artwork is missing when no external PNG files are installed");
        Check(ImageConversion.Inputs.Count == 2 && ImageConversion.Inputs[0].Length > 1000000 && ImageConversion.Inputs[1].Length > 1000000,
            "Artwork bytes did not reach the real loader call");
        MenuTheme.DrawBorder(new Rect(0, 0, 1200, 1000));
        Check(GUI.Draws == 1 && GUI.Mode == ScaleMode.ScaleToFit && GUI.Alpha, "Border was stretched or lost transparency");
        Check(UnityEngine.Object.Destroyed == 0 && Plugin.Instance.Log.Warnings.Count == 0, "Valid artwork was destroyed or warned");
    }
    Check(ReferenceEquals(border, MenuTheme.Border) && ReferenceEquals(landscape, MenuTheme.Artwork) && ImageConversion.Inputs.Count == 2,
        "Artwork was decoded again on a later frame");
    Console.WriteLine("Menu artwork checks passed: " + (ImageConversion.Reject ? "decode rejection, cleanup and cached fallback" : "no external files, both embedded PNGs, cached proportional border"));
    return 0;
}
catch (Exception error) { Console.Error.WriteLine("FAIL " + error.Message); return 1; }

namespace BigWalkDLSS
{
    internal sealed class Plugin
    {
        internal static readonly Plugin Instance = new();
        internal readonly Logger Log = new();
    }
    internal sealed class Logger
    {
        internal readonly List<string> Warnings = new();
        internal void LogWarning(string message) => Warnings.Add(message);
    }
}
namespace BepInEx { internal static class Paths { internal static string PluginPath => Path.Combine(Path.GetTempPath(), "bbw-no-external-artwork-1b935907"); } }
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    internal sealed class Il2CppStructArray<T>(T[] data) { internal T[] Data => data; }
}
namespace UnityEngine
{
    internal enum HideFlags { HideAndDontSave }
    internal enum ScaleMode { ScaleToFit }
    internal enum FontStyle { Normal }
    internal enum TextAnchor { MiddleRight }
    internal readonly record struct Color(float R, float G, float B, float A)
    { internal static Color clear => new(0,0,0,0); }
    internal sealed class RectOffset(int a,int b,int c,int d) { }
    internal readonly record struct Rect(float X,float Y,float W,float H);
    internal static class Mathf { internal static float Max(float a,float b) => Math.Max(a,b); }
    internal static class Object
    { internal static int Destroyed; internal static void Destroy(Texture2D texture) => Destroyed++; }
    internal sealed class Texture2D(int width,int height)
    {
        internal HideFlags hideFlags; internal static readonly Texture2D whiteTexture = new(1,1);
        internal void SetPixel(int x,int y,Color colour) { } internal void Apply() { }
    }
    internal sealed class GUIStyle
    {
        internal GUIStyle(GUIStyle source) { }
        internal int fontSize; internal FontStyle fontStyle; internal TextAnchor alignment;
        internal RectOffset? border,padding;
        internal readonly State normal=new(),hover=new(),active=new(),focused=new();
    }
    internal sealed class State { internal Texture2D? background; internal Color textColor; }
    internal static class ImageConversion
    {
        internal static bool Reject;
        internal static readonly List<byte[]> Inputs = new();
        internal static bool LoadImage(Texture2D texture,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> bytes,bool markNonReadable)
        {
            Inputs.Add(bytes.Data);
            if (bytes.Data.Length < 8 || bytes.Data[0] != 137 || bytes.Data[1] != 80 || bytes.Data[2] != 78 || bytes.Data[3] != 71)
                throw new Exception("Not PNG bytes");
            return !Reject;
        }
    }
    internal static class GUI
    {
        internal static Color color; internal static int Draws; internal static ScaleMode Mode; internal static bool Alpha;
        internal static void DrawTexture(Rect rect,Texture2D texture) { }
        internal static void DrawTexture(Rect rect,Texture2D texture,ScaleMode mode,bool alpha) { Draws++; Mode=mode; Alpha=alpha; }
    }
}
