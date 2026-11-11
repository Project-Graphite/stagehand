using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Stagehand
{
    static class Program
    {
        const string AgentMutexName = "Stagehand.Agent";
        const string AppMutexName = "Stagehand.App";
        const string QuitEventName = "Stagehand.Quit";
        const string ShowSettingsEventName = "Stagehand.ShowSettings";

        [STAThread]
        static void Main(string[] args)
        {
            var renderIndex = Array.IndexOf(args, "--render-window");
            if (renderIndex >= 0) RenderWindow(args[renderIndex + 1] == "setup" ? (Window)new SetupWindow() : new SettingsWindow(Settings.Load()), args[renderIndex + 2]);
            else if (args.Contains("--install")) Installer.Install(args.Contains("--startup") || StartupShortcut.Exists);
            else if (args.Contains("--uninstall")) RunUninstall(args.Contains("--quiet"));
            else if (args.Contains("--agent") || args.Contains("--preview") || args.Contains("--snapshot")) RunAgent(args);
            else if (Installer.IsSetup(ExePath)) new Application().Run(new SetupWindow());
            else RunApp();
        }

        public static string ExePath => Process.GetCurrentProcess().MainModule.FileName;

        public static bool AgentRunning
        {
            get
            {
                if (!Mutex.TryOpenExisting(AgentMutexName, out var existing)) return false;
                existing.Dispose();
                return true;
            }
        }

        static void RenderWindow(Window window, string path)
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -6000;
            window.Top = -6000;
            window.ShowActivated = false;
            window.Show();
            var delay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            delay.Tick += (s, e) =>
            {
                delay.Stop();
                var content = (FrameworkElement)window.Content;
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using (var file = File.Create(path)) encoder.Save(file);
                app.Shutdown();
            };
            delay.Start();
            app.Run();
        }

        static void RunUninstall(bool quiet)
        {
            if (!quiet && MessageBox.Show("Uninstall Stagehand?\n\nYour settings and cached lyrics stay in " + Log.DataDir + ".", "Stagehand", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            Installer.Uninstall();
        }

        public static void StartAgent()
        {
            if (!Mutex.TryOpenExisting(AgentMutexName, out var existing)) Process.Start(new ProcessStartInfo(ExePath, "--agent") { UseShellExecute = false });
            else existing.Dispose();
        }

        public static void StopAgent()
        {
            if (EventWaitHandle.TryOpenExisting(QuitEventName, out var quit)) using (quit) quit.Set();
        }

        static void RunApp()
        {
            using (var single = new Mutex(true, AppMutexName, out var first))
            {
                if (!first)
                {
                    if (EventWaitHandle.TryOpenExisting(ShowSettingsEventName, out var show)) using (show) show.Set();
                    return;
                }
                StartAgent();
                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                app.DispatcherUnhandledException += (s, e) => Log.Write("app unhandled: " + e.Exception);
                var settings = Settings.Load();
                var window = new SettingsWindow(settings);
                var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventName);
                WatchSignal(showSignal, () => app.Dispatcher.BeginInvoke(new Action(() => { window.Activate(); window.WindowState = WindowState.Normal; })));
                app.Run(window);
            }
        }

        static void RunAgent(string[] args)
        {
            using (var single = new Mutex(true, AgentMutexName, out var first))
            {
                if (!first) return;
                var snapshotIndex = Array.IndexOf(args, "--snapshot");
                var preview = args.Contains("--preview") || snapshotIndex >= 0;
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.DispatcherUnhandledException += (s, e) => { Log.Write("agent unhandled: " + e.Exception); e.Handled = true; };
                var settings = Settings.Load();
                settings.WatchFile(app.Dispatcher);
                var window = new LyricsWindow(settings, preview);

                var demoIndex = Array.IndexOf(args, "--demo");
                if (demoIndex >= 0)
                {
                    window.Loaded += (s, e) => window.StartDemo(new TrackInfo
                    {
                        Title = args[demoIndex + 1],
                        Artist = args[demoIndex + 2],
                        Album = "Demo album",
                        TrackNumber = 4,
                        TrackCount = 12,
                        Source = "Demo",
                        DurationSeconds = double.Parse(args[demoIndex + 3], CultureInfo.InvariantCulture),
                        Artwork = File.ReadAllBytes(args[demoIndex + 4])
                    }, double.Parse(args[demoIndex + 5], CultureInfo.InvariantCulture));
                }

                if (snapshotIndex >= 0)
                {
                    window.Left = -6000;
                    window.Top = -6000;
                    window.Show();
                    var delay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(double.Parse(args[snapshotIndex + 2], CultureInfo.InvariantCulture)) };
                    delay.Tick += (s, e) => { delay.Stop(); window.SaveSnapshot(args[snapshotIndex + 1]); app.Shutdown(); };
                    delay.Start();
                    app.Run();
                    return;
                }

                if (preview)
                {
                    window.Left = 0;
                    window.Top = 0;
                    window.Show();
                }
                else
                {
                    Action place = () =>
                    {
                        var screen = ScreenPlacement.FindLyricsScreen();
                        if (screen == null)
                        {
                            window.Hide();
                            return;
                        }
                        if (!window.IsVisible) window.Show();
                        ScreenPlacement.PlaceOn(new WindowInteropHelper(window).Handle, screen.Bounds);
                    };
                    SystemEvents.DisplaySettingsChanged += (s, e) => window.Dispatcher.BeginInvoke(place);
                    window.SourceInitialized += (s, e) => place();
                    var screenNow = ScreenPlacement.FindLyricsScreen();
                    if (screenNow != null)
                    {
                        window.Left = screenNow.Bounds.X;
                        window.Top = screenNow.Bounds.Y;
                        window.Show();
                    }
                    else new WindowInteropHelper(window).EnsureHandle();
                    ScreenPlacement.StartScreenGuard();
                }

                if (!preview)
                {
                    var switches = CaseSwitches.Load();
                    window.SetBlackout(switches.ScreensOff);
                    Settings.WatchDataFile(CaseSwitches.FileName, app.Dispatcher, () =>
                    {
                        switches = CaseSwitches.Load();
                        window.SetBlackout(switches.ScreensOff);
                    });
                    var keepScreensOff = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
                    keepScreensOff.Tick += async (s, e) =>
                    {
                        if (!switches.ScreensOff) return;
                        try
                        {
                            await CaseSwitches.ApplyScreensOffAsync();
                        }
                        catch (Exception error) when (error is System.Net.Http.HttpRequestException || error is System.Threading.Tasks.TaskCanceledException || error is InvalidOperationException)
                        {
                            Log.Write("screens off: " + error.Message);
                        }
                    };
                    keepScreensOff.Start();
                }

                var quit = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName);
                WatchSignal(quit, () => app.Dispatcher.BeginInvoke(new Action(() => window.FadeOutAndExit(app))));

                using (var tray = new System.Windows.Forms.NotifyIcon
                {
                    Icon = System.Drawing.Icon.ExtractAssociatedIcon(ExePath),
                    Text = "Stagehand",
                    Visible = settings.ShowTrayIcon,
                    ContextMenu = new System.Windows.Forms.ContextMenu(new[]
                    {
                        new System.Windows.Forms.MenuItem("Open Stagehand", (s, e) => Process.Start(new ProcessStartInfo(ExePath) { UseShellExecute = false })),
                        new System.Windows.Forms.MenuItem("-"),
                        new System.Windows.Forms.MenuItem("Quit", (s, e) => window.FadeOutAndExit(app))
                    })
                })
                {
                    settings.Changed += () => tray.Visible = settings.ShowTrayIcon;
                    tray.DoubleClick += (s, e) => Process.Start(new ProcessStartInfo(ExePath) { UseShellExecute = false });
                    app.Run();
                }
            }
        }

        static void WatchSignal(EventWaitHandle signal, Action onSignal)
        {
            new Thread(() => { while (true) { signal.WaitOne(); onSignal(); } }) { IsBackground = true, Name = "signal" }.Start();
        }
    }
}
