using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Stagehand
{
    sealed class Gaming
    {
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        const int FullScreenWindow = 2, ExclusiveFullScreen = 3;
        const double HeatEase = 5;
        readonly Settings settings;
        readonly LyricsWindow window;
        readonly CaseLighting lighting;
        readonly HardwareMonitor hardware = new HardwareMonitor();
        DateTime gameSeenAt = DateTime.MinValue, gameMissingAt = DateTime.MinValue;
        bool playing, hot;

        public Gaming(Settings settings, LyricsWindow window, CaseLighting lighting)
        {
            this.settings = settings;
            this.window = window;
            this.lighting = lighting;
            var tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            tick.Tick += (s, e) => Tick();
            tick.Start();
        }

        void Tick()
        {
            var reading = settings.GameMode || settings.HeatAlert ? hardware.Read(playing) : new HardwareReading();
            var inGame = settings.GameMode && LooksLikeGame(reading);
            var now = DateTime.UtcNow;
            if (inGame) gameMissingAt = now; else gameSeenAt = now;
            var nextPlaying = playing ? (now - gameMissingAt).TotalSeconds < 5 : (now - gameSeenAt).TotalSeconds >= 3;
            if (nextPlaying != playing)
            {
                playing = nextPlaying;
                window.SetGame(playing);
                lighting.SetGame(playing);
            }
            if (playing) window.SetHardware(reading);

            var temperature = reading.GpuTemperature;
            var nextHot = settings.HeatAlert && temperature.HasValue && (hot ? temperature > settings.HeatLimit - HeatEase : temperature >= settings.HeatLimit);
            if (nextHot == hot) return;
            hot = nextHot;
            Log.Write(hot ? "heat alert: GPU at " + temperature + " °C" : "heat alert cleared");
            lighting.SetHeat(hot);
        }

        bool LooksLikeGame(HardwareReading reading)
        {
            if (SHQueryUserNotificationState(out var state) != 0) return false;
            if (state == ExclusiveFullScreen) return true;
            if (state != FullScreenWindow) return false;
            GetWindowThreadProcessId(GetForegroundWindow(), out var processId);
            string name;
            try
            {
                name = Process.GetProcessById((int)processId).ProcessName;
            }
            catch (ArgumentException)
            {
                return false;
            }
            if (string.Equals(name, AppVolume.ProcessName(window.Player.AppId), StringComparison.OrdinalIgnoreCase)) return false;
            return !hardware.HasGpu || reading.GpuLoad >= 30;
        }
    }
}
