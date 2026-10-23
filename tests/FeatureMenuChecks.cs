using System;
using System.Collections.Generic;
using BigWalkDLSS;
using UnityEngine;

void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
void Draw() => FeatureMenu.DrawNeural(0, 0, new GUIStyle(), new GUIStyle(), new GUIStyle());
void Reset()
{
    GUI.enabled = true; GUI.Hit = ""; GUI.FailedSaveSeen = false; GUI.DrawnAfterFailure = false;
    GUI.Labels.Clear(); GUI.LabelPositions.Clear(); GUI.SawDisabledFsr4 = false;
    Plugin.QualityMode.Value = 2; Plugin.Preset.Value = 12; Plugin.Enabled.Value = true;
    FeatureSettings.Frame = new(); BridgeSettings.PendingBackend = -1;
    GUI.ScrollDepth = 0; AdvancedGraphics.GuideOpened = false; ToolGuide.Reset();
    BridgeSettings.RecognizedState = BridgeSettings.NvidiaState = BridgeSettings.XessPresent = true;
    BridgeSettings.Backend = 0;
    BridgeSettings.Fsr4Reason = "FSR 4 is unavailable on NVIDIA GPUs.";
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
    foreach (int backend in new[] { 0, 1, 2 })
    {
        Reset(); BridgeSettings.Backend = backend; BridgeSettings.XessPresent = backend == 2;
        UpscalingMenu.Draw(0, 0, new GUIStyle(), new GUIStyle(), new GUIStyle());
        string intro = GUI.LabelPositions.Find(item => item.Y == 145).Text;
        Check(!intro.Contains("FSR 4"), "Unselected FSR 4 notice is presented as the page introduction");
        Check(GUI.Labels.Contains(UpscalingPresentation.BackendHelp(backend)), "Selected provider has no own explanation");
        Check(!GUI.Labels.Contains(BridgeSettings.UnavailableReason(2)), "Unrelated XeSS warning appears for another provider");
    }
    Reset(); GUI.Hit = "FSR 3.1";
    UpscalingMenu.Draw(0, 0, new GUIStyle(), new GUIStyle(), new GUIStyle());
    Check(BridgeSettings.NextLaunchBackend == 1 && BridgeSettings.RunningBackend == 0, "Provider request changed running state");
    Check(GUI.Labels.Contains(UpscalingPresentation.BackendHelp(1)), "Provider explanation did not follow the clicked choice in the same draw");
    Check(GUI.Labels.Exists(text => text.Contains("DLSS quality mode")), "Pending provider hides which renderer live quality affects");
    foreach (int quality in UpscalingOptions.QualityValues)
    {
        Reset(); Plugin.QualityMode.Value = quality;
        UpscalingMenu.Draw(0, 0, new GUIStyle(), new GUIStyle(), new GUIStyle());
        Check(GUI.Labels.Exists(text => text.StartsWith(UpscalingPresentation.QualityHelp(quality))), "Quality explanation missing: " + quality);
    }
    foreach (var preset in new (int Value, string Token)[] { (0,"runtime chooses"), (10,"ghost"), (11,"Quality"), (12,"Ultra Performance"), (13,"Performance"), (5,"Deprecated"), (6,"Deprecated") })
    {
        Reset(); Plugin.Preset.Value = preset.Value;
        UpscalingMenu.Draw(0, 0, new GUIStyle(), new GUIStyle(), new GUIStyle());
        Check(GUI.Labels.Exists(text => text.Contains(preset.Token)), "DLSS model preset has no specific explanation: " + preset.Value);
    }
    foreach (int provider in new[] { 0, 1, 2 })
    {
        Reset(); FeatureSettings.Frame.Provider = FeatureOptions.ProviderValues[provider];
        FeatureMenu.DrawFrameGen(0, 0, new GUIStyle(), new GUIStyle(), new GUIStyle());
        Check(GUI.Labels.Contains(FeatureOptions.ProviderHelp(provider)), "FG explanation differs from the provider guide");
        string requirements = GUI.LabelPositions.Find(item => item.Y == 489).Text;
        Check(provider == 0 || !requirements.Replace("Detected GPU: " + SystemInfo.graphicsDeviceName + ". ", "").Contains("RTX 40"), "Non-NVIDIA FG shows NVIDIA-specific requirements: " + provider + " / " + requirements);
    }
    foreach (var style in new (int Value, string Token)[] { (0,"Default profile"), (1,"Natural profile"), (2,"Cinematic profile") })
    {
        Reset(); FeatureSettings.Live.Style = style.Value; Draw();
        Check(GUI.Labels.Exists(text => text.Contains(style.Token)), "NR style explanation does not follow its selection");
    }
    foreach (var preset in new (int Value, string Token)[] { (0,"runtime choose"), (1,"Standard model"), (2,"Lightweight FP8"), (3,"legacy") })
    {
        Reset(); FeatureSettings.Live.Preset = preset.Value; Draw();
        Check(GUI.Labels.Exists(text => text.Contains(preset.Token)), "NR preset explanation does not follow its selection");
    }
    foreach (var setup in new (bool Nvidia, bool Bridge, bool Xess)[] { (true, false, false), (true, true, false), (false, true, true) })
    {
        Reset();
        BridgeSettings.NvidiaState = setup.Nvidia;
        BridgeSettings.RecognizedState = setup.Bridge;
        BridgeSettings.XessPresent = setup.Xess;
        BridgeSettings.Backend = setup.Nvidia ? 0 : 1;
        BridgeSettings.Fsr4Reason = setup.Nvidia ? "FSR 4 is unavailable on NVIDIA GPUs." : "FSR 4 support is not available in this alpha.";
        int renderer = BridgeSettings.NextLaunchBackend;
        GUI.Hit = "Learn these tools";
        UpscalingMenu.Draw(0, 0, new GUIStyle(), new GUIStyle(), new GUIStyle());
        Check(AdvancedGraphics.GuideOpened, "Unavailable tools have no read-only guide entry");
        GUI.Labels.Clear();
        ToolGuide.Draw(0, 0, new GUIStyle(), new GUIStyle());
        for (int index = 0; index < 4; index++)
        {
            string explanation = UpscalingPresentation.BackendHelp(index);
            string reason = BridgeSettings.UnavailableReason(index);
            Check(GUI.Labels.Exists(text => text.Contains(explanation) && text.Contains(reason)),
                "Tool guide hides an unavailable upscaler explanation or reason: " + index);
        }
        for (int index = 0; index < 3; index++)
            Check(GUI.Labels.Exists(text => text.Contains(FeatureOptions.ProviderHelp(index))), "Frame-generation provider explanation missing");
        Check(GUI.Labels.Contains("Neural Rendering · AI lighting") && GUI.Labels.Contains("DLAA / Native AA"), "Other tool explanations missing");
        Check(BridgeSettings.NextLaunchBackend == renderer && Plugin.QualityMode.Value == 2 && FeatureSettings.Calls == 0 && GUI.ScrollDepth == 0 && GUI.enabled,
            "Learning about unavailable tools changed settings or leaked GUI state");
    }
    Console.WriteLine("Feature menu checks passed: edit recovery, 3 selected/queued upscalers, 6 quality modes, 7 DLSS presets, 3 FG providers, 3 NR styles, 4 NR presets and unavailable-tool guide states.");
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
        internal static FrameGenProfile Frame = new();
        internal static FrameGenProfile QueuedFrameGen => Frame;
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
        internal static bool RecognizedState = true, NvidiaState = true, XessPresent = true;
        internal static int Backend, PendingBackend = -1;
        internal static bool Recognized => RecognizedState;
        internal static string Message => "Test saved-state feedback";
        internal static int GpuVendorId => NvidiaState ? 0x10de : 0x1002;
        internal static bool NvidiaGpu => NvidiaState;
        internal static int RunningBackend => Backend;
        internal static int NextLaunchBackend => PendingBackend >= 0 ? PendingBackend : Backend;
        internal static bool PresetOverride => false;
        internal static readonly string[] BackendNames = { "DLSS", "FSR 3.1", "XeSS", "FSR 4" };
        internal static string Fsr4Reason = "FSR 4 is unavailable on NVIDIA GPUs.";
        internal static string UnavailableReason(int index)
        {
            if (index == 3) return Fsr4Reason;
            if (index == 0 && !NvidiaState) return "DLSS and its model presets need an NVIDIA RTX GPU.";
            if (index == 0 && !RecognizedState && Backend == 0) return "";
            if (!RecognizedState) return "Additional renderers need optional OptiScaler support. See the graphics guide.";
            if (index == 2 && !XessPresent) return "XeSS needs its Intel runtime. See the graphics guide to install it.";
            return "";
        }
        internal static void SelectBackend(int index) => PendingBackend = index;
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
    internal static class AdvancedGraphics
    {
        internal static bool GuideOpened;
        internal static GUIStyle ToggleStyle { get; } = new();
        internal static void ShowToolGuide() { GuideOpened = true; ToolGuide.Reset(); }
    }
}

namespace UnityEngine
{
    internal sealed class GUIStyle { }
    internal readonly record struct Color(float Red, float Green, float Blue, float Alpha);
    internal readonly record struct Vector2(float X, float Y) { internal static Vector2 zero => new(0, 0); }
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
        internal static readonly List<(float Y, string Text)> LabelPositions = new();
        internal static bool SawDisabledFsr4;
        internal static int ScrollDepth;
        internal static Vector2 BeginScrollView(Rect viewport, Vector2 scroll, Rect content) { ScrollDepth++; return scroll; }
        internal static void EndScrollView() { ScrollDepth--; }
        internal static void DrawTexture(Rect rectangle, Texture2D texture) { }
        internal static void Label(Rect rectangle, string text, GUIStyle style)
        { Labels.Add(text); LabelPositions.Add((rectangle.Y,text)); if (FailedSaveSeen) DrawnAfterFailure = true; }
        internal static bool Toggle(Rect rectangle, bool value, string text, GUIStyle style)
        { if (Hit != "toggle") return value; Hit = ""; return !value; }
        internal static bool Button(Rect rectangle, string text, GUIStyle style)
        {
            if (FailedSaveSeen) DrawnAfterFailure = true;
            if (text == "FSR 4" && !enabled) SawDisabledFsr4 = true;
            bool hit = Hit == text || (Hit == "plus" && text == "+") ||
                (Hit == "preset-left" && text == "‹");
            if (hit) Hit = "";
            return enabled && hit;
        }
    }
}
