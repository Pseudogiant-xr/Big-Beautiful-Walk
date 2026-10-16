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
    internal static string RunningBackendValue { get; private set; } = "";
    internal static string NextLaunchBackendValue => ReadOption("Upscalers", "Dx11Upscaler") ?? "auto";
    internal static string Message { get; private set; } = "Graphics settings are loading.";

    internal static void Initialize()
    {
        _gameRoot = Paths.GameRootPath;
        _iniPath = Path.Combine(_gameRoot, "OptiScaler.ini");
        _backupDirectory = Path.Combine(Paths.BepInExRootPath, "BigWalkDLSS", "config-backups");
        Reload();
        RunningBackend = NextLaunchBackend;
        RunningBackendValue = NextLaunchBackendValue;
        PresetOverride = string.Equals(_ini?.Get("DLSS", "RenderPresetOverride"), "true", StringComparison.OrdinalIgnoreCase);
        FeatureSettings.Initialize();
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
            if (!File.Exists(_iniPath))
            { Message = "Renderer settings are missing. Repair the optional OptiScaler installation."; NextLaunchBackend = -1; return; }
            ReadSavedSettings();
            Message = "Renderer changes apply after restarting Big Walk.";
        }
        catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
        {
            _ini = null;
            NextLaunchBackend = -1;
            Message = "Settings could not be read. Refresh and try again.";
        }
    }

    // Called at most once a second while Home is open. Proxy validation stays at
    // menu opening and writes; refreshing saved requests does not change startup state.
    internal static void RefreshSavedSettings()
    {
        if (!Recognized) return;
        try
        {
            if (ReadSavedSettings())
                Message = "Saved settings reloaded. Restart Big Walk to apply bridge changes.";
        }
        catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
        {
            _ini = null;
            NextLaunchBackend = -1;
            Message = "Saved settings could not be read. Choices cannot be saved; refresh and try again.";
        }
    }

    private static bool ReadSavedSettings()
    {
        IniSettings.RejectReparsePath(_iniPath);
        byte[] bytes = File.ReadAllBytes(_iniPath);
        if (_ini != null && bytes.AsSpan().SequenceEqual(_ini.Original)) return false;
        var next = new IniSettings(bytes);
        string selected = next.Get("Upscalers", "Dx11Upscaler") ?? "auto";
        next.Get("DLSS", "RenderPresetOverride"); // Reject duplicate ownership options before enabling edits.
        _ini = next;
        NextLaunchBackend = Array.FindIndex(BackendValues, value => string.Equals(value, selected, StringComparison.OrdinalIgnoreCase));
        return true;
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

    internal static string? ReadOption(string section, string key)
    {
        try { return _ini?.Get(section, key); }
        catch (InvalidDataException) { return null; }
    }

    internal static bool HasRuntimeFile(string relativePath)
    {
        try
        {
            string path = Path.GetFullPath(Path.Combine(_gameRoot, relativePath));
            if (!path.StartsWith(Path.GetFullPath(_gameRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
            IniSettings.RejectReparsePath(path);
            return File.Exists(path);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) { return false; }
    }

    private static bool Write(string section, string key, string value) => WriteOptions((section, key, value));

    internal static bool WriteOptions(params (string Section, string Key, string Value)[] changes)
    {
        if (_ini == null) { Message = "Choice was not saved. Refresh saved settings and try again."; return false; }
        try
        {
            string proxy = Path.Combine(_gameRoot, "version.dll");
            IniSettings.RejectReparsePath(proxy);
            if (!File.Exists(proxy) || IniSettings.Hash(File.ReadAllBytes(proxy)) != OptiScalerHash)
                throw new IOException("Optional proxy changed.");
            byte[] next = _ini.WithValues(changes);
            IniSettings.SaveAtomic(_iniPath, _ini.Original, next, _backupDirectory);
            _ini = new IniSettings(next);
            Message = "Settings saved. Restart Big Walk to apply bridge changes.";
            return true;
        }
        catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
        {
            // Do not rebase an already drawn profile over another writer's changes.
            // Reload for the next deliberate choice, retaining the stale-write refusal.
            Reload();
            Message = _ini != null
                ? "Choice was not saved. Saved settings reloaded; review them and choose again."
                : "Choice was not saved. " + Message;
            return false;
        }
    }
}
