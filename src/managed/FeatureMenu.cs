using System;
using UnityEngine;

namespace BigWalkDLSS;

internal static class FeatureMenu
{
    private static bool _confirmedHags;

    internal static void DrawFrameGen(float x, float y, GUIStyle label, GUIStyle small, GUIStyle button)
    {
        bool enabled = GUI.enabled;
        try
        {
            var current = FeatureSettings.StartupFrameGen;
            var queued = FeatureSettings.QueuedFrameGen;
            int vendor = SystemInfo.graphicsDeviceVendorID;
            string device = SystemInfo.graphicsDeviceName;
            GUI.Label(new Rect(x + 25, y + 185, 900, 32), "This launch: " + Request(current.Enabled, FeatureOptions.ProviderName(current.Provider)), label);
            GUI.Label(new Rect(x + 25, y + 221, 900, 35), "Next launch: " + Request(queued.Enabled, FeatureOptions.ProviderName(queued.Provider)) +
                (FeatureSettings.FrameGenPending ? " · Restart required" : " · No queued change"), small);
            string unavailable = FeatureSettings.FrameGenUnavailable(queued.Provider, vendor, device, _confirmedHags);
            GUI.enabled = enabled && BridgeSettings.Recognized && (queued.Enabled || unavailable.Length == 0);
            bool next = GUI.Toggle(new Rect(x + 25, y + 268, 900, 30), queued.Enabled, "Enable Frame Generation after restart", AdvancedGraphics.ToggleStyle);
            if (next != queued.Enabled) FeatureSettings.SetFrameGen(next, queued.Provider, vendor, device, _confirmedHags);
            GUI.enabled = enabled;
            GUI.Label(new Rect(x + 25, y + 310, 900, 27), "Provider · standard 2× (one generated frame)", label);
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
                    ? "Files ready. NVIDIA FG also requests NVIDIA Reflex Low Latency. Check FG in F10; this panel does not confirm Reflex initialization."
                    : "Files ready. Check provider initialization and active Frame Generation in F10 and the logs. This panel shows saved requests; compare smoothness and controls in the same view.", small);
            if (FeatureOptions.ProviderIndex(queued.Provider) == 0 && FeatureSettings.HagsMode != 1 && FeatureSettings.HagsMode != 2)
                _confirmedHags = GUI.Toggle(new Rect(x + 25, y + 453, 900, 28), _confirmedHags,
                    "I checked that Hardware-accelerated GPU Scheduling is enabled in Windows", AdvancedGraphics.ToggleStyle);
            GUI.Label(new Rect(x + 25, y + 489, 900, 47), "Detected GPU: " + device +
                ". NVIDIA requires RTX 40 Series or newer and Hardware-accelerated GPU Scheduling. No Windows or driver settings are changed here.", small);
            GUI.Label(new Rect(x + 25, y + 543, 900, 49),
                "AMD and Intel providers can work across GPU vendors, subject to runtime support. XeSS Frame Generation requires windowed or borderless mode. Higher multipliers and experimental options are in F10.", small);
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
            int vendor = SystemInfo.graphicsDeviceVendorID;
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
            float intensity = Strength(x, y + 346, "Model strength", queued.Intensity, 2, label, button);
            float detail = Strength(x, y + 391, "Composed detail", queued.TransferStrength, 2, label, button);
            float colour = Strength(x, y + 436, "Composed colour", queued.ColourStrength, 4, label, button);
            if (intensity != queued.Intensity || detail != queued.TransferStrength || colour != queued.ColourStrength)
            {
                queued.Intensity = intensity; queued.TransferStrength = detail; queued.ColourStrength = colour;
                if (!FeatureSettings.SetNeural(queued, vendor)) return;
            }
            GUI.Label(new Rect(x + 25, y + 482, 280, 28), "Model preset", label);
            if (GUI.Button(new Rect(x + 325, y + 481, 34, 28), "‹", button))
            { queued.Preset = (queued.Preset + 3) % 4; if (!FeatureSettings.SetNeural(queued, vendor)) return; }
            GUI.Label(new Rect(x + 375, y + 482, 485, 28), queued.Preset + " · Model-specific", label);
            if (GUI.Button(new Rect(x + 875, y + 481, 34, 28), "›", button))
            { queued.Preset = (queued.Preset + 1) % 4; if (!FeatureSettings.SetNeural(queued, vendor)) return; }
            GUI.enabled = enabled;
            GUI.Label(new Rect(x + 25, y + 521, 900, 57), unavailable.Length > 0 ? unavailable :
                "Forwarder and user model are present. Confirm model initialization in F10 and the logs, then compare the picture. Saved settings do not confirm active rendering.", small);
            GUI.Label(new Rect(x + 25, y + 582, 900, 39),
                "For a new install, place your compatible model at OptiScaler/nvngx_dlssnr.dll; existing game-root files are also recognized. Advanced codec and cache options are in F10.", small);
            DrawSaveNote(x, y, small);
        }
        finally { GUI.enabled = enabled; }
    }

    private static string Request(bool enabled, string choice) => enabled ? "On requested · " + choice + " · Runtime unverified" : "Off · " + choice;

    private static float Strength(float x, float y, string title, float value, float maximum, GUIStyle label, GUIStyle button)
    {
        GUI.Label(new Rect(x + 25, y, 280, 28), title, label);
        float next = value;
        if (GUI.Button(new Rect(x + 325, y, 34, 28), "−", button)) next = Math.Max(0, value - .1f);
        GUI.Label(new Rect(x + 375, y, 485, 28), value.ToString("0.0") + " / " + maximum.ToString("0") + " · 1.0 is the model default", label);
        if (GUI.Button(new Rect(x + 875, y, 34, 28), "+", button)) next = Math.Min(maximum, value + .1f);
        return next == value ? value : (float)Math.Round(next, 2);
    }

    private static void DrawSaveNote(float x, float y, GUIStyle small)
    {
        GUI.Label(new Rect(x + 25, y + 626, 900, 41), BridgeSettings.Message +
            " Save F10 changes before queuing Home choices; saving in F10 later may replace them.", small);
    }
}
