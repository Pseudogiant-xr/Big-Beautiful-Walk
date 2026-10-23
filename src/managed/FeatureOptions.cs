using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BigWalkDLSS;

internal sealed class FrameGenProfile
{
    internal bool Enabled;
    internal string Provider = "DLSSG";
    internal string Signature = "";
}

internal sealed class NeuralProfile
{
    internal bool Enabled;
    internal int Style, Preset;
    internal float Intensity = 1, TransferStrength = 1, ColourStrength = 1;
    internal string Signature = "";
}

// These mappings describe saved requests. They cannot establish runtime support.
internal static class FeatureOptions
{
    internal static readonly string[] ProviderValues = { "DLSSG", "FSRFG", "XeFG" };
    internal static readonly string[] ProviderNames = { "NVIDIA DLSS Frame Generation", "AMD FSR Frame Generation", "Intel XeSS Frame Generation" };
    internal static readonly string[] StyleNames = { "Default", "Natural", "Cinematic" };
    internal static readonly string[] NeuralPresetNames = { "0 · Auto (runtime default)", "1 · A: Standard (310.9.16)", "2 · B: Lightweight FP8 (310.9.16)", "3 · Model-specific / legacy" };
    internal static readonly string[] NvidiaFiles = { "OptiScaler/streamline/sl.interposer.dll", "OptiScaler/streamline/sl.common.dll", "OptiScaler/streamline/sl.dlss_g.dll", "OptiScaler/streamline/sl.pcl.dll", "OptiScaler/streamline/sl.reflex.dll", "OptiScaler/streamline/nvngx_dlssg.dll" };
    internal static readonly string[] AmdFiles = { "OptiScaler/amd_fidelityfx_loader_dx12.dll", "OptiScaler/amd_fidelityfx_framegeneration_dx12.dll" };
    internal static readonly string[] IntelFiles = { "OptiScaler/libxess_fg.dll", "OptiScaler/libxell.dll" };
    internal static readonly string[] NeuralFiles = { "OptiScaler/nvngx.dll_dlssnr.dll", "OptiScaler/nvngx_dlssnr.dll" };
    internal static string ProviderHelp(int index) => index switch
    {
        0 => "NVIDIA uses AI to make in-between pictures for smoother motion. Needs a supported RTX 40 Series or newer GPU. It looked best on the tested RTX 4090; thin objects can shimmer.",
        1 => "AMD uses motion between real frames to make in-between pictures, across different GPU brands. On the tested RTX 4090 it looked and ran worse than NVIDIA.",
        2 => "Intel uses AI to make in-between pictures on supported GPUs. Needs windowed or borderless mode. On the tested RTX 4090 it made the game slower; compare before keeping it.",
        _ => "A custom provider is selected in F10. Choose a supported provider to use Home's Frame Generation controls."
    };

    internal static string ProviderRequirements(int index) => index switch
    {
        0 => "NVIDIA FG needs RTX 40 Series or newer and Hardware-accelerated GPU Scheduling. This panel changes no Windows settings.",
        1 => "AMD FG needs the optional bridge and AMD frame-generation files. Cross-vendor availability depends on the GPU and runtime.",
        2 => "Intel FG needs the optional bridge and Intel frame-generation files, in windowed or borderless mode. Runtime support varies by GPU.",
        _ => "A custom F10 provider has its own requirements."
    };
    internal static string StyleHelp(int style) => style switch
    {
        0 => "Default profile: stronger lighting and contrast; can look stylised. This mod's tested starting point.",
        1 => "Natural profile: gentler detail and colours closer to the game, according to the bridge's community testing.",
        2 => "Cinematic profile: a film-like treatment with less harsh shine, according to the bridge's community testing.",
        _ => "A model-specific visual profile is selected."
    };
    internal static string NeuralPresetHelp(int preset) => preset switch
    {
        0 => "Auto: lets the runtime choose its default model. The result depends on the installed NR runtime.",
        1 => "A: Standard model in NR 310.9.16. Keeps the original model family, with more graphics work than Lightweight FP8. Other runtimes may interpret A differently.",
        2 => "B: Lightweight FP8 in NR 310.9.16. Aims to reduce graphics work, with a different look; Big Walk performance is unverified. Other runtimes may interpret B differently.",
        3 => "Preset 3: model-specific / legacy request. No verified 310.9.16 meaning; use Auto, A or B unless comparing another runtime.",
        _ => "A model-specific preset is selected."
    };

    internal static int ProviderIndex(string value) => Array.FindIndex(ProviderValues, item => string.Equals(value, item, StringComparison.OrdinalIgnoreCase));
    internal static string ProviderName(string value)
    {
        int index = ProviderIndex(value);
        return index < 0 ? "Custom provider (F10)" : ProviderNames[index];
    }

    internal static bool BridgedBackend(string value) => value.ToLowerInvariant() is "dlss_12" or "xess_12" or "fsr21_12" or "fsr22_12" or "ffx_12";

    internal static string NvidiaReason(int vendor, string device)
    {
        if (vendor != 0 && vendor != 0x10de) return "Direct NVIDIA Frame Generation requires a supported NVIDIA RTX 40 Series or newer GPU.";
        string name = device ?? "";
        var match = Regex.Match(name, @"\bRTX\s+(\d{4})\b", RegexOptions.IgnoreCase);
        if (vendor != 0x10de || !match.Success || name.IndexOf("Quadro", StringComparison.OrdinalIgnoreCase) >= 0)
            return "GPU support could not be identified. Direct NVIDIA Frame Generation requires an RTX 40 Series or newer GPU; unsupported initialization can close the game.";
        int model = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        if (model < 4000) return "Direct NVIDIA Frame Generation requires an RTX 40 Series or newer GPU; this GPU is an older RTX generation.";
        return model % 100 < 50 ? "This NVIDIA GPU could not be identified as an RTX 40 Series or newer gaming GPU. Direct Frame Generation support is unverified." : "";
    }

    internal static string NvidiaSystemReason(int windowsBuild, int? hagsMode, bool userConfirmedHags)
    {
        if (windowsBuild < 19041) return "Direct NVIDIA Frame Generation requires Windows build 19041 or newer.";
        if (hagsMode == 1) return "Hardware-accelerated GPU Scheduling is disabled. Enable it yourself in Windows Graphics settings and restart before using NVIDIA Frame Generation.";
        if (hagsMode != 2 && !userConfirmedHags) return "Hardware-accelerated GPU Scheduling could not be verified. Check Windows Graphics settings before enabling NVIDIA Frame Generation.";
        return "";
    }

    internal static string[] ProviderFiles(string provider) => ProviderIndex(provider) switch
    { 0 => NvidiaFiles, 1 => AmdFiles, 2 => IntelFiles, _ => Array.Empty<string>() };

    internal static FrameGenProfile ReadFrameGen(Func<string, string, string?> read)
    {
        string Get(string section, string key, string fallback) => read(section, key) ?? fallback;
        return new FrameGenProfile
        {
            Enabled = Bool(Get("FrameGen", "Enabled", "false")),
            Provider = Bool(Get("fakenvapi", "ForceXeLL", "false")) ? "XeFG" : Get("FrameGen", "FGOutput", "DLSSG"),
            Signature = string.Join("|", Get("FrameGen", "Enabled", "false"), Get("FrameGen", "FGOutput", "DLSSG"),
                Get("FrameGen", "FGInput", "Upscaler"), Get("FrameGen", "FGNvngxReplacement", "None"),
                Get("DLSSG", "InterpolationCount", "1"), Get("DLSSG", "ForceDMFG", "false"),
                Get("XeFG", "InterpolationCount", "1"), Get("XeFG", "IgnoreInitChecks", "false"), Get("fakenvapi", "ForceXeLL", "false"))
        };
    }

    internal static NeuralProfile ReadNeural(Func<string, string, string?> read)
    {
        string Get(string key, string fallback) => read("DlssNr", key) ?? fallback;
        return new NeuralProfile
        {
            Enabled = Bool(Get("Enabled", "false")), Style = Integer(Get("Style", "0"), 0, 2), Preset = Integer(Get("Preset", "0"), 0, 3),
            Intensity = Number(Get("Intensity", "1"), 1, 0, 2), TransferStrength = Number(Get("TransferStrength", "1"), 1, 0, 2),
            ColourStrength = Number(Get("ColourStrength", "1"), 1, 0, 4),
            Signature = string.Join("|", Get("Enabled", "false"), Get("Style", "0"), Get("Preset", "0"),
                Get("Intensity", "1"), Get("TransferStrength", "1"), Get("ColourStrength", "1"))
        };
    }

    internal static (string Section, string Key, string Value)[] FrameGenChanges(bool enabled, string provider)
    {
        if (ProviderIndex(provider) < 0) throw new ArgumentException("Unknown frame generation provider.", nameof(provider));
        return new[] { ("FrameGen", "Enabled", enabled ? "true" : "false"), ("FrameGen", "FGOutput", provider),
            ("FrameGen", "FGInput", "Upscaler"), ("FrameGen", "FGNvngxReplacement", "None"),
            ("DLSSG", "InterpolationCount", "1"), ("DLSSG", "ForceDMFG", "false"),
            ("XeFG", "InterpolationCount", "1"), ("XeFG", "IgnoreInitChecks", "false"), ("fakenvapi", "ForceXeLL", "false") };
    }

    internal static (string Section, string Key, string Value)[] NeuralChanges(NeuralProfile profile)
    {
        return new[] { ("DlssNr", "Enabled", profile.Enabled ? "true" : "false"),
            ("DlssNr", "Style", Math.Clamp(profile.Style, 0, 2).ToString(CultureInfo.InvariantCulture)),
            ("DlssNr", "Preset", Math.Clamp(profile.Preset, 0, 3).ToString(CultureInfo.InvariantCulture)),
            ("DlssNr", "Intensity", Format(profile.Intensity, 0, 2)),
            ("DlssNr", "TransferStrength", Format(profile.TransferStrength, 0, 2)),
            ("DlssNr", "ColourStrength", Format(profile.ColourStrength, 0, 4)) };
    }

    private static bool Bool(string value) => bool.TryParse(value, out bool parsed) && parsed;
    private static int Integer(string value, int min, int max) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? Math.Clamp(parsed, min, max) : min;
    private static float Number(string value, float fallback, float min, float max) => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && float.IsFinite(parsed) ? Math.Clamp(parsed, min, max) : fallback;
    private static string Format(float value, float min, float max) => Math.Clamp(float.IsFinite(value) ? value : 1, min, max).ToString("0.###", CultureInfo.InvariantCulture);
}
