using System;

namespace BigWalkDLSS;

internal static class UpscalingPresentation
{
    internal static string Introduction(string fsr4Unavailable) => fsr4Unavailable.Length > 0
        ? "FSR 4 uses newer AI to rebuild detail from fewer pixels. " + fsr4Unavailable
        : "These tools can improve frame rate and smooth jagged edges by rebuilding a detailed picture from fewer pixels. Each option explains how below.";
    internal static string BackendName(int backend) => backend switch
    {
        0 => "DLSS", 1 => "FSR 3.1", 2 => "XeSS", 3 => "FSR 4", _ => "Automatic upscaler"
    };
    internal static string[] QualityNames(int backend) => new[]
    {
        backend == 0 ? "DLAA · smooth edges at full resolution" : "Native AA · smooth edges at full resolution",
        "Ultra Quality · more detail", "Quality · clearer picture", "Balanced · picture + speed", "Performance · favour speed", "Ultra Performance · least detail"
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
        0 => "DLSS can raise frame rate while keeping a detailed picture. Your GPU draws fewer pixels; AI uses motion and earlier frames to rebuild the missing detail. Needs NVIDIA RTX.",
        1 => "FSR 3.1 can raise frame rate on different GPU brands. It draws fewer pixels, then combines motion and earlier frames to rebuild detail. Fine edges may look different from DLSS.",
        2 => "XeSS can raise frame rate while smoothing jagged edges. Like DLSS, it uses AI and earlier frames to rebuild a smaller image; it can work on different GPU brands.",
        3 => "FSR 4 uses AI to rebuild a detailed picture from fewer pixels on supported AMD GPUs. It is not available in this alpha; choose DLSS, FSR 3.1 or XeSS instead.",
        _ => "An upscaler rebuilds a smaller image to reduce graphics work. Choose a supported option above."
    };
    internal static string QualityHelp(int quality) => quality switch
    {
        5 => "Draws every screen pixel, then smooths jagged edges. Favours a clean picture rather than a frame-rate boost. Experimental.",
        4 => "Draws more pixels before rebuilding the picture. Favours fine detail, but gives less of a speed boost. Experimental.",
        2 => "Draws fewer pixels than full resolution, while keeping more detail than the faster modes. A good starting point.",
        1 => "Draws fewer pixels than Quality for a bigger speed benefit, with some loss of fine detail.",
        0 => "Draws a smaller image to favour frame rate. Fine details may look softer or less stable.",
        3 => "Uses the smallest image and sacrifices the most detail. Mainly useful at very high screen resolutions.",
        _ => "Choose Quality as a starting point, then compare detail and smoothness in the same view."
    };
    // NGX success confirms reconstruction, but cannot identify a bridge's fallback backend.
    internal static string ActiveStatus(int backend, int quality) => quality == 5 ? "Native antialiasing active" : "Upscaling active";
}
