using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Media;

namespace Stagehand
{
    static class Graphite
    {
        public static readonly Color Paper = Color.FromRgb(0x0C, 0x0C, 0x0E);
        public static readonly Color Surface = Color.FromRgb(0x13, 0x13, 0x16);
        public static readonly Color Raised = Color.FromRgb(0x18, 0x18, 0x1C);
        public static readonly Color Line = Color.FromRgb(0x26, 0x26, 0x2C);
        public static readonly Color LineSoft = Color.FromRgb(0x1C, 0x1C, 0x21);
        public static readonly Color Ink = Color.FromRgb(0xE8, 0xE8, 0xEA);
        public static readonly Color Muted = Color.FromRgb(0x8B, 0x8B, 0x95);
        public static readonly Color Live = Color.FromRgb(0x46, 0xB9, 0x7A);
        public static readonly Color Wip = Color.FromRgb(0xD7, 0xA5, 0x4A);
        public static readonly Color Dormant = Color.FromRgb(0x6E, 0x6E, 0x78);
        public static readonly FontFamily Sans = new FontFamily("Segoe UI Variable Display, Segoe UI, Yu Gothic UI, Microsoft YaHei UI, Malgun Gothic");
        public static readonly FontFamily Mono = new FontFamily("JetBrains Mono, Cascadia Mono, Consolas");

        public static SolidColorBrush Brush(Color color, byte alpha = 255)
        {
            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        }

        public static Brush Glass(byte topAlpha, byte bottomAlpha) => new LinearGradientBrush(
            Color.FromArgb(topAlpha, Ink.R, Ink.G, Ink.B), Color.FromArgb(bottomAlpha, Ink.R, Ink.G, Ink.B), new System.Windows.Point(0, 0), new System.Windows.Point(0.4, 1));

        public static double Settle(double t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            double lo = 0, hi = 1, u = t;
            for (int i = 0; i < 24; i++)
            {
                u = (lo + hi) / 2;
                var x = 3 * u * (1 - u) * (1 - u) * 0.2 + 3 * u * u * (1 - u) * 0.2 + u * u * u;
                if (x < t) lo = u; else hi = u;
            }
            return 3 * u * (1 - u) * (1 - u) * 0.8 + 3 * u * u * (1 - u) * 1.0 + u * u * u;
        }
    }

    static class SyncNudges
    {
        public const string FileName = "nudges.json";
        static readonly string FilePath = Path.Combine(Log.DataDir, FileName);

        public static Dictionary<string, double> Load()
        {
            return File.Exists(FilePath)
                ? new JavaScriptSerializer().Deserialize<Dictionary<string, double>>(File.ReadAllText(FilePath, Encoding.UTF8))
                : new Dictionary<string, double>();
        }

        public static void Save(Dictionary<string, double> nudges)
        {
            Directory.CreateDirectory(Log.DataDir);
            File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(nudges), Encoding.UTF8);
        }
    }

    sealed class Settings
    {
        public string IdleBackgroundPath = "";
        public string IdleAnchor = "Center";
        public double LyricOffsetSeconds = 0.1;
        public bool ShowStats = true;
        public bool KeepMouseOff = true;
        public int IdleAfterPauseSeconds = 20;
        public string RomanizeMode = "Off";
        public string Highlight = "Lines";
        public string Display = "";
        public string LyricFont = "";
        public string LyricSize = "M";
        public string LyricAlign = "Left";
        public string HighlightColor = "White";
        public string BackgroundStyle = "Cover";
        public bool ShowCover = true;
        public string StatsSize = "M";
        public string LightsExcluded = "";
        public string ScreensExcluded = "";
        public bool ShowTrayIcon = true;

        static readonly string FilePath = Path.Combine(Log.DataDir, "settings.json");

        public event Action Changed;

        public Settings Copy()
        {
            var copy = new Settings();
            copy.CopyFrom(this);
            return copy;
        }

        public void CopyFrom(Settings source)
        {
            foreach (var field in typeof(Settings).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                field.SetValue(this, field.GetValue(source));
        }

        public bool SameAs(Settings other) => new JavaScriptSerializer().Serialize(this) == new JavaScriptSerializer().Serialize(other);

        public static Settings Load()
        {
            return File.Exists(FilePath)
                ? new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(FilePath, Encoding.UTF8))
                : new Settings();
        }

        public void Save()
        {
            Directory.CreateDirectory(Log.DataDir);
            File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(this), Encoding.UTF8);
            Changed?.Invoke();
        }

        public void WatchFile(System.Windows.Threading.Dispatcher dispatcher)
        {
            WatchDataFile("settings.json", dispatcher, () =>
            {
                CopyFrom(Load());
                Changed?.Invoke();
            });
        }

        public static void WatchDataFile(string name, System.Windows.Threading.Dispatcher dispatcher, Action reloaded)
        {
            Directory.CreateDirectory(Log.DataDir);
            var reload = new System.Windows.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(150), System.Windows.Threading.DispatcherPriority.Normal, (s, e) =>
            {
                ((System.Windows.Threading.DispatcherTimer)s).Stop();
                reloaded();
            }, dispatcher);
            reload.Stop();
            var watcher = new FileSystemWatcher(Log.DataDir, name) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName };
            FileSystemEventHandler restart = (s, e) => dispatcher.BeginInvoke(new Action(() => { reload.Stop(); reload.Start(); }));
            watcher.Changed += restart;
            watcher.Created += restart;
            watcher.Renamed += (s, e) => restart(s, e);
            watcher.EnableRaisingEvents = true;
            watchers.Add(watcher);
        }

        static readonly System.Collections.Generic.List<FileSystemWatcher> watchers = new System.Collections.Generic.List<FileSystemWatcher>();
    }
}
