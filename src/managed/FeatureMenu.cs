using System;
using UnityEngine;

namespace BigWalkDLSS;

internal static class FeatureMenu
{
    private static bool _confirmedHags;
    internal static bool UserConfirmedHags => _confirmedHags;

    internal static void DrawFrameGen(float x, float y, GUIStyle label, GUIStyle small, GUIStyle button)
    {
        bool enabled = GUI.enabled;
        try
        {
            var current = FeatureSettings.StartupFrameGen;
            var queued = FeatureSettings.QueuedFrameGen;
            int vendor = BridgeSettings.GpuVendorId;
            string device = SystemInfo.graphicsDeviceName;
            GUI.Label(new Rect(x + 25, y + 185, 900, 32), "This launch: " + Request(current.Enabled, FeatureOptions.ProviderName(current.Provider)), label);
            GUI.Label(new Rect(x + 25, y + 221, 900, 35), "Next launch: " + Request(queued.Enabled, FeatureOptions.ProviderName(queued.Provider)) +
                (FeatureSettings.FrameGenPending ? " · Restart required" : " · No queued change"), small);
            string unavailable = FeatureSettings.FrameGenUnavailable(queued.Provider, vendor, device, _confirmedHags);
            GUI.enabled = enabled && BridgeSettings.Recognized && (queued.Enabled || unavailable.Length == 0);
            bool next = GUI.Toggle(new Rect(x + 25, y + 268, 900, 30), queued.Enabled, "Enable Frame Generation after restart", AdvancedGraphics.ToggleStyle);
            if (next != queued.Enabled) FeatureSettings.SetFrameGen(next, queued.Provider, vendor, device, _confirmedHags);
            GUI.enabled = enabled;
            GUI.Label(new Rect(x + 25, y + 310, 900, 27), "Who generates extra frames · 2× adds one between each real frame", label);
            string[] names = { "NVIDIA DLSS Frame Generation", "AMD FSR Frame Generation", "Intel XeSS Frame Generation" };
            for (int i = 0; i < names.Length; i++)
            {
                string provider = FeatureOptions.ProviderValues[i];
                // Missing files may be queued while off; a running request must satisfy the enable guard.
                bool knownNvidiaBlocked = i == 0 && (FeatureOptions.NvidiaReason(vendor, device).Length > 0 ||
                    FeatureSettings.WindowsBuild < 19041 || FeatureSettings.HagsMode == 1);
                GUI.enabled = enabled && BridgeSettings.Recognized && !knownNvidiaBlocked &&
                    (!queued.Enabled || FeatureSettings.FrameGenUnavailable(provider, vendor, device, _confirmedHags).Length == 0);
                if (GUI.Button(new Rect(x + 25 + i * 303, y + 345, 293, 34),
                    (FeatureOptions.ProviderIndex(queued.Provider) == i ? "● " : "") + names[i], button))
                    FeatureSettings.SetFrameGen(queued.Enabled, provider, vendor, device, _confirmedHags);
            }
            GUI.enabled = enabled;
            GUI.Label(new Rect(x + 25, y + 391, 900, 56), unavailable.Length > 0 ? unavailable :
                FeatureOptions.ProviderIndex(queued.Provider) == 0
                    ? "Files are installed. NVIDIA FG also asks for Reflex to reduce delay. F10 shows whether extra frames are actually being generated; this panel cannot confirm Reflex is running."
                    : "Files are installed. F10 shows whether extra frames are actually being generated. Compare motion and control response in the same view; a saved choice does not prove it is working.", small);
            if (FeatureOptions.ProviderIndex(queued.Provider) == 0 && FeatureSettings.HagsMode != 1 && FeatureSettings.HagsMode != 2)
                _confirmedHags = GUI.Toggle(new Rect(x + 25, y + 453, 900, 28), _confirmedHags,
                    "I checked that Hardware-accelerated GPU Scheduling is enabled in Windows", AdvancedGraphics.ToggleStyle);
            GUI.Label(new Rect(x + 25, y + 489, 900, 47), "Detected GPU: " + device +
                ". NVIDIA requires RTX 40 Series or newer and Hardware-accelerated GPU Scheduling. No Windows or driver settings are changed here.", small);
            GUI.Label(new Rect(x + 25, y + 543, 900, 49),
                FeatureOptions.ProviderIndex(queued.Provider) == 0
                    ? "NVIDIA uses AI to make the in-between pictures. It was the best-looking option on the tested RTX 4090. Thin objects can shimmer; extra frames do not make controls respond faster."
                    : FeatureOptions.ProviderIndex(queued.Provider) == 2
                        ? "Intel uses AI to make the in-between pictures on supported GPUs. Needs windowed or borderless mode. On the tested RTX 4090 it made the game slower; compare before keeping it."
                        : FeatureOptions.ProviderIndex(queued.Provider) == 1
                            ? "AMD uses motion between real frames to make the in-between pictures, across different GPU brands. On the tested RTX 4090 it looked and ran worse than NVIDIA."
                            : "A custom provider is selected in F10. Choose a supported provider above to use Home's Frame Generation controls.", small);
            DrawSaveNote(x, y, small);
        }
        finally { GUI.enabled = enabled; }
    }

    internal static void DrawNeural(float x, float y, GUIStyle label, GUIStyle small, GUIStyle button)
    {
        bool enabled = GUI.enabled;
        try
        {
            var current = FeatureSettings.StartupNeural;
            var queued = FeatureSettings.QueuedNeural;
            int vendor = BridgeSettings.GpuVendorId;
            GUI.Label(new Rect(x + 25, y + 185, 900, 28), "This launch: " + Request(current.Enabled, FeatureOptions.StyleNames[current.Style]), label);
            GUI.Label(new Rect(x + 25, y + 219, 900, 31), "Next launch: " + Request(queued.Enabled, FeatureOptions.StyleNames[queued.Style]) +
                (FeatureSettings.NeuralPending ? " · Restart required" : " · No queued change"), small);
            string unavailable = FeatureSettings.NeuralUnavailable(vendor);
            GUI.enabled = enabled && BridgeSettings.Recognized && (queued.Enabled || unavailable.Length == 0);
            bool next = GUI.Toggle(new Rect(x + 25, y + 259, 900, 29), queued.Enabled, "Enable Neural Rendering after restart", AdvancedGraphics.ToggleStyle);
            if (next != queued.Enabled)
            {
                queued.Enabled = next;
                if (!FeatureSettings.SetNeural(queued, vendor)) return;
            }
            GUI.enabled = enabled && BridgeSettings.Recognized && (!queued.Enabled || unavailable.Length == 0);
            GUI.Label(new Rect(x + 25, y + 302, 260, 28), "Style", label);
            for (int i = 0; i < FeatureOptions.StyleNames.Length; i++)
                if (GUI.Button(new Rect(x + 325 + i * 200, y + 300, 190, 31),
                    (queued.Style == i ? "● " : "") + FeatureOptions.StyleNames[i], button))
                { queued.Style = i; if (!FeatureSettings.SetNeural(queued, vendor)) return; }
            GUI.Label(new Rect(x + 25, y + 333, 900, 21), "Chooses the AI's visual treatment. Default is the tested starting point; compare the alternatives yourself.", small);
            float intensity = Strength(x, y + 356, "AI effect strength", "How strongly the AI redraws the scene's lighting and surfaces. 1 is the normal amount.", queued.Intensity, 2, label, small, button);
            float detail = Strength(x, y + 401, "Lighting / detail blend", "How much AI lighting and surface detail is blended into the picture.", queued.TransferStrength, 2, label, small, button);
            float colour = Strength(x, y + 446, "AI colour amount", "How much the AI changes colours. 0 keeps the game's original hues.", queued.ColourStrength, 4, label, small, button);
            if (intensity != queued.Intensity || detail != queued.TransferStrength || colour != queued.ColourStrength)
            {
                queued.Intensity = intensity; queued.TransferStrength = detail; queued.ColourStrength = colour;
                if (!FeatureSettings.SetNeural(queued, vendor)) return;
            }
            GUI.Label(new Rect(x + 25, y + 492, 280, 28), "Model preset", label);
            if (GUI.Button(new Rect(x + 325, y + 491, 34, 28), "‹", button))
            { queued.Preset = (queued.Preset + 3) % 4; if (!FeatureSettings.SetNeural(queued, vendor)) return; }
            GUI.Label(new Rect(x + 375, y + 492, 485, 28), FeatureOptions.NeuralPresetNames[queued.Preset], label);
            if (GUI.Button(new Rect(x + 875, y + 491, 34, 28), "›", button))
            { queued.Preset = (queued.Preset + 1) % 4; if (!FeatureSettings.SetNeural(queued, vendor)) return; }
            GUI.enabled = enabled;
            GUI.Label(new Rect(x + 25, y + 525, 900, 36), "NR 310.9.16: A keeps the original AI look with more graphics work. B is the faster new model, with a different look. Auto chooses for you; 3 depends on the runtime.", small);
            GUI.Label(new Rect(x + 25, y + 562, 900, 36), unavailable.Length > 0 ? unavailable :
                "The required files are present. Check F10 to confirm AI rendering is running, then compare the picture; saved settings alone do not prove it.", small);
            GUI.Label(new Rect(x + 25, y + 600, 900, 23),
                "Need the model? Extract the optional NR pack from the release page and select its DLL in Setup.", small);
            DrawSaveNote(x, y, small);
        }
        finally { GUI.enabled = enabled; }
    }

    private static string Request(bool enabled, string choice) => enabled ? "On requested · " + choice + " · Check F10" : "Off · " + choice;

    private static float Strength(float x, float y, string title, string help, float value, float maximum, GUIStyle label, GUIStyle small, GUIStyle button)
    {
        GUI.Label(new Rect(x + 25, y, 280, 25), title, label);
        float next = value;
        if (GUI.Button(new Rect(x + 325, y, 34, 25), "−", button)) next = Math.Max(0, value - .1f);
        GUI.Label(new Rect(x + 375, y, 485, 25), value.ToString("0.0") + " / " + maximum.ToString("0") + " · 1.0 is the model default", label);
        if (GUI.Button(new Rect(x + 875, y, 34, 25), "+", button)) next = Math.Min(maximum, value + .1f);
        GUI.Label(new Rect(x + 25, y + 25, 900, 20), help, small);
        return next == value ? value : (float)Math.Round(next, 2);
    }

    private static void DrawSaveNote(float x, float y, GUIStyle small)
    {
        GUI.Label(new Rect(x + 25, y + 626, 900, 41), BridgeSettings.Message +
            " Save F10 changes before queuing Home choices; saving in F10 later may replace them.", small);
    }
}
