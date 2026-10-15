using System;
using System.Collections.Generic;
using System.Globalization;
using BigWalkDLSS;

static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
static string? Empty(string section, string key) => null;
static string Value((string Section, string Key, string Value)[] values, string section, string key) => Array.Find(values, item => item.Section == section && item.Key == key).Value;

var frame = FeatureOptions.ReadFrameGen(Empty);
var neural = FeatureOptions.ReadNeural(Empty);
Check(!frame.Enabled && !neural.Enabled, "Optional features must start off");
Check(neural.Style == 0 && FeatureOptions.StyleNames[0] == "Default", "Neural default changed to an alternate style");
foreach (string provider in FeatureOptions.ProviderValues)
{
    var changes = FeatureOptions.FrameGenChanges(true, provider);
    Check(Value(changes, "FrameGen", "Enabled") == "true" && Value(changes, "FrameGen", "FGOutput") == provider, "Provider output mapping incorrect");
    Check(Value(changes, "FrameGen", "FGInput") == "Upscaler" && Value(changes, "FrameGen", "FGNvngxReplacement") == "None", "Direct route replaced with a different integration");
    Check(Value(changes, "DLSSG", "InterpolationCount") == "1" && Value(changes, "DLSSG", "ForceDMFG") == "false", "Unverified NVIDIA multiplier exposed");
    Check(Value(changes, "XeFG", "InterpolationCount") == "1" && Value(changes, "XeFG", "IgnoreInitChecks") == "false", "Intel baseline bypassed capability checks");
    Check(Value(changes, "fakenvapi", "ForceXeLL") == "false", "Forced XeLL can override the chosen provider");
}
bool rejected = false;
try { FeatureOptions.FrameGenChanges(true, "OptiFG"); } catch (ArgumentException) { rejected = true; }
Check(rejected, "Invalid provider accepted");
Check(FeatureOptions.ProviderIndex("xefg") == 2 && FeatureOptions.ProviderIndex("Nukems") == -1, "Provider identity incorrect");
Check(FeatureOptions.NvidiaReason(0x10de, "NVIDIA GeForce RTX 4090").Length == 0, "RTX 4090 rejected for direct FG");
Check(FeatureOptions.NvidiaReason(0x10de, "NVIDIA GeForce RTX 5090").Length == 0, "RTX 50 Series rejected for direct FG");
foreach (var gpu in new[] { (0x10de, "NVIDIA GeForce RTX 3090"), (0x1002, "AMD Radeon"), (0, "Unknown"), (0x10de, "NVIDIA RTX A6000"), (0x10de, "NVIDIA Quadro RTX 6000"), (0x10de, "NVIDIA RTX 5000 Ada") })
    Check(FeatureOptions.NvidiaReason(gpu.Item1, gpu.Item2).Length > 0, "Unsupported or unidentified direct NVIDIA GPU accepted");
Check(FeatureOptions.NvidiaSystemReason(19041, 2, false).Length == 0, "Known NVIDIA system prerequisites rejected");
Check(FeatureOptions.NvidiaSystemReason(19040, 2, true).Length > 0, "Old Windows build accepted");
Check(FeatureOptions.NvidiaSystemReason(26100, 1, true).Length > 0, "User acknowledgement bypassed known disabled HAGS");
Check(FeatureOptions.NvidiaSystemReason(26100, null, false).Length > 0 && FeatureOptions.NvidiaSystemReason(26100, null, true).Length == 0, "Unknown HAGS was assumed enabled");
foreach (string backend in new[] { "dlss_12", "xess_12", "fsr21_12", "fsr22_12", "ffx_12", "DLSS_12" }) Check(FeatureOptions.BridgedBackend(backend), "NR callsite bridge rejected");
foreach (string backend in new[] { "dlss", "xess", "fsr31", "fsr22", "auto", "" }) Check(!FeatureOptions.BridgedBackend(backend), "Native backend incorrectly has an NR callsite");

var previousCulture = CultureInfo.CurrentCulture;
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
neural.Enabled = true; neural.Style = 0; neural.Preset = 3; neural.Intensity = .7f; neural.TransferStrength = 1.2f; neural.ColourStrength = 3.4f;
var nrChanges = FeatureOptions.NeuralChanges(neural);
Check(Value(nrChanges, "DlssNr", "Intensity") == "0.7", "INI strength used locale decimal separators");
Check(Value(nrChanges, "DlssNr", "Style") == "0" && Value(nrChanges, "DlssNr", "Preset") == "3", "NR style/preset mapping changed");
foreach (var option in nrChanges) Check(option.Section == "DlssNr" && option.Key != "Mode" && option.Key != "ReversibleMode", "Basic UI rewrote codec/composition experiments");
neural.Intensity = float.NaN; neural.TransferStrength = -1; neural.ColourStrength = 99; neural.Style = -1; neural.Preset = 9;
var bounded = FeatureOptions.NeuralChanges(neural);
Check(Value(bounded, "DlssNr", "Intensity") == "1" && Value(bounded, "DlssNr", "TransferStrength") == "0" && Value(bounded, "DlssNr", "ColourStrength") == "4", "Nonfinite or out-of-range strength written");
Check(Value(bounded, "DlssNr", "Style") == "0" && Value(bounded, "DlssNr", "Preset") == "3", "Style/preset outside provider bounds");
CultureInfo.CurrentCulture = previousCulture;

BridgeSettings.Values[("Upscalers", "Dx11Upscaler")] = "dlss_12";
FeatureSettings.Initialize();
Check(!FeatureSettings.StartupFrameGen.Enabled && !FeatureSettings.StartupNeural.Enabled, "Startup snapshots incorrect");
foreach (string file in FeatureOptions.AmdFiles) BridgeSettings.Files.Add(file);
foreach (string file in FeatureOptions.IntelFiles) BridgeSettings.Files.Add(file);
Check(FeatureSettings.FrameGenUnavailable("FSRFG", 0x10de, "NVIDIA GeForce RTX 4090") == "", "AMD provider incorrectly vendor-gated");
Check(FeatureSettings.FrameGenUnavailable("XeFG", 0x1002, "AMD Radeon") == "", "Intel provider incorrectly Intel-only");
BridgeSettings.Values[("fakenvapi", "ForceXeLL")] = "true";
BridgeSettings.Values[("fakenvapi", "KeepOtherSetting")] = "unchanged";
Check(FeatureSettings.QueuedFrameGen.Provider == "XeFG", "Forced XeLL startup route was reported as NVIDIA");
Check(FeatureSettings.SetFrameGen(true, "FSRFG", 0x10de, "NVIDIA GeForce RTX 4090"), "Direct AMD baseline not saved");
Check(FeatureSettings.QueuedFrameGen.Provider == "FSRFG" && BridgeSettings.ReadOption("fakenvapi", "ForceXeLL") == "false", "AMD choice remains overridden by forced XeLL");
Check(BridgeSettings.ReadOption("fakenvapi", "KeepOtherSetting") == "unchanged", "Provider selection changed unrelated settings");
Check(FeatureSettings.FrameGenPending && FeatureSettings.QueuedFrameGen.Enabled && !FeatureSettings.StartupFrameGen.Enabled, "Queued FG falsely reported as startup state");
BridgeSettings.Files.Remove(FeatureOptions.IntelFiles[0]);
Check(!FeatureSettings.SetFrameGen(true, "XeFG", 0x1002, "AMD Radeon"), "Missing FG dependency ignored");
Check(FeatureSettings.SetFrameGen(false, "XeFG", 0x1002, "AMD Radeon"), "Missing dependency prevented disabling FG");
Check(FeatureSettings.NeuralUnavailable(0x10de).Contains("Model missing"), "Absent NR model not explained");
foreach (string file in FeatureOptions.NeuralFiles) BridgeSettings.Files.Add(file);
Check(FeatureSettings.NeuralUnavailable(0x10de) == "", "User NR model/forwarder with bridged path rejected");
foreach (string file in FeatureOptions.NeuralFiles) BridgeSettings.Files.Remove(file);
BridgeSettings.Files.Add("nvngx_dlssnr.dll"); BridgeSettings.Files.Add("nvngx.dll_dlssnr.dll");
Check(FeatureSettings.NeuralUnavailable(0x10de) == "", "Existing root-only NR layout rejected");
BridgeSettings.Files.Remove("nvngx.dll_dlssnr.dll"); BridgeSettings.Files.Add(FeatureOptions.NeuralFiles[0]);
Check(FeatureSettings.NeuralUnavailable(0x10de) == "", "Root model with OptiScaler forwarder rejected");
BridgeSettings.Files.Remove("nvngx_dlssnr.dll"); BridgeSettings.Files.Remove(FeatureOptions.NeuralFiles[0]);
BridgeSettings.Files.Add(FeatureOptions.NeuralFiles[1]); BridgeSettings.Files.Add("nvngx.dll_dlssnr.dll");
Check(FeatureSettings.NeuralUnavailable(0x10de) == "", "OptiScaler model with root forwarder rejected");
BridgeSettings.Files.Remove(FeatureOptions.NeuralFiles[1]);
BridgeSettings.Files.Add("nvngx_dlssnr_real.dll"); BridgeSettings.Files.Add("backup/nvngx_dlssnr.dll");
Check(FeatureSettings.NeuralUnavailable(0x10de).Contains("Model missing"), "Backup or renamed NR model incorrectly accepted");
BridgeSettings.Files.Add("nvngx_dlssnr.dll"); BridgeSettings.Files.Remove("nvngx.dll_dlssnr.dll");
BridgeSettings.Files.Add("nvngx.dll_dlssnr_real.dll"); BridgeSettings.Files.Add("backup/nvngx.dll_dlssnr.dll");
Check(FeatureSettings.NeuralUnavailable(0x10de).Contains("forwarder"), "Backup or renamed NR forwarder incorrectly accepted");
BridgeSettings.Files.Add("nvngx.dll_dlssnr.dll");
var nr = FeatureSettings.QueuedNeural; nr.Enabled = true;
Check(FeatureSettings.SetNeural(nr, 0x10de), "NR saved request rejected");
Check(FeatureSettings.NeuralPending && FeatureSettings.QueuedNeural.Enabled && !FeatureSettings.StartupNeural.Enabled, "Queued NR falsely reported as startup state");
BridgeSettings.Values[("Upscalers", "Dx11Upscaler")] = "fsr31";
Check(!FeatureSettings.SetNeural(nr, 0x10de), "Native FSR 3.1 NR callsite invented");
nr.Enabled = false;
Check(FeatureSettings.SetNeural(nr, 0x10de), "Native backend prevented disabling NR");
BridgeSettings.Recognized = false;
Check(FeatureSettings.FrameGenUnavailable("FSRFG", 0x10de, "NVIDIA GeForce RTX 4090").Length > 0, "Missing proxy accepted");
Console.WriteLine("Feature checks passed: provider batches, NR exact keys/bounds, GPU/Windows/HAGS guards, cross-vendor routes, missing files and immutable startup/queued state.");

namespace BigWalkDLSS
{
    // CPU-only bridge double: records requested file changes without a game or DLL load.
    internal static class BridgeSettings
    {
        internal static readonly Dictionary<(string, string), string> Values = new();
        internal static readonly HashSet<string> Files = new();
        internal static bool Recognized = true;
        internal static string RunningBackendValue => "dlss_12";
        internal static string NextLaunchBackendValue => ReadOption("Upscalers", "Dx11Upscaler") ?? "auto";
        internal static string? ReadOption(string section, string key) => Values.TryGetValue((section, key), out string? value) ? value : null;
        internal static bool HasRuntimeFile(string relative) => Files.Contains(relative);
        internal static bool WriteOptions(params (string Section, string Key, string Value)[] changes)
        {
            if (!Recognized) return false;
            foreach (var change in changes) Values[(change.Section, change.Key)] = change.Value;
            return true;
        }
    }
}
