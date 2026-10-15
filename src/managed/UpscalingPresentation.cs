using System;

namespace BigWalkDLSS;

internal static class UpscalingPresentation
{
    internal static string BackendName(int backend) => backend switch
    {
        0 => "DLSS", 1 => "FSR 3.1", 2 => "XeSS", 3 => "FSR 4", _ => "Automatic upscaler"
    };
    internal static string[] QualityNames(int backend) => new[]
    {
        backend == 0 ? "DLAA · native resolution" : "Native AA · 100%",
        "Ultra Quality · 77%", "Quality · 67%", "Balanced · 58%", "Performance · 50%", "Ultra Performance · 33%"
    };
    internal static string QualityName(int backend, int quality)
    {
        int index = Array.IndexOf(UpscalingOptions.QualityValues, quality);
        return index < 0 ? "Custom quality" : QualityNames(backend)[index];
    }
    internal static string ToggleLabel(int backend, int quality) => quality == 5
        ? backend == 0 ? "Enable DLAA" : "Enable native antialiasing"
        : "Enable " + BackendName(backend) + " upscaling";
    // NGX success confirms reconstruction, but cannot identify a bridge's fallback backend.
    internal static string ActiveStatus(int backend, int quality) => quality == 5 ? "Native antialiasing active" : "Upscaling active";
}
