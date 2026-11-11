using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Win32;

namespace Stagehand
{
    static class Installer
    {
        public static readonly string InstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Stagehand");
        public static readonly string InstalledExe = Path.Combine(InstallDir, "Stagehand.exe");
        static readonly string MenuShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Stagehand.lnk");
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Stagehand";

        public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        public static string InstalledVersion
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(UninstallKey))
                    return File.Exists(InstalledExe) ? key?.GetValue("DisplayVersion") as string ?? "unknown" : null;
            }
        }

        public static bool IsSetup(string exePath) => Path.GetFileName(exePath).StartsWith("Stagehand-Setup", StringComparison.OrdinalIgnoreCase);

        public static void Install(bool startWithWindows)
        {
            startWithWindows |= RemoveGraphiteLyrics();
            StopInstalledCopies();
            Directory.CreateDirectory(InstallDir);
            if (!string.Equals(Path.GetFullPath(Program.ExePath), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase))
                File.Copy(Program.ExePath, InstalledExe, true);
            Shortcut.Create(MenuShortcut, InstalledExe, "", "Runs the case lighting, screens and the 9.2-inch lyrics screen");
            if (startWithWindows) StartupShortcut.Create(InstalledExe); else StartupShortcut.Remove();

            using (var key = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                key.SetValue("DisplayName", "Stagehand");
                key.SetValue("DisplayVersion", Version);
                key.SetValue("Publisher", "Project Graphite");
                key.SetValue("DisplayIcon", InstalledExe);
                key.SetValue("InstallLocation", InstallDir);
                key.SetValue("URLInfoAbout", "https://github.com/Project-Graphite/stagehand");
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

        static bool RemoveGraphiteLyrics()
        {
            const string legacy = "GraphiteLyrics";
            if (EventWaitHandle.TryOpenExisting(legacy + ".Quit", out var quit)) using (quit) quit.Set();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (Mutex.TryOpenExisting(legacy + ".Agent", out var agent) && DateTime.UtcNow < deadline)
            {
                agent.Dispose();
                Thread.Sleep(100);
            }
            foreach (var process in Process.GetProcessesByName(legacy))
            {
                process.Kill();
                process.WaitForExit(3000);
            }
            var startup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Graphite Lyrics.lnk");
            var hadStartup = File.Exists(startup);
            foreach (var shortcut in new[] { startup, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Graphite Lyrics.lnk") })
                if (File.Exists(shortcut)) File.Delete(shortcut);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + legacy, false);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var oldInstall = Path.Combine(localAppData, "Programs", "Graphite Lyrics");
            if (Directory.Exists(oldInstall)) Directory.Delete(oldInstall, true);
            var oldData = Path.Combine(localAppData, legacy);
            if (Directory.Exists(oldData) && !Directory.Exists(Log.DataDir)) Directory.Move(oldData, Log.DataDir);
            return hadStartup;
        }

        static void StopInstalledCopies()
        {
            Program.StopAgent();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (Program.AgentRunning && DateTime.UtcNow < deadline) Thread.Sleep(100);
            var self = Process.GetCurrentProcess().Id;
            foreach (var process in Process.GetProcessesByName("Stagehand").Where(p => p.Id != self))
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
