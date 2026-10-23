using System;
using System.Collections.Generic;
using BigWalkDLSS;
using UnityEngine;

void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
void Draw() => FeatureMenu.DrawNeural(0, 0, new GUIStyle(), new GUIStyle(), new GUIStyle());
void Reset()
{
    GUI.enabled = true; GUI.Hit = ""; GUI.FailedSaveSeen = false; GUI.DrawnAfterFailure = false;
    GUI.Labels.Clear(); GUI.SawDisabledFsr4 = false;
    FeatureSettings.Calls = 0; FeatureSettings.FailNext = false;
    FeatureSettings.Live = new NeuralProfile { Style = 0, Intensity = .1234f, TransferStrength = 1.2345f, ColourStrength = .9876f };
}
try
{
    Reset(); Draw();
    Check(FeatureSettings.Calls == 0 && FeatureSettings.Live.Intensity == .1234f, "Idle draw changed an untouched custom strength");

    Reset(); GUI.Hit = "Natural"; FeatureSettings.FailNext = true; Draw();
    Check(FeatureSettings.Calls == 1, "Failed style edit triggered a second save");
    Check(FeatureSettings.Live.Style == 2 && FeatureSettings.Live.Intensity == .4321f, "Failed style edit overwrote refreshed external values");
    Check(!GUI.DrawnAfterFailure, "Draw continued with stale controls after a failed style edit");
    GUI.Hit = "Natural"; GUI.FailedSaveSeen = false; Draw();
    Check(FeatureSettings.Calls == 2 && FeatureSettings.Live.Style == 1 && FeatureSettings.Live.Intensity == .4321f,
        "Deliberate next-frame retry did not preserve refreshed custom strength");

    Reset(); GUI.Hit = "toggle"; FeatureSettings.FailNext = true; Draw();
    Check(FeatureSettings.Calls == 1 && !GUI.DrawnAfterFailure, "Failed enable edit continued drawing stale controls");

    Reset(); GUI.Hit = "Natural"; Draw();
    Check(FeatureSettings.Calls == 1 && FeatureSettings.Live.Style == 1 && FeatureSettings.Live.Intensity == .1234f,
        "Successful style edit rounded unrelated strength values");

    Reset(); GUI.Hit = "plus"; Draw();
    Check(FeatureSettings.Calls == 1 && FeatureSettings.Live.Intensity == .22f && FeatureSettings.Live.TransferStrength == 1.2345f,
        "Strength input did not change only the requested value");

    Reset(); GUI.Hit = "preset-left"; FeatureSettings.FailNext = true; Draw();
    Check(FeatureSettings.Calls == 1 && !GUI.DrawnAfterFailure && FeatureSettings.Live.Style == 2,
        "Failed preset edit continued with a stale profile");
    foreach (string reason in new[] { "FSR 4 is unavailable on NVIDIA GPUs.", "FSR 4 support is not available in this alpha." })
    {
        Reset(); BridgeSettings.Fsr4Reason = reason;
        UpscalingMenu.Draw(0, 0, new GUIStyle(), new GUIStyle(), new GUIStyle());
        Check(GUI.SawDisabledFsr4, "Fixture did not draw FSR 4 disabled");
        Check(GUI.Labels.Exists(text => text.Contains("FSR 4") && text.Contains("AI") && text.Contains(reason)),
            "Disabled FSR 4 has no visible explanation and availability reason");
        Check(BridgeSettings.NextLaunchBackend == 0 && Plugin.QualityMode.Value == 2 && GUI.enabled,
            "Reading unavailable-tool help changed settings or GUI state");
    }
    Console.WriteLine("Feature menu checks passed: idle precision, edit recovery and visible disabled-upscaler explanations; memory-only GUI capture.");
    return 0;
}
catch (Exception error) { Console.Error.WriteLine("FAIL " + error.Message); return 1; }

namespace BigWalkDLSS
{
    internal static class FeatureSettings
    {
        internal static NeuralProfile Live = new();
        internal static int Calls;
        internal static bool FailNext;
        internal static readonly NeuralProfile StartupNeural = new();
        internal static readonly FrameGenProfile StartupFrameGen = new();
        internal static NeuralProfile QueuedNeural => Copy(Live);
        internal static FrameGenProfile QueuedFrameGen => new();
        internal static bool NeuralPending => false;
        internal static bool FrameGenPending => false;
        internal static int WindowsBuild => 26100;
        internal static int? HagsMode => 2;
        internal static string NeuralUnavailable(int vendor) => "";
        internal static string FrameGenUnavailable(string provider, int vendor, string device, bool confirmed) => "";
        internal static bool SetFrameGen(bool enabled, string provider, int vendor, string device, bool confirmed) => true;
        internal static bool SetNeural(NeuralProfile profile, int vendor)
        {
            Calls++;
            if (FailNext)
            {
                FailNext = false;
                Live = new NeuralProfile { Style = 2, Intensity = .4321f, TransferStrength = 1.7654f, ColourStrength = .8765f };
                GUI.FailedSaveSeen = true;
                return false;
            }
            Live = Copy(profile);
            return true;
        }
        private static NeuralProfile Copy(NeuralProfile profile) => new()
        { Enabled = profile.Enabled, Style = profile.Style, Preset = profile.Preset,
          Intensity = profile.Intensity, TransferStrength = profile.TransferStrength, ColourStrength = profile.ColourStrength };
    }
    internal static class BridgeSettings
    {
        internal static bool Recognized => true;
        internal static string Message => "Test saved-state feedback";
        internal static int GpuVendorId => 0x10de;
        internal static bool NvidiaGpu => true;
        internal static int RunningBackend => 0;
        internal static int NextLaunchBackend => 0;
        internal static bool PresetOverride => false;
        internal static readonly string[] BackendNames = { "DLSS", "FSR 3.1", "XeSS", "FSR 4" };
        internal static string Fsr4Reason = "FSR 4 is unavailable on NVIDIA GPUs.";
        internal static string UnavailableReason(int index) => index == 3 ? Fsr4Reason : "";
        internal static void SelectBackend(int index) => throw new Exception("Unexpected backend edit");
        internal static void UseHomePresets() => throw new Exception("Unexpected preset edit");
        internal static void Reload() => throw new Exception("Unexpected settings reload");
    }
    internal sealed class TestSetting<T>(T value) { internal T Value = value; }
    internal static class Plugin
    {
        internal static bool Compatible => true;
        internal static bool NativeLoaded => true;
        internal static string RendererStatus => "Test renderer status";
        internal static readonly TestSetting<bool> Enabled = new(true);
        internal static readonly TestSetting<int> QualityMode = new(2), Preset = new(12);
        internal static readonly TestSetting<float> RenderScale = new(.666667f);
        internal static void ResetHistory() => throw new Exception("Unexpected history reset");
        internal static void RetryUpscaler() => throw new Exception("Unexpected upscaler retry");
    }
    internal static class MenuTheme { internal static Color Highlight => new(0, 0, 0, 1); }
    internal static class AdvancedGraphics { internal static GUIStyle ToggleStyle { get; } = new(); }
}

namespace UnityEngine
{
    internal sealed class GUIStyle { }
    internal readonly record struct Color(float Red, float Green, float Blue, float Alpha);
    internal sealed class Texture2D { internal static readonly Texture2D whiteTexture = new(); }
    internal readonly struct Rect
    {
        internal readonly float X, Y;
        internal Rect(float x, float y, float width, float height) { X = x; Y = y; }
    }
    internal static class SystemInfo { internal static int graphicsDeviceVendorID => 0x10de; internal static string graphicsDeviceName => "NVIDIA GeForce RTX 4090"; }
    internal static class GUI
    {
        internal static bool enabled = true, FailedSaveSeen, DrawnAfterFailure;
        internal static string Hit = "";
        internal static Color color = new(1, 1, 1, 1);
        internal static readonly List<string> Labels = new();
        internal static bool SawDisabledFsr4;
        internal static void DrawTexture(Rect rectangle, Texture2D texture) { }
        internal static void Label(Rect rectangle, string text, GUIStyle style)
        { Labels.Add(text); if (FailedSaveSeen) DrawnAfterFailure = true; }
        internal static bool Toggle(Rect rectangle, bool value, string text, GUIStyle style)
        { if (Hit != "toggle") return value; Hit = ""; return !value; }
        internal static bool Button(Rect rectangle, string text, GUIStyle style)
        {
            if (FailedSaveSeen) DrawnAfterFailure = true;
            if (text == "FSR 4" && !enabled) SawDisabledFsr4 = true;
            bool hit = Hit == text || (Hit == "plus" && text == "+") ||
                (Hit == "preset-left" && text == "‹");
            if (hit) Hit = "";
            return hit;
        }
    }
}
