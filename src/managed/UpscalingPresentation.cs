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
    internal static string BackendHelp(int backend) => backend switch
    {
        0 => "DLSS rebuilds the picture using an NVIDIA RTX GPU. Quality is the tested starting point.",
        1 => "FSR 3.1 is an alternative upscaler for different GPU brands. Compare its picture with DLSS or XeSS.",
        2 => "XeSS is another way to rebuild the picture. It may look different from DLSS or FSR in moving scenes.",
        3 => "FSR 4 is not available in this alpha. Choose one of the supported upscalers instead.",
        _ => "An upscaler rebuilds a smaller image to reduce graphics work. Choose a supported option above."
    };
    internal static string QualityHelp(int quality) => quality switch
    {
        5 => "Smooths jagged edges at full resolution, without the usual upscaling speed benefit. Experimental.",
        4 => "Draws more detail than Quality, with a heavier graphics load. Experimental.",
        2 => "A clearer picture with less graphics work than native resolution. The tested starting point.",
        1 => "Trades some image detail for less graphics work. Try this if Quality feels too slow.",
        0 => "Draws a smaller image to favour frame rate. Fine details may look softer or less stable.",
        3 => "Uses the smallest image and sacrifices the most detail. Mainly useful at very high screen resolutions.",
        _ => "Choose Quality as a starting point, then compare detail and smoothness in the same view."
    };
    // NGX success confirms reconstruction, but cannot identify a bridge's fallback backend.
    internal static string ActiveStatus(int backend, int quality) => quality == 5 ? "Native antialiasing active" : "Upscaling active";
}
