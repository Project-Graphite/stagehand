using System;
using System.IO;
using System.Reflection;

namespace Stagehand
{
    static class Shortcut
    {
        public static void Create(string path, string target, string arguments, string description)
        {
            var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            var link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
            var type = link.GetType();
            type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
            type.InvokeMember("Arguments", BindingFlags.SetProperty, null, link, new object[] { arguments });
            type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { Path.GetDirectoryName(target) });
            type.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { description });
            type.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
        }
    }

    static class StartupShortcut
    {
        static readonly string ShortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Stagehand.lnk");

        public static bool Exists => File.Exists(ShortcutPath);

        public static void Create() => Create(File.Exists(Installer.InstalledExe) ? Installer.InstalledExe : Program.ExePath);

        public static void Create(string exe) => Shortcut.Create(ShortcutPath, exe, "--agent", "Stagehand background agent");

        public static void Remove()
        {
            if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
        }
    }
}
