using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using BigWalkDLSS;

int assertions = 0;
void Check(bool value, string reason) { assertions++; if (!value) throw new Exception(reason); }
void Reject(Action action, string reason)
{
    bool rejected = false;
    try { action(); } catch (Exception error) when (error is IOException || error is InvalidDataException) { rejected = true; }
    Check(rejected, reason);
}
string original = "; untouched header\r\n[Upscalers]\r\n Dx11Upscaler = dlss_12  ; keep this comment\r\nOther=keep\r\n\r\n[DLSS]\r\nRenderPresetOverride=true\r\nRenderPresetForAll=12\r\n[Other]\r\nOpaque=unchanged";
byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(original)).ToArray();
var ini = new IniSettings(bytes);
Check(ini.Get("upscalers", "DX11UPSCALER") == "dlss_12", "Read must be case-insensitive and omit inline comments");
byte[] changed = ini.WithValue("Upscalers", "Dx11Upscaler", "fsr31");
Check(changed.Take(3).SequenceEqual(Encoding.UTF8.GetPreamble()), "UTF-8 BOM lost");
Check(Encoding.UTF8.GetString(changed, 3, changed.Length - 3) == original.Replace("dlss_12", "fsr31"), "Unrelated INI bytes, whitespace, comments or line endings changed");
var next = new IniSettings(changed);
byte[] ownership = next.WithValue("DLSS", "RenderPresetOverride", "false");
Check(Encoding.UTF8.GetString(ownership, 3, ownership.Length - 3) == original.Replace("dlss_12", "fsr31").Replace("RenderPresetOverride=true", "RenderPresetOverride=false"), "Ownership edit changed other preset options");
Check(new IniSettings(bytes).Original.SequenceEqual(bytes), "Original snapshot changed");
foreach (Encoding encoding in new Encoding[] { new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
{
    byte[] encoded = encoding.GetPreamble().Concat(encoding.GetBytes(original)).ToArray();
    byte[] result = new IniSettings(encoded).WithValue("Upscalers", "Dx11Upscaler", "xess_12");
    Check(result.SequenceEqual(encoding.GetPreamble().Concat(encoding.GetBytes(original.Replace("dlss_12", "xess_12")))), "UTF-16 BOM or content changed");
}
var missing = new IniSettings(Encoding.UTF8.GetBytes("[Upscalers]\nOther=yes\n[Other]\nKeep=1"));
Check(Encoding.UTF8.GetString(missing.WithValue("Upscalers", "Dx11Upscaler", "fsr31")) == "[Upscalers]\nOther=yes\nDx11Upscaler=fsr31\n[Other]\nKeep=1", "Missing key inserted into wrong section or with wrong newline");
Check(Encoding.UTF8.GetString(missing.WithValue("DLSS", "RenderPresetOverride", "false")) == "[Upscalers]\nOther=yes\n[Other]\nKeep=1\n[DLSS]\nRenderPresetOverride=false\n", "Missing section appended incorrectly");
var emptyValue = new IniSettings(Encoding.UTF8.GetBytes("[DLSS]\nRenderPresetOverride= ; preserve\n"));
Check(new IniSettings(emptyValue.WithValue("DLSS", "RenderPresetOverride", "false")).Get("DLSS", "RenderPresetOverride") == "false", "An empty value's comment became part of the replacement value");
foreach (string ambiguous in new[] {
    "[Upscalers]\nDx11Upscaler=dlss_12\nDx11Upscaler=fsr31\n",
    "[Upscalers]\nDx11Upscaler=dlss_12\n[UPSCALERS]\nOther=1\n" })
    Reject(() => new IniSettings(Encoding.UTF8.GetBytes(ambiguous)).WithValue("Upscalers", "Dx11Upscaler", "xess_12"), "Ambiguous target key or section accepted");

string root = Path.Combine(Path.GetTempPath(), "big-walk-bridge-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string path = Path.Combine(root, "OptiScaler.ini"), backup = Path.Combine(root, "backups");
    File.WriteAllBytes(path, bytes);
    IniSettings.SaveAtomic(path, bytes, changed, backup);
    Check(File.ReadAllBytes(path).SequenceEqual(changed), "Atomic edit did not replace the intended bytes");
    Check(Directory.GetFiles(backup).Length == 1, "Original was not backed up exactly once");
    Check(File.ReadAllBytes(Directory.GetFiles(backup)[0]).SequenceEqual(bytes), "Original backup differs");
    Reject(() => IniSettings.SaveAtomic(path, bytes, ownership, backup), "Stale snapshot overwrote external changes");
    Check(File.ReadAllBytes(path).SequenceEqual(changed), "Stale edit changed the file");
    Check(Directory.GetFiles(backup).Length == 1, "Failed stale edit created an extra backup");
    File.WriteAllBytes(path, bytes);
    IniSettings.SaveAtomic(path, bytes, ownership, backup);
    Check(Directory.GetFiles(backup).Length == 1, "Identical original snapshot backed up more than once");
    string blocker = Path.Combine(root, "not-a-directory");
    File.WriteAllText(blocker, "blocked");
    Reject(() => IniSettings.SaveAtomic(path, ownership, changed, blocker), "Backup failure did not abort the write");
    Check(File.ReadAllBytes(path).SequenceEqual(ownership), "Backup failure changed the target");
    Check(Directory.GetFiles(root, "*.tmp").Length == 0, "Temporary INI leaked");
    string link = Path.Combine(root, "linked-backups");
    bool linked = false;
    if (OperatingSystem.IsWindows())
    {
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("New-Item -ItemType Junction -Path '" + link.Replace("'", "''") + "' -Target '" + backup.Replace("'", "''") + "' -ErrorAction Stop | Out-Null");
        using var process = Process.Start(start) ?? throw new Exception("Could not start junction fixture creator");
        process.WaitForExit();
        Check(process.ExitCode == 0, "Could not create Windows junction fixture");
        linked = true;
    }
    else
    {
        try { Directory.CreateSymbolicLink(link, backup); linked = true; }
        catch (UnauthorizedAccessException) { Console.WriteLine("Reparse fixture unavailable: this platform denies symbolic-link creation."); }
        catch (PlatformNotSupportedException) { Console.WriteLine("Reparse fixture unavailable on this platform."); }
    }
    if (linked)
    {
        Reject(() => IniSettings.SaveAtomic(path, ownership, changed, link), "Linked backup directory accepted");
        Check(File.ReadAllBytes(path).SequenceEqual(ownership), "Linked backup refusal changed the target");
        Directory.Delete(link);
    }
    bool invalid = false;
    try { new IniSettings(new byte[] { 0xff, 0xfd }); } catch (DecoderFallbackException) { invalid = true; }
    Check(invalid, "Invalid encoding was silently rewritten");
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"Bridge checks passed: {assertions} assertions covering lossless INI editing, encoding, ambiguity, stale writes, atomic replacement and backups.");
