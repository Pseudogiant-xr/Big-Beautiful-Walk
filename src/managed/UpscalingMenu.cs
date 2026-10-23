using System;
using UnityEngine;

namespace BigWalkDLSS;

internal static class UpscalingMenu
{
    internal static void Draw(float x, float y, GUIStyle label, GUIStyle small, GUIStyle button)
    {
        bool enabled = GUI.enabled;
        try
        {
            GUI.Label(new Rect(x + 25, y + 145, 900, 37), UpscalingPresentation.Introduction(BridgeSettings.UnavailableReason(3)), small);
            GUI.Label(new Rect(x + 25, y + 185, 900, 28), "Upscaler · how the picture is rebuilt", label);
            int selectedBackend = BridgeSettings.NextLaunchBackend >= 0 ? BridgeSettings.NextLaunchBackend : BridgeSettings.RunningBackend;
            for (int index = 0; index < BridgeSettings.BackendNames.Length; index++)
            {
                GUI.enabled = enabled && BridgeSettings.UnavailableReason(index).Length == 0;
                if (GUI.Button(new Rect(x + 25 + index * 225, y + 217, 210, 32),
                    (selectedBackend == index ? "● " : "") + BridgeSettings.BackendNames[index], button))
                    BridgeSettings.SelectBackend(index);
            }
            GUI.enabled = enabled;
            bool pending = BridgeSettings.NextLaunchBackend >= 0 && BridgeSettings.NextLaunchBackend != BridgeSettings.RunningBackend;
            if (pending)
            {
                Color colour = GUI.color;
                GUI.color = MenuTheme.Highlight;
                GUI.DrawTexture(new Rect(x + 25, y + 252, 900, 32), Texture2D.whiteTexture);
                GUI.color = colour;
                GUI.Label(new Rect(x + 38, y + 255, 875, 26), "Restart required · Restart Big Walk to use " + UpscalingPresentation.BackendName(BridgeSettings.NextLaunchBackend) + ".", label);
            }
            GUI.Label(new Rect(x + 25, y + 285, 900, 38), UpscalingPresentation.BackendHelp(selectedBackend), small);
            GUI.Label(new Rect(x + 25, y + 325, 900, 26), Plugin.RendererStatus, label);
            GUI.enabled = enabled && Plugin.Compatible && Plugin.NativeLoaded;
            bool nextEnabled = GUI.Toggle(new Rect(x + 25, y + 355, 570, 28), Plugin.Enabled.Value,
                UpscalingPresentation.ToggleLabel(BridgeSettings.RunningBackend, Plugin.QualityMode.Value), AdvancedGraphics.ToggleStyle);
            if (nextEnabled != Plugin.Enabled.Value) { Plugin.Enabled.Value = nextEnabled; Plugin.ResetHistory(); }
            if (GUI.Button(new Rect(x + 690, y + 355, 235, 28), "Retry upscaler", button)) Plugin.RetryUpscaler();
            int quality = Choice(x, y + 393, "Quality mode", Plugin.QualityMode.Value,
                UpscalingOptions.QualityValues, UpscalingPresentation.QualityNames(BridgeSettings.RunningBackend), label, button);
            if (quality != Plugin.QualityMode.Value)
            {
                Plugin.QualityMode.Value = quality;
                Plugin.RenderScale.Value = UpscalingOptions.Scale(quality);
                Plugin.ResetHistory();
            }
            GUI.enabled = enabled;
            GUI.Label(new Rect(x + 25, y + 428, 900, 38),
                UpscalingPresentation.QualityHelp(Plugin.QualityMode.Value) +
                " Changes apply now.", small);
            if (BridgeSettings.RunningBackend == 0)
            {
                GUI.enabled = enabled && Plugin.Compatible && Plugin.NativeLoaded;
                int preset = Choice(x, y + 471, "Model preset", Plugin.Preset.Value,
                    UpscalingOptions.PresetValues, UpscalingOptions.PresetNames, label, button);
                if (preset != Plugin.Preset.Value) { Plugin.Preset.Value = preset; Plugin.ResetHistory(); }
                GUI.enabled = enabled;
                GUI.Label(new Rect(x + 25, y + 505, 900, 36), "Chooses a different AI method for rebuilding detail. Letters are not quality levels: later is not always better. Start with L; the driver may override it.", small);
                if (BridgeSettings.PresetOverride)
                {
                    GUI.Label(new Rect(x + 25, y + 544, 555, 37), "OptiScaler controls the model preset until you restart with Home presets enabled.", small);
                    if (GUI.Button(new Rect(x + 590, y + 545, 335, 31), "Use Home presets after restart", button)) BridgeSettings.UseHomePresets();
                }
            }
            else if (BridgeSettings.UnavailableReason(2).Length > 0)
                GUI.Label(new Rect(x + 25, y + 471, 900, 42), BridgeSettings.UnavailableReason(2), small);
            GUI.Label(new Rect(x + 25, y + 584, 715, 36), BridgeSettings.Message, small);
            if (GUI.Button(new Rect(x + 750, y + 584, 175, 31), "Refresh settings", button)) BridgeSettings.Reload();
            GUI.Label(new Rect(x + 25, y + 625, 900, 40),
                "Retry upscaler tries again if it has stopped. Refresh settings rereads choices saved in F10. Frame Gen adds smoother motion; Neural changes the scene's lighting and detail.", small);
        }
        finally { GUI.enabled = enabled; }
    }

    private static int Choice(float x, float y, string title, int current, int[] values, string[] names, GUIStyle label, GUIStyle button)
    {
        int index = Array.IndexOf(values, current);
        int next = index;
        GUI.Label(new Rect(x + 25, y, 280, 28), title, label);
        if (GUI.Button(new Rect(x + 325, y, 34, 28), "‹", button)) next = index < 0 ? 0 : (index + values.Length - 1) % values.Length;
        GUI.Label(new Rect(x + 375, y, 485, 28), index < 0 ? "Custom setting" : names[index], label);
        if (GUI.Button(new Rect(x + 875, y, 34, 28), "›", button)) next = index < 0 ? 0 : (index + 1) % values.Length;
        return next < 0 ? current : values[next];
    }
}
