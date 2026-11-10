using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace GraphiteLyrics
{
    static class StartupShortcut
    {
        static readonly string ShortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Graphite Lyrics.lnk");

        public static bool Exists => File.Exists(ShortcutPath);

        public static void Create()
        {
            var exe = Process.GetCurrentProcess().MainModule.FileName;
            var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            var link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { ShortcutPath });
            var type = link.GetType();
            type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { exe });
            type.InvokeMember("Arguments", BindingFlags.SetProperty, null, link, new object[] { "--agent" });
            type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { Path.GetDirectoryName(exe) });
            type.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { "Graphite Lyrics background agent" });
            type.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
        }

        public static void Remove()
        {
            if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
        }
    }
}
