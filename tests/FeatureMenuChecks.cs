using System;
using BigWalkDLSS;
using UnityEngine;

void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
void Draw() => FeatureMenu.DrawNeural(0, 0, new GUIStyle(), new GUIStyle(), new GUIStyle());
void Reset()
{
    GUI.enabled = true; GUI.Hit = ""; GUI.FailedSaveSeen = false; GUI.DrawnAfterFailure = false;
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
    Console.WriteLine("Feature menu checks passed: idle precision, style/enable/preset failures, fresh retry and intentional strength edits; memory-only GUI stubs.");
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
    internal static class BridgeSettings { internal static bool Recognized => true; internal static string Message => "Test saved-state feedback"; internal static int GpuVendorId => 0x10de; }
    internal static class AdvancedGraphics { internal static GUIStyle ToggleStyle { get; } = new(); }
}

namespace UnityEngine
{
    internal sealed class GUIStyle { }
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
        internal static void Label(Rect rectangle, string text, GUIStyle style) { if (FailedSaveSeen) DrawnAfterFailure = true; }
        internal static bool Toggle(Rect rectangle, bool value, string text, GUIStyle style)
        { if (Hit != "toggle") return value; Hit = ""; return !value; }
        internal static bool Button(Rect rectangle, string text, GUIStyle style)
        {
            if (FailedSaveSeen) DrawnAfterFailure = true;
            bool hit = Hit == text || (Hit == "plus" && text == "+" && rectangle.Y == 346) ||
                (Hit == "preset-left" && text == "‹" && rectangle.Y == 481);
            if (hit) Hit = "";
            return hit;
        }
    }
}
