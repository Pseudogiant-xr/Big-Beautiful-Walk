using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace BigWalkDLSS;

internal static class GpuVendor
{
    internal const int Nvidia = 0x10de, Amd = 0x1002, Intel = 0x8086;
    private const string DisplayClassId = "{4d36e968-e325-11ce-bfc1-08002be10318}";
    private const string DisplayClass = @"SYSTEM\CurrentControlSet\Control\Class\" + DisplayClassId;
    private const string PciDevices = @"SYSTEM\CurrentControlSet\Enum\PCI";

    // OptiScaler can report AMD and Intel GPUs as an NVIDIA RTX GPU. An NVIDIA report counts
    // only when NVIDIA display hardware is installed; otherwise the installed vendor is used.
    internal static int Resolve(int reported, IReadOnlyCollection<int> installed)
    {
        if (reported != Nvidia || installed.Count == 0 || Contains(installed, Nvidia)) return reported;
        if (Contains(installed, Amd)) return Amd;
        if (Contains(installed, Intel)) return Intel;
        return 0;
    }

    internal static bool IsNvidia(int vendor) => vendor == Nvidia;

    internal static string Name(int vendor) => vendor switch
    {
        Nvidia => "NVIDIA", Amd => "AMD", Intel => "Intel", _ => "non-NVIDIA"
    };

    // Vendors of display adapters Windows currently has present. Windows keeps class entries for
    // removed cards, so those are only a fallback when the present-device list cannot be read.
    internal static IReadOnlyCollection<int> InstalledVendors()
    {
        var present = PresentVendors();
        return present.Count > 0 ? present : ClassVendors();
    }

    // Enum\PCI\VEN_xxxx&DEV_... instances of the display class; a present device has a live Control key.
    private static List<int> PresentVendors()
    {
        var vendors = new List<int>();
        if (!OperatingSystem.IsWindows()) return vendors;
        try
        {
            using RegistryKey? pci = Registry.LocalMachine.OpenSubKey(PciDevices);
            if (pci == null) return vendors;
            foreach (string device in pci.GetSubKeyNames())
            {
                int vendor = ParsePciVendor(device);
                if (vendor == 0 || Contains(vendors, vendor)) continue;
                using RegistryKey? deviceKey = pci.OpenSubKey(device);
                if (deviceKey == null) continue;
                foreach (string instance in deviceKey.GetSubKeyNames())
                {
                    using RegistryKey? instanceKey = deviceKey.OpenSubKey(instance);
                    if (instanceKey == null || !string.Equals(instanceKey.GetValue("ClassGUID") as string, DisplayClassId, StringComparison.OrdinalIgnoreCase)) continue;
                    using RegistryKey? control = instanceKey.OpenSubKey("Control");
                    if (control != null) { vendors.Add(vendor); break; }
                }
            }
        }
        catch (Exception error) when (error is System.Security.SecurityException || error is UnauthorizedAccessException || error is System.IO.IOException) { }
        return vendors;
    }

    // Reads PCI vendor ids from the display adapter class, e.g. "pci\ven_10de&dev_2684".
    private static List<int> ClassVendors()
    {
        var vendors = new List<int>();
        if (!OperatingSystem.IsWindows()) return vendors;
        try
        {
            using RegistryKey? root = Registry.LocalMachine.OpenSubKey(DisplayClass);
            if (root == null) return vendors;
            foreach (string name in root.GetSubKeyNames())
            {
                if (name.Length != 4 || !int.TryParse(name, out _)) continue;
                using RegistryKey? adapter = root.OpenSubKey(name);
                int vendor = ParsePciVendor(adapter?.GetValue("MatchingDeviceId") as string);
                if (vendor != 0 && !Contains(vendors, vendor)) vendors.Add(vendor);
            }
        }
        catch (Exception error) when (error is System.Security.SecurityException || error is UnauthorizedAccessException || error is System.IO.IOException) { }
        return vendors;
    }

    internal static int ParsePciVendor(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return 0;
        int start = deviceId.IndexOf("ven_", StringComparison.OrdinalIgnoreCase);
        if (start < 0 || start + 8 > deviceId.Length) return 0;
        return int.TryParse(deviceId.Substring(start + 4, 4), System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out int vendor) ? vendor : 0;
    }

    private static bool Contains(IEnumerable<int> values, int value)
    {
        foreach (int item in values) if (item == value) return true;
        return false;
    }
}
