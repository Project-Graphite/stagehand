using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Stagehand
{
    sealed class CaseLighting
    {
        static readonly string SnapshotPath = Path.Combine(Log.DataDir, "lighting-before-stagehand.json");
        static readonly Color HeatBright = Color.FromRgb(255, 0, 0), HeatDim = Color.FromRgb(60, 0, 0);
        readonly Settings settings;
        readonly Func<CaseSwitches> switches;
        Color? shown;
        bool game, heat, breath, stopping;
        Task pump = Task.CompletedTask;

        public CaseLighting(Settings settings, Func<CaseSwitches> switches)
        {
            this.settings = settings;
            this.switches = switches;
        }

        public static string Snapshot() => File.Exists(SnapshotPath) ? File.ReadAllText(SnapshotPath, Encoding.UTF8) : null;

        public static void ForgetSnapshot() => File.Delete(SnapshotPath);

        public void SetGame(bool on)
        {
            game = on;
            Refresh();
        }

        public void SetHeat(bool on)
        {
            heat = on;
            Refresh();
        }

        public Task Refresh()
        {
            if (pump.IsCompleted) pump = Pump();
            return pump;
        }

        public Task StopAsync()
        {
            stopping = true;
            return Refresh();
        }

        Color? Target()
        {
            if (stopping) return null;
            if (heat) return HeatBright;
            return game && settings.GameLighting ? (Color)ColorConverter.ConvertFromString(settings.GameColor) : (Color?)null;
        }

        async Task Pump()
        {
            try
            {
                while (true)
                {
                    if (switches().LightsOff)
                    {
                        shown = null;
                        return;
                    }
                    var target = Target();
                    if (target == null)
                    {
                        var before = Snapshot();
                        if (before == null) return;
                        await LConnect.Post(LConnect.SetLighting, before);
                        ForgetSnapshot();
                        shown = null;
                        continue;
                    }
                    if (Snapshot() == null)
                    {
                        Directory.CreateDirectory(Log.DataDir);
                        File.WriteAllText(SnapshotPath, LConnect.SavedLighting(), Encoding.UTF8);
                    }
                    if (heat)
                    {
                        breath = !breath;
                        await Show(breath ? HeatBright : HeatDim);
                        shown = null;
                        await Task.Delay(700);
                        continue;
                    }
                    if (shown == target) return;
                    await Show(target.Value);
                    shown = target;
                }
            }
            catch (Exception error) when (error is HttpRequestException || error is TaskCanceledException || error is IOException || error is InvalidOperationException)
            {
                Log.Write("case lighting: " + error.Message);
            }
        }

        static Task Show(Color colour) => LConnect.Post(LConnect.SetLighting, LConnect.Solid(Snapshot(), colour, new HashSet<string>()));
    }
}
