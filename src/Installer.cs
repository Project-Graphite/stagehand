using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Win32;

namespace GraphiteLyrics
{
    static class Installer
    {
        public static readonly string InstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Graphite Lyrics");
        public static readonly string InstalledExe = Path.Combine(InstallDir, "GraphiteLyrics.exe");
        static readonly string MenuShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Graphite Lyrics.lnk");
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\GraphiteLyrics";

        public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        public static string InstalledVersion
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(UninstallKey))
                    return File.Exists(InstalledExe) ? key?.GetValue("DisplayVersion") as string ?? "unknown" : null;
            }
        }

        public static bool IsSetup(string exePath) => Path.GetFileName(exePath).StartsWith("GraphiteLyrics-Setup", StringComparison.OrdinalIgnoreCase);

        public static void Install(bool startWithWindows)
        {
            StopInstalledCopies();
            Directory.CreateDirectory(InstallDir);
            if (!string.Equals(Path.GetFullPath(Program.ExePath), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase))
                File.Copy(Program.ExePath, InstalledExe, true);
            Shortcut.Create(MenuShortcut, InstalledExe, "", "Synced lyrics for a Lian Li 9.2-inch case screen");
            if (startWithWindows) StartupShortcut.Create(InstalledExe); else StartupShortcut.Remove();

            using (var key = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                key.SetValue("DisplayName", "Graphite Lyrics");
                key.SetValue("DisplayVersion", Version);
                key.SetValue("Publisher", "Project Graphite");
                key.SetValue("DisplayIcon", InstalledExe);
                key.SetValue("InstallLocation", InstallDir);
                key.SetValue("URLInfoAbout", "https://github.com/Project-Graphite/graphite-lyrics");
                key.SetValue("UninstallString", "\"" + InstalledExe + "\" --uninstall");
                key.SetValue("QuietUninstallString", "\"" + InstalledExe + "\" --uninstall --quiet");
                key.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        public static void Uninstall()
        {
            StopInstalledCopies();
            if (File.Exists(MenuShortcut)) File.Delete(MenuShortcut);
            StartupShortcut.Remove();
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            Process.Start(new ProcessStartInfo("cmd.exe", "/c timeout /t 2 /nobreak >nul & rmdir /s /q \"" + InstallDir + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        static void StopInstalledCopies()
        {
            Program.StopAgent();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (Program.AgentRunning && DateTime.UtcNow < deadline) Thread.Sleep(100);
            var self = Process.GetCurrentProcess().Id;
            foreach (var process in Process.GetProcessesByName("GraphiteLyrics").Where(p => p.Id != self))
            {
                string path;
                try { path = process.MainModule.FileName; } catch (System.ComponentModel.Win32Exception) { continue; }
                if (!string.Equals(path, InstalledExe, StringComparison.OrdinalIgnoreCase)) continue;
                process.Kill();
                process.WaitForExit(3000);
            }
        }
    }
}
