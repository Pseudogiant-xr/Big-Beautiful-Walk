using System;

namespace BigWalkDLSS;

internal static class UpscalingPresentation
{
    internal static string Introduction => "Upscalers can improve frame rate and smooth jagged edges by rebuilding a detailed picture from fewer pixels. The selected tool is explained below.";
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
    internal static string PresetHelp(int preset) => preset switch
    {
        0 => "Default: the runtime chooses the reconstruction model. Its choice can change with driver updates.",
        10 => "J: may reduce trails behind moving objects (ghosting), but can add flicker compared with K. Compare moving foliage and fine edges.",
        11 => "K: NVIDIA's usual model for DLAA, Quality and Balanced. Rebuilds fine detail, with more graphics work than older models.",
        12 => "L: tuned for Ultra Performance. Can sharpen detail and reduce trails, but costs more graphics work. Quality + L is this mod's tested baseline.",
        13 => "M: tuned for Performance. Aims for detail improvements similar to L with a lower rendering cost; compare on your GPU.",
        5 => "E: Deprecated older model hint. Current runtimes may replace it; use a current preset unless comparing an older runtime.",
        6 => "F: Deprecated older model hint. Current runtimes may replace it; this is not a higher quality level than E.",
        _ => "Custom model hint. Its effect depends on the installed runtime."
    };
    // NGX success confirms reconstruction, but cannot identify a bridge's fallback backend.
    internal static string ActiveStatus(int backend, int quality) => quality == 5 ? "Native antialiasing active" : "Upscaling active";
}
