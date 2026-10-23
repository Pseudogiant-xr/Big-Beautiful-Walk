using UnityEngine;

namespace BigWalkDLSS;

internal static class ToolGuide
{
    private static Vector2 _scroll;
    internal static void Reset() => _scroll = Vector2.zero;

    internal static void Draw(float x, float y, GUIStyle label, GUIStyle small)
    {
        GUI.Label(new Rect(x + 25, y + 185, 900, 28), "Graphics tools explained", label);
        const float rowHeight = 112;
        _scroll = GUI.BeginScrollView(new Rect(x + 25, y + 225, 910, 430), _scroll, new Rect(0, 0, 885, (6 + FeatureOptions.ProviderValues.Length) * rowHeight));
        try
        {
            float top = 0;
            for (int index = 0; index < 4; index++)
            {
                Row(UpscalingPresentation.BackendName(index), UpscalingPresentation.BackendHelp(index), BridgeSettings.UnavailableReason(index));
            }
            for (int index = 0; index < FeatureOptions.ProviderValues.Length; index++)
            {
                string reason = FeatureSettings.FrameGenUnavailable(FeatureOptions.ProviderValues[index], BridgeSettings.GpuVendorId, SystemInfo.graphicsDeviceName, FeatureMenu.UserConfirmedHags);
                Row(FeatureOptions.ProviderNames[index], FeatureOptions.ProviderHelp(index), reason);
            }
            Row("Neural Rendering · AI lighting", "Uses AI to redraw lighting and surface detail for a more realistic look. It can change the art style and reduce frame rate; it is separate from upscaling and is not ray tracing.", FeatureSettings.NeuralUnavailable(BridgeSettings.GpuVendorId));
            Row("DLAA / Native AA", UpscalingPresentation.QualityHelp(5), "");

            void Row(string title, string explanation, string reason)
            {
                GUI.Label(new Rect(16, top, 850, 26), title, label);
                GUI.Label(new Rect(16, top + 28, 850, 76), explanation + (reason.Length > 0 ? " On this setup: " + reason : ""), small);
                top += rowHeight;
            }
        }
        finally { GUI.EndScrollView(); }
    }
}
