using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace BigWalkDLSS;

internal static class BridgeSettings
{
    private const string OptiScalerHash = "AD143C5BD6E5FAB4D1ABD8B048C424E9DDCEABF1A864E0E0695C01A0DC82B4C9";
    internal static readonly string[] BackendNames = { "DLSS", "FSR 3.1", "XeSS", "FSR 4" };
    private static readonly string[] BackendValues = { "dlss_12", "fsr31", "xess_12", "ffx_12" };
    private static string _gameRoot = "", _iniPath = "", _backupDirectory = "";
    private static IniSettings? _ini;
    internal static bool Recognized { get; private set; }
    internal static bool PresetOverride { get; private set; }
    internal static int RunningBackend { get; private set; }
    internal static int NextLaunchBackend { get; private set; }
    internal static string Message { get; private set; } = "Graphics settings are loading.";

    internal static void Initialize()
    {
        _gameRoot = Paths.GameRootPath;
        _iniPath = Path.Combine(_gameRoot, "OptiScaler.ini");
        _backupDirectory = Path.Combine(Paths.BepInExRootPath, "BigWalkDLSS", "config-backups");
        Reload();
        RunningBackend = NextLaunchBackend;
        PresetOverride = string.Equals(_ini?.Get("DLSS", "RenderPresetOverride"), "true", StringComparison.OrdinalIgnoreCase);
    }

    internal static void Reload()
    {
        _ini = null;
        Recognized = false;
        NextLaunchBackend = 0;
        try
        {
            string proxy = Path.Combine(_gameRoot, "version.dll");
            if (!File.Exists(proxy)) { Message = "DLSS is available. Other renderers need optional OptiScaler support."; return; }
            IniSettings.RejectReparsePath(proxy);
            if (IniSettings.Hash(File.ReadAllBytes(proxy)) != OptiScalerHash)
            { Message = "Renderer switching is unavailable with this installation. See the graphics guide."; NextLaunchBackend = -1; return; }
            Recognized = true;
            IniSettings.RejectReparsePath(_iniPath);
            if (!File.Exists(_iniPath))
            { Message = "Renderer settings are missing. Repair the optional OptiScaler installation."; NextLaunchBackend = -1; return; }
            _ini = new IniSettings(File.ReadAllBytes(_iniPath));
            string selected = _ini.Get("Upscalers", "Dx11Upscaler") ?? "auto";
            NextLaunchBackend = Array.FindIndex(BackendValues, value => string.Equals(value, selected, StringComparison.OrdinalIgnoreCase));
            _ini.Get("DLSS", "RenderPresetOverride"); // Reject duplicate ownership options before enabling edits.
            Message = "Renderer changes apply after restarting Big Walk.";
        }
        catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
        {
            _ini = null;
            NextLaunchBackend = -1;
            Message = "Settings could not be read. Refresh and try again.";
        }
    }

    internal static string BackendName(int index) => UpscalingPresentation.BackendName(index);

    internal static string UnavailableReason(int index)
    {
        if (index < 0 || index >= BackendValues.Length) return "Renderer unavailable.";
        if (index == 3) return SystemInfo.graphicsDeviceVendorID == 0x10de
            ? "FSR 4 requires a supported AMD GPU. It is unavailable on NVIDIA GPUs."
            : "FSR 4 requires a supported AMD GPU; support is not available in this alpha.";
        if (index == 0 && !Recognized && NextLaunchBackend == 0) return "";
        if (!Recognized || _ini == null) return "Additional renderers need optional OptiScaler support. See the graphics guide.";
        if (index == 2)
        {
            string library = Path.Combine(_gameRoot, "OptiScaler", "libxess.dll");
            if (!File.Exists(library)) return "XeSS needs its Intel runtime. See the graphics guide to install it.";
            try { IniSettings.RejectReparsePath(library); }
            catch (IOException) { return "XeSS is unavailable with this installation. See the graphics guide."; }
        }
        return "";
    }

    internal static bool SelectBackend(int index)
    {
        string unavailable = UnavailableReason(index);
        if (unavailable.Length > 0) { Message = unavailable; return false; }
        if (!Recognized) return index == 0;
        if (!Write("Upscalers", "Dx11Upscaler", BackendValues[index])) return false;
        NextLaunchBackend = index;
        Message = index == RunningBackend ? "Settings saved." : "Settings saved. Restart to use " + BackendName(index) + ".";
        return true;
    }

    internal static bool UseHomePresets()
    {
        if (!Recognized || _ini == null) return false;
        if (!Write("DLSS", "RenderPresetOverride", "false")) return false;
        // The bridge already loaded its settings. Its startup override still owns this session.
        Message = "Settings saved. Restart to use Home model presets.";
        return true;
    }

    private static bool Write(string section, string key, string value)
    {
        if (_ini == null) return false;
        try
        {
            string proxy = Path.Combine(_gameRoot, "version.dll");
            IniSettings.RejectReparsePath(proxy);
            if (!File.Exists(proxy) || IniSettings.Hash(File.ReadAllBytes(proxy)) != OptiScalerHash)
                throw new IOException("Optional proxy changed.");
            byte[] next = _ini.WithValue(section, key, value);
            IniSettings.SaveAtomic(_iniPath, _ini.Original, next, _backupDirectory);
            _ini = new IniSettings(next);
            return true;
        }
        catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
        {
            Message = "Settings changed outside this menu. Refresh and try again.";
            return false;
        }
    }
}
