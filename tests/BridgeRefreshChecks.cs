using System;
using System.IO;
using System.Linq;
using BigWalkDLSS;

int assertions = 0;
void Check(bool value, string reason) { assertions++; if (!value) throw new Exception(reason); }
if (args.Length != 1) throw new ArgumentException("Pass the recognized OptiScaler version.dll fixture path; it is copied, never loaded or modified.");
string root = Path.Combine(Path.GetTempPath(), "big-walk-refresh-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string proxy = Path.Combine(root, "version.dll"), path = Path.Combine(root, "OptiScaler.ini");
    File.Copy(args[0], proxy);
    BepInEx.Paths.GameRootPath = root;
    BepInEx.Paths.BepInExRootPath = Path.Combine(root, "BepInEx");
    File.WriteAllText(path, "[Upscalers]\nDx11Upscaler=dlss_12\n[DLSS]\nRenderPresetOverride=true\n" +
        "[FrameGen]\nEnabled=false\nFGOutput=FSRFG\n[DlssNr]\nEnabled=false\nStyle=0\nIntensity=1\n[Logging]\nLogLevel=Debug\n");
    foreach (string relative in FeatureOptions.IntelFiles)
    {
        string dependency = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(dependency)!);
        File.WriteAllText(dependency, "fixture only: never loaded");
    }
    BridgeSettings.Initialize();
    Check(BridgeSettings.Recognized, "Recognized OptiScaler fixture required");
    string startupFrame = FeatureSettings.StartupFrameGen.Signature, startupNeural = FeatureSettings.StartupNeural.Signature;

    // F10 saves after startup, then the user opens Home and selects Intel normally.
    var initial = new IniSettings(File.ReadAllBytes(path));
    File.WriteAllBytes(path, initial.WithValues(("Logging", "LogLevel", "Info"), ("DlssNr", "Style", "2"),
        ("Upscalers", "Dx11Upscaler", "xess_12")));
    BridgeSettings.Reload(); // The Home-open path.
    Check(FeatureSettings.QueuedNeural.Style == 2 && BridgeSettings.NextLaunchBackendValue == "xess_12", "Opening Home did not read F10's saved choices");
    Check(FeatureSettings.SetFrameGen(true, "XeFG", 0x10de, "NVIDIA GeForce RTX 4090"), "Ordinary Intel choice after opening Home was refused");
    var selected = new IniSettings(File.ReadAllBytes(path));
    Check(selected.Get("FrameGen", "FGOutput") == "XeFG" && selected.Get("XeFG", "InterpolationCount") == "1", "Intel baseline was not saved");
    Check(selected.Get("Logging", "LogLevel") == "Info" && selected.Get("DlssNr", "Style") == "2", "Intel choice overwrote unrelated F10 choices");
    Check(BridgeSettings.RunningBackendValue == "dlss_12" && BridgeSettings.RunningBackend == 0 && BridgeSettings.PresetOverride, "Refresh changed this launch's backend or preset ownership");
    Check(FeatureSettings.StartupFrameGen.Signature == startupFrame && FeatureSettings.StartupNeural.Signature == startupNeural, "Refresh changed startup feature snapshots");

    // F10 and Home can coexist; the visible-menu poll reads the next save too.
    File.WriteAllBytes(path, selected.WithValues(("DlssNr", "Style", "1"), ("Other", "Keep", "from F10")));
    BridgeSettings.RefreshSavedSettings();
    Check(FeatureSettings.QueuedNeural.Style == 1 && BridgeSettings.ReadOption("Other", "Keep") == "from F10", "Visible Home did not refresh saved F10 state");
    Check(FeatureSettings.SetFrameGen(false, "XeFG", 0x10de, "NVIDIA GeForce RTX 4090"), "Provider toggle after visible refresh was refused");
    var refreshed = new IniSettings(File.ReadAllBytes(path));
    Check(refreshed.Get("DlssNr", "Style") == "1" && refreshed.Get("Other", "Keep") == "from F10", "Visible-menu provider edit overwrote new NR style or unrelated key");
    string savedMessage = BridgeSettings.Message;
    BridgeSettings.RefreshSavedSettings();
    Check(BridgeSettings.Message == savedMessage, "Unchanged poll erased save feedback");

    // A save racing a drawn NR profile is refused, never rebased over the new style.
    var staleProfile = FeatureSettings.QueuedNeural;
    staleProfile.Intensity = .8f;
    byte[] external = refreshed.WithValues(("DlssNr", "Style", "2"), ("Other", "Keep", "newer writer"));
    File.WriteAllBytes(path, external);
    Check(!FeatureSettings.SetNeural(staleProfile, 0x10de), "Stale NR profile overwrote a concurrent F10 save");
    Check(File.ReadAllBytes(path).SequenceEqual(external), "Rejected concurrent choice changed the file");
    Check(FeatureSettings.QueuedNeural.Style == 2 && BridgeSettings.Message.Contains("Choice was not saved"), "Stale choice did not report failure and reload the next displayed profile");
    var retry = FeatureSettings.QueuedNeural;
    retry.Intensity = .8f;
    Check(FeatureSettings.SetNeural(retry, 0x10de), "Fresh deliberate retry was refused after concurrent save");
    var retried = new IniSettings(File.ReadAllBytes(path));
    Check(retried.Get("DlssNr", "Style") == "2" && retried.Get("Other", "Keep") == "newer writer" && retried.Get("DlssNr", "Intensity") == "0.8", "Retry lost refreshed settings");
    Check(FeatureSettings.StartupFrameGen.Signature == startupFrame && FeatureSettings.StartupNeural.Signature == startupNeural, "Save or recovery changed startup snapshots");

    // AMD and Intel GPUs: DLSS cannot run there, and GPU spoofing would hide the real vendor.
    File.WriteAllText(path, "[Upscalers]\nDx11Upscaler=dlss_12\n[FrameGen]\nEnabled=false\n[Logging]\nLogLevel=Debug\n");
    BridgeSettings.GpuVendorId = 0x1002;
    BridgeSettings.Initialize();
    var corrected = new IniSettings(File.ReadAllBytes(path));
    Check(corrected.Get("Upscalers", "Dx11Upscaler") == "fsr31" && corrected.Get("Spoofing", "Dxgi") == "false", "AMD GPU kept DLSS or GPU spoofing for the next launch");
    Check(corrected.Get("Logging", "LogLevel") == "Debug" && corrected.Get("FrameGen", "Enabled") == "false", "Vendor correction changed unrelated settings");
    Check(BridgeSettings.RunningBackend == -1 && BridgeSettings.NextLaunchBackend == 1 && BridgeSettings.Message.Contains("FSR 3.1"), "AMD session reported DLSS or hid the restart");
    Check(BridgeSettings.UnavailableReason(0).Contains("NVIDIA") && BridgeSettings.UnavailableReason(1).Length == 0, "AMD GPU could select DLSS or lost FSR 3.1");
    byte[] settled = File.ReadAllBytes(path);
    BridgeSettings.Initialize();
    Check(File.ReadAllBytes(path).SequenceEqual(settled), "Corrected AMD settings were rewritten on the next launch");
    // A saved NVIDIA FG or NR request is switched off on these GPUs; AMD FG requests are kept.
    File.WriteAllText(path, "[Upscalers]\nDx11Upscaler=fsr31\n[Spoofing]\nDxgi=false\n[FrameGen]\nEnabled=true\nFGOutput=DLSSG\n[DlssNr]\nEnabled=true\n");
    BridgeSettings.Initialize();
    var nvidiaOnly = new IniSettings(File.ReadAllBytes(path));
    Check(nvidiaOnly.Get("FrameGen", "Enabled") == "false" && nvidiaOnly.Get("DlssNr", "Enabled") == "false", "NVIDIA-only FG or NR stayed enabled on an AMD GPU");
    Check(BridgeSettings.Message.Contains("NVIDIA Frame Generation and Neural Rendering"), "Turning off NVIDIA-only features was not explained");
    Check(FeatureSettings.StartupFrameGen.Enabled && FeatureSettings.StartupNeural.Enabled && FeatureSettings.FrameGenPending && FeatureSettings.NeuralPending,
        "This launch's running features were misreported as already off");
    File.WriteAllText(path, "[Upscalers]\nDx11Upscaler=fsr31\n[Spoofing]\nDxgi=false\n[FrameGen]\nEnabled=true\nFGOutput=FSRFG\n[DlssNr]\nEnabled=false\n");
    byte[] amdFrameGen = File.ReadAllBytes(path);
    BridgeSettings.Initialize();
    Check(File.ReadAllBytes(path).SequenceEqual(amdFrameGen), "AMD Frame Generation request was changed on an AMD GPU");
    // ForceBridgeNative testing on NVIDIA behaves as AMD/Intel without rewriting saved settings.
    File.WriteAllText(path, "[Upscalers]\nDx11Upscaler=dlss_12\n[FrameGen]\nEnabled=true\nFGOutput=DLSSG\n");
    byte[] tester = File.ReadAllBytes(path);
    BridgeSettings.SimulatedVendor = true;
    BridgeSettings.Initialize();
    BridgeSettings.SimulatedVendor = false;
    Check(File.ReadAllBytes(path).SequenceEqual(tester) && BridgeSettings.RunningBackend == -1 && BridgeSettings.UnavailableReason(0).Contains("NVIDIA"),
        "Forced AMD/Intel testing rewrote saved settings or did not behave as AMD/Intel");
    BridgeSettings.GpuVendorId = 0x10de;

    // Refresh does not read/hash the proxy, but every write still validates it.
    File.Delete(proxy);
    BridgeSettings.RefreshSavedSettings();
    Check(BridgeSettings.Recognized, "Visible refresh unnecessarily revalidated the proxy");
    byte[] beforeRejectedWrite = File.ReadAllBytes(path);
    Check(!FeatureSettings.SetFrameGen(false, "XeFG", 0x10de, "NVIDIA GeForce RTX 4090"), "Write accepted a missing proxy");
    Check(File.ReadAllBytes(path).SequenceEqual(beforeRejectedWrite) && !BridgeSettings.Recognized, "Proxy failure changed settings or left edits enabled");
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"Bridge refresh checks passed: {assertions} assertions covering Home-open and visible refresh, F10 preservation, immutable startup state, concurrent-save refusal/retry and proxy validation.");

namespace BepInEx
{
    internal static class Paths
    {
        internal static string GameRootPath = "", BepInExRootPath = "";
    }
}

namespace UnityEngine
{
    internal static class SystemInfo { internal static int graphicsDeviceVendorID => 0x10de; }
}

namespace BigWalkDLSS
{
    internal static class UpscalingPresentation { internal static string BackendName(int index) => index.ToString(); }
}
