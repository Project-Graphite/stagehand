using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Stagehand
{
    sealed class SleepTimer
    {
        public DateTime Until;
        public bool Lights;
        public bool Screens;

        public const string FileName = "sleep-timer.json";
        static readonly string FilePath = Path.Combine(Log.DataDir, FileName);

        public static SleepTimer Load() => File.Exists(FilePath) ? new JavaScriptSerializer().Deserialize<SleepTimer>(File.ReadAllText(FilePath, Encoding.UTF8)) : null;

        public void Save()
        {
            Directory.CreateDirectory(Log.DataDir);
            File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(this), Encoding.UTF8);
        }

        public static void Cancel() => File.Delete(FilePath);
    }

    static class Hotkeys
    {
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr window, int id);

        static readonly string[] ModifierNames = { "Ctrl", "Alt", "Shift", "Win" };
        static readonly ModifierKeys[] ModifierValues = { ModifierKeys.Control, ModifierKeys.Alt, ModifierKeys.Shift, ModifierKeys.Windows };

        public static string Format(ModifierKeys modifiers, Key key) =>
            string.Join("+", ModifierNames.Where((name, i) => modifiers.HasFlag(ModifierValues[i])).Concat(new[] { key.ToString() }));

        public static string Display(string hotkey) =>
            hotkey.Length == 0 ? "Not set" : string.Join(" + ", hotkey.Split('+').Select(part => Regex.IsMatch(part, @"^D\d$") ? part.Substring(1) : part));

        public static bool Register(IntPtr window, int id, string hotkey)
        {
            var parts = hotkey.Split('+');
            var modifiers = parts.Take(parts.Length - 1).Aggregate(ModifierKeys.None, (all, name) => all | ModifierValues[Array.IndexOf(ModifierNames, name)]);
            var key = (Key)Enum.Parse(typeof(Key), parts.Last());
            return RegisterHotKey(window, id, (uint)modifiers | 0x4000, (uint)KeyInterop.VirtualKeyFromKey(key));
        }
    }

    sealed class Routines
    {
        const double FadeSeconds = 60;
        readonly Settings settings;
        readonly LyricsWindow window;
        readonly HwndSource messages = new HwndSource(0, 0, 0, 0, 0, "Stagehand shortcuts", IntPtr.Zero);
        SleepTimer sleep = SleepTimer.Load();
        string fadingApp;
        double? volumeBefore;
        bool wasNight, ticking, switching;
        DateTime nightCheckAt = DateTime.MinValue;

        public Routines(Settings settings, LyricsWindow window)
        {
            this.settings = settings;
            this.window = window;
            Settings.WatchDataFile(SleepTimer.FileName, window.Dispatcher, () =>
            {
                sleep = SleepTimer.Load();
                if (sleep == null) EndFade();
            });
            messages.AddHook(OnMessage);
            settings.Changed += RegisterHotkeys;
            RegisterHotkeys();
            var tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            tick.Tick += async (s, e) => await Tick();
            tick.Start();
        }

        async Task Tick()
        {
            if (ticking) return;
            ticking = true;
            try
            {
                if (sleep != null) await SleepTick();
                if (DateTime.UtcNow >= nightCheckAt)
                {
                    nightCheckAt = DateTime.UtcNow.AddSeconds(20);
                    await NightTick();
                }
            }
            catch (Exception error) when (error is HttpRequestException || error is TaskCanceledException || error is InvalidOperationException || error is IOException)
            {
                Log.Write("routines: " + error.Message);
            }
            ticking = false;
        }

        async Task SleepTick()
        {
            var left = (sleep.Until - DateTime.UtcNow).TotalSeconds;
            var player = window.Player;
            if (left > FadeSeconds) return;
            if (left > 0)
            {
                if (volumeBefore == null && player.AppId != null)
                {
                    fadingApp = player.AppId;
                    volumeBefore = AppVolume.Get(fadingApp);
                }
                if (volumeBefore != null) AppVolume.Set(fadingApp, volumeBefore.Value * left / FadeSeconds);
                return;
            }
            var ending = sleep;
            if (player.IsPlaying) player.Pause();
            await Task.Delay(1500);
            EndFade();
            SleepTimer.Cancel();
            sleep = null;
            if (ending.Lights) await CaseSwitches.Load().SetLightsOffAsync(true, settings.LightsExcluded);
            if (ending.Screens) await CaseSwitches.Load().SetScreensOffAsync(true, settings.ScreensExcluded);
        }

        void EndFade()
        {
            if (volumeBefore != null) AppVolume.Set(fadingApp, volumeBefore.Value);
            volumeBefore = null;
        }

        async Task NightTick()
        {
            var night = settings.NightEnabled && InWindow(DateTime.Now.TimeOfDay, TimeSpan.Parse(settings.NightStart), TimeSpan.Parse(settings.NightEnd));
            var switches = CaseSwitches.Load();
            if (night && !wasNight)
            {
                if (settings.NightLights && !switches.LightsOff)
                {
                    await switches.SetLightsOffAsync(true, settings.LightsExcluded);
                    switches.LightsOffForNight = true;
                    switches.Save();
                }
                if (settings.NightScreens && !switches.ScreensOff)
                {
                    await switches.SetScreensOffAsync(true, settings.ScreensExcluded);
                    switches.ScreensOffForNight = true;
                    switches.Save();
                }
            }
            if (!night && switches.LightsOffForNight)
            {
                await switches.SetLightsOffAsync(false, settings.LightsExcluded);
                switches.LightsOffForNight = false;
                switches.Save();
            }
            if (!night && switches.ScreensOffForNight)
            {
                await switches.SetScreensOffAsync(false, settings.ScreensExcluded);
                switches.ScreensOffForNight = false;
                switches.Save();
            }
            wasNight = night;
        }

        static bool InWindow(TimeSpan now, TimeSpan start, TimeSpan end) => start <= end ? now >= start && now < end : now >= start || now < end;

        void RegisterHotkeys()
        {
            var hotkeys = new[] { settings.HotkeyLights, settings.HotkeyScreens, settings.HotkeyEarlier, settings.HotkeyLater };
            for (var i = 0; i < hotkeys.Length; i++)
            {
                Hotkeys.UnregisterHotKey(messages.Handle, i + 1);
                if (hotkeys[i].Length > 0 && !Hotkeys.Register(messages.Handle, i + 1, hotkeys[i])) Log.Write("shortcut " + hotkeys[i] + " is taken by another app");
            }
        }

        IntPtr OnMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != 0x0312) return IntPtr.Zero;
            handled = true;
            RunShortcut(wParam.ToInt32());
            return IntPtr.Zero;
        }

        async void RunShortcut(int id)
        {
            if (id > 2)
            {
                if (window.TrackKey != null) SyncNudges.Nudge(window.TrackKey, id == 3 ? 0.1 : -0.1);
                return;
            }
            if (switching) return;
            switching = true;
            try
            {
                var switches = CaseSwitches.Load();
                if (id == 1) await switches.SetLightsOffAsync(!switches.LightsOff, settings.LightsExcluded);
                else await switches.SetScreensOffAsync(!switches.ScreensOff, settings.ScreensExcluded);
            }
            catch (Exception error) when (error is HttpRequestException || error is TaskCanceledException || error is InvalidOperationException || error is IOException)
            {
                Log.Write("shortcut: " + error.Message);
            }
            switching = false;
        }
    }
}
