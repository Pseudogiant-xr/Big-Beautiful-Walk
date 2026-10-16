using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Big Beautiful Walk Setup")]
[assembly: AssemblyVersion("0.7.0.0")]

internal static class SetupLauncher
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 0)
                throw new InvalidOperationException("For command-line options, use Setup.cmd or scripts\\Setup.ps1.");
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            string script = Path.Combine(directory, "scripts", "Setup.ps1");
            if (!File.Exists(script))
                throw new FileNotFoundException("Setup files are missing. Extract the complete release ZIP before opening Setup.exe.");
            var start = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell", "v1.0", "powershell.exe"),
                Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -STA -File \"" + script + "\"",
                WorkingDirectory = directory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process process = Process.Start(start))
            {
                if (process == null) throw new InvalidOperationException("Windows PowerShell could not be started.");
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Setup stopped before completing. Run Setup.cmd to view diagnostic details.");
                return 0;
            }
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Big Beautiful Walk Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
