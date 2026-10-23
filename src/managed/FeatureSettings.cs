using System;
using System.IO;
using Microsoft.Win32;

namespace BigWalkDLSS;

internal static class FeatureSettings
{
    internal static FrameGenProfile StartupFrameGen { get; private set; } = new();
    internal static NeuralProfile StartupNeural { get; private set; } = new();
    internal static FrameGenProfile QueuedFrameGen => FeatureOptions.ReadFrameGen(BridgeSettings.ReadOption);
    internal static NeuralProfile QueuedNeural => FeatureOptions.ReadNeural(BridgeSettings.ReadOption);
    internal static bool FrameGenPending => StartupFrameGen.Signature != QueuedFrameGen.Signature;
    internal static bool NeuralPending => StartupNeural.Signature != QueuedNeural.Signature ||
        (QueuedNeural.Enabled && !string.Equals(BridgeSettings.RunningBackendValue, BridgeSettings.NextLaunchBackendValue, StringComparison.OrdinalIgnoreCase));
    internal static int WindowsBuild { get; private set; }
    internal static int? HagsMode { get; private set; }

    internal static void Initialize()
    {
        StartupFrameGen = QueuedFrameGen;
        StartupNeural = QueuedNeural;
        WindowsBuild = OperatingSystem.IsWindows() ? Environment.OSVersion.Version.Build : 0;
        try
        {
            object? mode = OperatingSystem.IsWindows()
                ? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", null) : null;
            HagsMode = mode is int value ? value : null;
        }
        catch (Exception error) when (error is System.Security.SecurityException || error is UnauthorizedAccessException || error is IOException || error is PlatformNotSupportedException)
        { HagsMode = null; }
    }

    internal static string MissingFiles(string[] files)
    {
        foreach (string file in files)
            if (!BridgeSettings.HasRuntimeFile(file)) return "Missing dependency: " + Path.GetFileName(file) + ". Check the optional runtime files; Setup supplies supported dependencies.";
        return "";
    }

    internal static string FrameGenUnavailable(string provider, int vendor, string device, bool userConfirmedHags = false)
    {
        if (!BridgeSettings.Recognized) return "Frame Generation needs the optional OptiScaler installation. See Setup.";
        int index = FeatureOptions.ProviderIndex(provider);
        if (index < 0) return "Choose a Home provider to use the standard 2× configuration. Custom settings remain available in F10.";
        if (index == 0)
        {
            string hardware = FeatureOptions.NvidiaReason(vendor, device);
            if (hardware.Length > 0) return hardware;
            string system = FeatureOptions.NvidiaSystemReason(WindowsBuild, HagsMode, userConfirmedHags);
            if (system.Length > 0) return system;
        }
        return MissingFiles(FeatureOptions.ProviderFiles(provider));
    }

    internal static string NeuralUnavailable(int vendor)
    {
        if (!BridgeSettings.Recognized) return "Neural Rendering needs the optional OptiScaler installation. See Setup.";
        if (vendor != 0x10de) return "Neural Rendering needs NVIDIA NGX support; a supported NVIDIA GPU has not been identified.";
        if (!FeatureOptions.BridgedBackend(BridgeSettings.NextLaunchBackendValue))
            return "Choose DLSS or XeSS on the Upscaling tab for the next launch. The native FSR 3.1 path has no Neural Rendering call site.";
        if (!HasNeuralFile(FeatureOptions.NeuralFiles[1]))
            return "Model missing: nvngx_dlssnr.dll was not found. Download the optional NR pack from the release page and select its DLL in Setup.";
        if (!HasNeuralFile(FeatureOptions.NeuralFiles[0]))
            return "Missing Neural Rendering forwarder: nvngx.dll_dlssnr.dll in the game root or OptiScaler folder. Setup supplies the forwarder.";
        return "";
    }

    // Accept the two supported layouts independently, without treating backups as runtime files.
    private static bool HasNeuralFile(string relative) => BridgeSettings.HasRuntimeFile(relative) ||
        BridgeSettings.HasRuntimeFile(Path.GetFileName(relative));

    internal static bool SetFrameGen(bool enabled, string provider, int vendor, string device, bool userConfirmedHags = false)
    {
        if (enabled && FrameGenUnavailable(provider, vendor, device, userConfirmedHags).Length > 0) return false;
        if (!enabled && FeatureOptions.ProviderIndex(provider) < 0)
            return BridgeSettings.WriteOptions(("FrameGen", "Enabled", "false"));
        return BridgeSettings.WriteOptions(FeatureOptions.FrameGenChanges(enabled, provider));
    }

    internal static bool SetNeural(NeuralProfile profile, int vendor)
    {
        if (profile.Enabled && NeuralUnavailable(vendor).Length > 0) return false;
        return BridgeSettings.WriteOptions(FeatureOptions.NeuralChanges(profile));
    }
}
