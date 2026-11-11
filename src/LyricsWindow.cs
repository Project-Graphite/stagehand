using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Stagehand
{
    sealed class WordView
    {
        public LyricWord Word;
        public TextBlock Block;
        public GradientStop LitStart;
        public GradientStop LitEdge;
        public GradientStop DimEdge;
        public TranslateTransform Lift;
    }

    sealed class LineView
    {
        public LyricLine Line;
        public FrameworkElement Element;
        public List<WordView> Words = new List<WordView>();
        public Bubble Bubble;
        public BlurEffect Blur;
        public ScaleTransform Scale;
        public double Opacity;
        public double BlurRadius;
        public double Top;
        public double Height;
    }

    sealed class Bubble
    {
        public Border Pill;
        public ScaleTransform Scale;
        public List<Ellipse> Dots = new List<Ellipse>();

        public Bubble(double dotSize)
        {
            Scale = new ScaleTransform(1, 1);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Effect = new DropShadowEffect { Color = Graphite.Ink, BlurRadius = dotSize * 0.9, ShadowDepth = 0, Opacity = 0.55, RenderingBias = RenderingBias.Performance } };
            for (int i = 0; i < 3; i++)
            {
                var dot = new Ellipse { Width = dotSize, Height = dotSize, Fill = Graphite.Brush(Graphite.Ink), Margin = new Thickness(i == 0 ? 0 : dotSize * 0.55, 0, 0, 0), RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 1) };
                row.Children.Add(dot);
                Dots.Add(dot);
            }
            Pill = new Border
            {
                Child = row,
                CornerRadius = new CornerRadius(dotSize * 1.6),
                Padding = new Thickness(dotSize * 1.25, dotSize * 0.8, dotSize * 1.25, dotSize * 0.8),
                Background = Graphite.Glass(0x1F, 0x08),
                BorderBrush = Graphite.Brush(Graphite.Ink, 0x30),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                RenderTransformOrigin = new Point(0.15, 0.5),
                RenderTransform = Scale
            };
        }

        public void Animate(double progress, double now)
        {
            var pop = progress > 0.92 ? (progress - 0.92) / 0.08 : 0;
            var breathe = 1 + 0.035 * Math.Sin(now * 2.6);
            Scale.ScaleX = Scale.ScaleY = breathe * (1 + 0.14 * Graphite.Settle(pop));
            Pill.Opacity = 1 - Graphite.Settle(pop);
            for (int i = 0; i < Dots.Count; i++)
            {
                var lit = Math.Max(0, Math.Min(1, progress * 3.2 - i));
                Dots[i].Opacity = 0.22 + 0.78 * lit;
                var dotScale = (ScaleTransform)Dots[i].RenderTransform;
                dotScale.ScaleX = dotScale.ScaleY = 0.78 + 0.3 * lit + 0.06 * Math.Sin(now * 3.4 + i * 0.7) * lit;
            }
        }
    }

    sealed class CoverLayer
    {
        public Canvas Canvas;
        public List<Tuple<RotateTransform, ScaleTransform, double, double>> Blobs = new List<Tuple<RotateTransform, ScaleTransform, double, double>>();
    }

    enum LyricsView { Loading, Lines, Instrumental, NotFound }

    sealed class LyricsWindow : Window
    {
        const double BaseWidth = 1920, BaseHeight = 464;
        const double ArtLeft = 48;
        const double WordGap = 14;
        const double StatsWidth = 384, StatsInner = StatsWidth - 56;
        static readonly Color Dim = Color.FromArgb(0x8C, Graphite.Ink.R, Graphite.Ink.G, Graphite.Ink.B);
        static readonly Color Soft = Color.FromArgb(0xB4, Graphite.Ink.R, Graphite.Ink.G, Graphite.Ink.B);

        readonly Settings settings;
        readonly NowPlayingWatcher watcher = new NowPlayingWatcher();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly IdleBackground idle = new IdleBackground();
        readonly Grid coverHost = new Grid();
        readonly Grid content = new Grid { Opacity = 0 };
        readonly Grid root = new Grid { ClipToBounds = true };
        readonly ScaleTransform contentScale = new ScaleTransform(0.97, 0.97);
        readonly BlurEffect contentBlur = new BlurEffect { Radius = 16, RenderingBias = RenderingBias.Performance };
        readonly Image artImage = new Image { Stretch = Stretch.UniformToFill };
        readonly Border artFrame = new Border { CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), BorderBrush = Graphite.Brush(Graphite.Ink, 0x22) };
        readonly Border artShadow = new Border { CornerRadius = new CornerRadius(18), Background = Brushes.Black, Effect = new DropShadowEffect { BlurRadius = 48, ShadowDepth = 10, Opacity = 0.55, RenderingBias = RenderingBias.Performance } };
        readonly TextBlock titleText = Text(28, Graphite.Ink, FontWeights.Bold);
        readonly TextBlock artistText = Text(21, Graphite.Muted, FontWeights.Normal);
        readonly Canvas lyricsViewport = new Canvas { ClipToBounds = true };
        readonly StackPanel lyricsStack = new StackPanel();
        readonly TranslateTransform scroll = new TranslateTransform();
        readonly StackPanel emptyState = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock emptyTitle = Text(40, Graphite.Ink, FontWeights.SemiBold);
        readonly TextBlock emptyDetail = Text(22, Graphite.Muted, FontWeights.Normal);
        readonly Bubble emptyBubble = new Bubble(16);
        readonly Border statsPanel = new Border();
        readonly TextBlock sourceText = Text(16, Graphite.Ink, FontWeights.SemiBold);
        readonly TextBlock elapsedText = Text(46, Graphite.Ink, FontWeights.Normal);
        readonly TextBlock remainingText = Text(22, Graphite.Muted, FontWeights.Normal);
        readonly Border progressFill = new Border { Height = 7, CornerRadius = new CornerRadius(3.5), HorizontalAlignment = HorizontalAlignment.Left, Background = Graphite.Brush(Graphite.Ink) };
        readonly TextBlock albumValue = Text(19, Graphite.Ink, FontWeights.Normal);
        readonly TextBlock trackValue = Text(19, Graphite.Ink, FontWeights.Normal);
        readonly TextBlock lyricsValue = Text(19, Graphite.Ink, FontWeights.Normal);
        readonly Ellipse lyricsDot = new Ellipse { Width = 10, Height = 10, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        readonly List<CoverLayer> coverFading = new List<CoverLayer>();
        readonly Canvas musicLayer = new Canvas();
        readonly Canvas videoLayer = new Canvas { Opacity = 0 };
        readonly Image videoThumb = new Image { Width = 604, Height = 340, Stretch = Stretch.UniformToFill };
        readonly Border videoThumbBack = new Border { Width = 604, Height = 340, CornerRadius = new CornerRadius(16), Background = Graphite.Brush(Graphite.Surface, 0xB8) };
        readonly Border videoThumbFrame = new Border { Width = 604, Height = 340, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), BorderBrush = Graphite.Brush(Graphite.Ink, 0x22) };
        readonly StackPanel videoDetails = new StackPanel();
        readonly TextBlock videoSource = Text(15, Graphite.Muted, FontWeights.SemiBold);
        readonly TextBlock videoTitle = Text(52, Graphite.Ink, FontWeights.SemiBold);
        readonly TextBlock videoChannel = Text(26, Graphite.Muted, FontWeights.Normal);
        readonly TextBlock videoElapsed = Text(30, Graphite.Ink, FontWeights.Normal);
        readonly TextBlock videoRemaining = Text(20, Graphite.Muted, FontWeights.Normal);
        readonly Border videoProgress = new Border { Height = 8, CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left, Background = Graphite.Brush(Graphite.Ink) };
        double videoMix;
        static readonly Geometry PlayShape = Geometry.Parse("M4,2 L18,10 L4,18 Z");
        static readonly Geometry PauseShape = Geometry.Parse("M4,2 H8 V18 H4 Z M12,2 H16 V18 H12 Z");
        const double SeekWidth = 420, VolumeWidth = 120;
        readonly Border controls = new Border
        {
            CornerRadius = new CornerRadius(28), Padding = new Thickness(12, 6, 22, 6), Margin = new Thickness(0, 0, 0, 18),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Opacity = 0, Visibility = Visibility.Hidden,
            Background = Graphite.Brush(Graphite.Surface, 0xE0), BorderBrush = Graphite.Brush(Graphite.Ink, 0x30), BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 0, Opacity = 0.5, RenderingBias = RenderingBias.Performance }
        };
        readonly System.Windows.Shapes.Path playGlyph = Glyph(PauseShape, 18);
        readonly Border seekFill = new Border();
        readonly Border volumeFill = new Border();
        readonly StackPanel volumeGroup = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(18, 0, 0, 0) };
        readonly TextBlock controlsElapsed = Text(15, Graphite.Ink, FontWeights.Normal);
        readonly TextBlock controlsRemaining = Text(15, Graphite.Muted, FontWeights.Normal);
        double controlsMix;
        double? scrubbing;
        DateTime volumeReadAt = DateTime.MinValue;
        double designWidth = BaseWidth, designHeight = BaseHeight;
        double lyricsWidth;
        Color highlight = Graphite.Ink;
        Color paintedHighlight = Graphite.Ink;
        Color coverAccent = Graphite.Ink;

        List<LineView> lines = new List<LineView>();
        LyricsView view = LyricsView.Loading;
        LyricsKind lyricsKind = LyricsKind.None;
        List<LyricLine> sourceLines = new List<LyricLine>();
        bool japaneseSong;
        CoverLayer cover;
        TrackInfo track;
        int lyricsRequest;
        bool playing;
        DateTime lastPlayingAt = DateTime.MinValue;
        double show, showFrom, showTo, showStart, showDuration = 1;
        double lyricsFade = 1;
        double lastFrame;
        bool layoutDirty, snapLines, blackout;
        Dictionary<string, double> nudges = SyncNudges.Load();
        string loadedBackground, loadedAnchor;
        int lastElapsedSecond = -1;

        public LyricsWindow(Settings settings, bool preview)
        {
            this.settings = settings;
            Title = "Stagehand";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = preview;
            Topmost = !preview;
            ShowActivated = false;
            Background = Brushes.Black;
            Width = BaseWidth;
            Height = BaseHeight;

            root.Children.Add(idle);
            root.Children.Add(coverHost);
            root.Children.Add(content);
            Content = new Viewbox { Stretch = Stretch.Fill, Child = root };
            content.RenderTransform = contentScale;
            BuildContent();
            ApplySettings();
            SizeChanged += (s, e) => { Fit(e.NewSize.Width, e.NewSize.Height); ApplySettings(); };
            settings.Changed += ApplySettings;
            Settings.WatchDataFile(SyncNudges.FileName, Dispatcher, () => nudges = SyncNudges.Load());

            watcher.TrackChanged += OnTrackChanged;
            Loaded += async (s, e) =>
            {
                idle.SetPlaying(true);
                await watcher.StartAsync();
                var poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                poll.Tick += (a, b) => PollPlayback();
                poll.Start();
                CompositionTarget.Rendering += OnFrame;
            };
            SourceInitialized += (s, e) => ScreenPlacement.ApplyWindowStyles(new WindowInteropHelper(this).Handle, preview);
        }

        public void StartDemo(TrackInfo demoTrack, double positionSeconds) => watcher.StartDemo(demoTrack, positionSeconds);

        public void SetBlackout(bool value) => blackout = value;

        public bool ForceControls;

        public void FadeOutAndExit(Application app)
        {
            var fade = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(450))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            fade.Completed += (s, e) => app.Shutdown();
            ((UIElement)Content).BeginAnimation(OpacityProperty, fade);
        }

        public void SaveSnapshot(string path)
        {
            var bitmap = new RenderTargetBitmap((int)designWidth, (int)designHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(path)) encoder.Save(file);
        }

        static TextBlock Text(double size, Color color, FontWeight weight) => new TextBlock
        {
            FontFamily = Graphite.Sans, FontSize = size, FontWeight = weight, Foreground = Graphite.Brush(color), TextTrimming = TextTrimming.CharacterEllipsis
        };

        void Fit(double width, double height)
        {
            if (width <= 0 || height <= 0) return;
            var scale = Math.Min(width / BaseWidth, height / BaseHeight);
            designWidth = Math.Round(width / scale);
            designHeight = Math.Round(height / scale);
        }

        void Relayout()
        {
            root.Width = designWidth;
            root.Height = designHeight;
            contentScale.CenterX = designWidth / 2;
            contentScale.CenterY = designHeight / 2;

            var showArt = settings.ShowCover && designWidth >= 1100;
            var showStats = settings.ShowStats && designWidth >= 1450;
            var artSize = Math.Max(220, Math.Min(560, designHeight - 112));
            var artTop = (designHeight - artSize - 80) / 2;
            foreach (var element in new FrameworkElement[] { artShadow, artImage, artFrame })
            {
                element.Width = element.Height = artSize;
                element.Visibility = showArt ? Visibility.Visible : Visibility.Collapsed;
                Canvas.SetLeft(element, ArtLeft);
                Canvas.SetTop(element, artTop);
            }
            artImage.Clip = new RectangleGeometry(new Rect(0, 0, artSize, artSize), 18, 18);
            titleText.Width = artistText.Width = artSize;
            titleText.Visibility = artistText.Visibility = showArt ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(titleText, ArtLeft);
            Canvas.SetTop(titleText, artTop + artSize + 12);
            Canvas.SetLeft(artistText, ArtLeft);
            Canvas.SetTop(artistText, artTop + artSize + 50);

            var lyricsLeft = showArt ? ArtLeft + artSize + 52 : 72;
            var statsScale = settings.StatsSize == "S" ? 0.8 : settings.StatsSize == "L" ? 1.2 : settings.StatsSize == "XL" ? 1.4 : 1;
            statsPanel.LayoutTransform = new ScaleTransform(statsScale, statsScale);
            var statsLeft = designWidth - 40 - StatsWidth * statsScale;
            lyricsWidth = (showStats ? statsLeft - 40 : designWidth - 72) - lyricsLeft;
            statsPanel.Visibility = showStats ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(statsPanel, statsLeft);
            Canvas.SetLeft(lyricsViewport, lyricsLeft);
            lyricsViewport.Width = lyricsStack.Width = emptyState.Width = lyricsWidth;
            lyricsViewport.Height = designHeight;
            ((LinearGradientBrush)lyricsViewport.OpacityMask).EndPoint = new Point(0, designHeight);

            var showThumb = designWidth >= 1300;
            foreach (var element in new FrameworkElement[] { videoThumbBack, videoThumb, videoThumbFrame })
            {
                element.Visibility = showThumb ? Visibility.Visible : Visibility.Collapsed;
                Canvas.SetLeft(element, 64);
                Canvas.SetTop(element, (designHeight - 340) / 2);
            }
            Canvas.SetLeft(videoDetails, showThumb ? 720 : 72);
            videoDetails.Width = designWidth - (showThumb ? 720 : 72) - 80;
            layoutDirty = true;
        }

        void BuildContent()
        {
            content.Children.Add(new Rectangle
            {
                Fill = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(185, Graphite.Paper.R, Graphite.Paper.G, Graphite.Paper.B), 0),
                    new GradientStop(Color.FromArgb(120, Graphite.Paper.R, Graphite.Paper.G, Graphite.Paper.B), 0.32),
                    new GradientStop(Color.FromArgb(120, Graphite.Paper.R, Graphite.Paper.G, Graphite.Paper.B), 0.75),
                    new GradientStop(Color.FromArgb(175, Graphite.Paper.R, Graphite.Paper.G, Graphite.Paper.B), 1)
                }, new Point(0, 0), new Point(1, 0))
            });

            content.Children.Add(musicLayer);

            RenderOptions.SetBitmapScalingMode(artImage, BitmapScalingMode.HighQuality);
            musicLayer.Children.Add(artShadow);
            musicLayer.Children.Add(artImage);
            musicLayer.Children.Add(artFrame);
            musicLayer.Children.Add(titleText);
            musicLayer.Children.Add(artistText);

            lyricsViewport.OpacityMask = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(Colors.Transparent, 0),
                new GradientStop(Colors.Black, 0.18),
                new GradientStop(Colors.Black, 0.82),
                new GradientStop(Colors.Transparent, 1)
            }, new Point(0, 0), new Point(0, BaseHeight)) { MappingMode = BrushMappingMode.Absolute };
            lyricsStack.RenderTransform = scroll;
            lyricsViewport.Children.Add(lyricsStack);
            emptyState.Children.Add(emptyBubble.Pill);
            emptyBubble.Pill.Margin = new Thickness(0, 0, 0, 22);
            emptyState.Children.Add(emptyTitle);
            emptyState.Children.Add(emptyDetail);
            emptyDetail.Margin = new Thickness(0, 8, 0, 0);
            lyricsViewport.Children.Add(emptyState);
            musicLayer.Children.Add(lyricsViewport);

            BuildStats();
            musicLayer.Children.Add(statsPanel);

            content.Children.Add(videoLayer);
            BuildVideo();
            BuildControls();
            content.Children.Add(controls);
        }

        void BuildControls()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(ControlButton(Glyph(Geometry.Parse("M3,2 H6 V18 H3 Z M18,2 V18 L7,10 Z"), 16), () => watcher.Previous()));
            row.Children.Add(ControlButton(playGlyph, () => watcher.TogglePlayPause()));
            row.Children.Add(ControlButton(Glyph(Geometry.Parse("M2,2 L13,10 L2,18 Z M14,2 H17 V18 H14 Z"), 16), () => watcher.Next()));
            controlsElapsed.FontFamily = controlsRemaining.FontFamily = Graphite.Mono;
            controlsElapsed.Width = controlsRemaining.Width = 64;
            controlsElapsed.TextAlignment = TextAlignment.Right;
            controlsElapsed.Margin = new Thickness(10, 0, 12, 0);
            controlsRemaining.Margin = new Thickness(12, 0, 0, 0);
            controlsElapsed.VerticalAlignment = controlsRemaining.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(controlsElapsed);
            row.Children.Add(Bar(SeekWidth, seekFill, fraction => scrubbing = fraction, fraction =>
            {
                scrubbing = null;
                if (track != null && track.DurationSeconds > 0) watcher.Seek(fraction * track.DurationSeconds);
            }));
            row.Children.Add(controlsRemaining);
            var speaker = Glyph(Geometry.Parse("M2,7 H6 L11,3 V17 L6,13 H2 Z"), 16);
            speaker.Margin = new Thickness(0, 0, 10, 0);
            volumeGroup.Children.Add(speaker);
            volumeGroup.Children.Add(Bar(VolumeWidth, volumeFill, fraction =>
            {
                volumeFill.Width = VolumeWidth * fraction;
                AppVolume.Set(watcher.AppId, fraction);
            }, fraction => { }));
            row.Children.Add(volumeGroup);
            controls.Child = row;
        }

        static System.Windows.Shapes.Path Glyph(Geometry shape, double size) => new System.Windows.Shapes.Path
        {
            Data = shape, Fill = Graphite.Brush(Graphite.Ink), Stretch = Stretch.Uniform, Width = size, Height = size,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };

        static FrameworkElement ControlButton(System.Windows.Shapes.Path glyph, Action click)
        {
            var button = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(22), Background = Brushes.Transparent, Child = glyph, Cursor = Cursors.Hand };
            button.MouseEnter += (s, e) => button.Background = Graphite.Brush(Graphite.Ink, 0x22);
            button.MouseLeave += (s, e) => button.Background = Brushes.Transparent;
            button.MouseLeftButtonUp += (s, e) => click();
            return button;
        }

        static FrameworkElement Bar(double width, Border fill, Action<double> moving, Action<double> released)
        {
            var bar = new Grid { Width = width, Height = 24, Background = Brushes.Transparent, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            bar.Children.Add(new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = Graphite.Brush(Graphite.Ink, 0x30) });
            fill.Height = 6;
            fill.CornerRadius = new CornerRadius(3);
            fill.HorizontalAlignment = HorizontalAlignment.Left;
            fill.Background = Graphite.Brush(Graphite.Ink);
            bar.Children.Add(fill);
            double At(MouseEventArgs e) => Clamp(e.GetPosition(bar).X / width);
            bar.MouseLeftButtonDown += (s, e) => { bar.CaptureMouse(); moving(At(e)); };
            bar.MouseMove += (s, e) => { if (bar.IsMouseCaptured) moving(At(e)); };
            bar.MouseLeftButtonUp += (s, e) =>
            {
                if (!bar.IsMouseCaptured) return;
                bar.ReleaseMouseCapture();
                released(At(e));
            };
            return bar;
        }

        void BuildVideo()
        {
            videoThumb.Clip = new RectangleGeometry(new Rect(0, 0, 604, 340), 16, 16);
            RenderOptions.SetBitmapScalingMode(videoThumb, BitmapScalingMode.HighQuality);
            videoLayer.Children.Add(videoThumbBack);
            videoLayer.Children.Add(videoThumb);
            videoLayer.Children.Add(videoThumbFrame);

            videoTitle.TextWrapping = TextWrapping.Wrap;
            videoTitle.MaxHeight = 142;
            videoTitle.Margin = new Thickness(0, 12, 0, 0);
            videoChannel.Margin = new Thickness(0, 10, 0, 0);
            videoDetails.Children.Add(videoSource);
            videoDetails.Children.Add(videoTitle);
            videoDetails.Children.Add(videoChannel);
            AddProgress(videoDetails, videoElapsed, videoRemaining, videoProgress, new Thickness(0, 34, 0, 0), new Thickness(0, 12, 0, 0));
            videoLayer.Children.Add(videoDetails);
        }

        static void AddProgress(StackPanel stack, TextBlock elapsed, TextBlock remaining, Border fill, Thickness timesMargin, Thickness barMargin)
        {
            var times = new DockPanel { LastChildFill = false, Margin = timesMargin };
            elapsed.FontFamily = remaining.FontFamily = Graphite.Mono;
            remaining.VerticalAlignment = VerticalAlignment.Bottom;
            remaining.Margin = new Thickness(0, 0, 0, 5);
            DockPanel.SetDock(elapsed, Dock.Left);
            DockPanel.SetDock(remaining, Dock.Right);
            times.Children.Add(elapsed);
            times.Children.Add(remaining);
            stack.Children.Add(times);
            var bar = new Grid { Height = fill.Height, Margin = barMargin };
            bar.Children.Add(new Border { CornerRadius = fill.CornerRadius, Background = Graphite.Brush(Graphite.Ink, 0x26) });
            bar.Children.Add(fill);
            stack.Children.Add(bar);
        }

        void BuildStats()
        {
            var stack = new StackPanel { Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.8, RenderingBias = RenderingBias.Performance } };
            var header = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 20) };
            var label = Text(15, Soft, FontWeights.SemiBold);
            label.Text = "NOW PLAYING";
            DockPanel.SetDock(label, Dock.Left);
            DockPanel.SetDock(sourceText, Dock.Right);
            header.Children.Add(label);
            header.Children.Add(sourceText);
            stack.Children.Add(header);

            AddProgress(stack, elapsedText, remainingText, progressFill, new Thickness(0), new Thickness(0, 12, 0, 20));

            stack.Children.Add(StatRow("ALBUM", albumValue, null));
            stack.Children.Add(StatRow("TRACK", trackValue, null));
            stack.Children.Add(StatRow("LYRICS", lyricsValue, lyricsDot));

            statsPanel.Child = stack;
            statsPanel.Width = StatsWidth;
            statsPanel.Padding = new Thickness(28, 24, 28, 12);
            statsPanel.CornerRadius = new CornerRadius(18);
            statsPanel.Background = Graphite.Glass(0x1C, 0x06);
            statsPanel.BorderBrush = Graphite.Brush(Graphite.Ink, 0x2A);
            statsPanel.BorderThickness = new Thickness(1);
        }

        static FrameworkElement StatRow(string name, TextBlock value, Ellipse dot)
        {
            var row = new DockPanel { LastChildFill = false };
            var label = Text(15, Soft, FontWeights.SemiBold);
            label.Text = name;
            label.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(label, Dock.Left);
            row.Children.Add(label);
            var right = new StackPanel { Orientation = Orientation.Horizontal };
            if (dot != null) right.Children.Add(dot);
            value.MaxWidth = StatsInner - 100;
            right.Children.Add(value);
            DockPanel.SetDock(right, Dock.Right);
            row.Children.Add(right);
            return new Border { Child = row, BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Graphite.Brush(Graphite.Ink, 0x1F), Padding = new Thickness(0, 13, 0, 13) };
        }

        void ApplySettings()
        {
            if (settings.IdleBackgroundPath != loadedBackground || settings.IdleAnchor != loadedAnchor)
            {
                loadedBackground = settings.IdleBackgroundPath;
                loadedAnchor = settings.IdleAnchor;
                idle.Load(loadedBackground, loadedAnchor);
                idle.SetPlaying(show < 0.999);
            }
            highlight = settings.HighlightColor == "Cover" ? coverAccent : Graphite.Ink;
            coverHost.Visibility = settings.BackgroundStyle == "Cover" ? Visibility.Visible : Visibility.Hidden;
            var font = LyricFont;
            foreach (var text in new[] { titleText, artistText, emptyTitle, emptyDetail, videoTitle, videoChannel }) text.FontFamily = font;
            Relayout();
            if (sourceLines.Count > 0) BuildLines(sourceLines);
            ScreenPlacement.CursorGuardEnabled = settings.KeepMouseOff;
            ScreenPlacement.PreferredDisplay = settings.Display;
        }

        FontFamily LyricFont => string.IsNullOrWhiteSpace(settings.LyricFont) ? Graphite.Sans : new FontFamily(settings.LyricFont + ", " + Graphite.Sans.Source);

        double LyricScale => settings.LyricSize == "S" ? 0.8 : settings.LyricSize == "L" ? 1.2 : settings.LyricSize == "XL" ? 1.4 : 1;

        void PollPlayback()
        {
            try
            {
                var snapshot = watcher.Poll();
                playing = snapshot.IsPlaying && snapshot.Track != null;
                if (playing) lastPlayingAt = DateTime.UtcNow;
                if (snapshot.Track == null && track != null) track = null;
            }
            catch (Exception error)
            {
                Log.Write("poll: " + error.Message);
            }
        }

        async void OnTrackChanged(TrackInfo next)
        {
            var sameSong = track != null && track.Key == next.Key;
            track = next;
            titleText.Text = next.Title;
            artistText.Text = next.Artist;
            sourceText.Text = next.Source;
            albumValue.Text = string.IsNullOrWhiteSpace(next.Album) ? "—" : next.Album;
            trackValue.Text = next.TrackNumber > 0 ? (next.TrackCount > 0 ? next.TrackNumber + " of " + next.TrackCount : next.TrackNumber.ToString()) : "—";
            videoSource.Text = "NOW WATCHING  ·  " + next.Source.ToUpperInvariant();
            videoTitle.Text = next.Title;
            videoChannel.Text = next.Artist;
            videoChannel.Visibility = string.IsNullOrWhiteSpace(next.Artist) ? Visibility.Collapsed : Visibility.Visible;
            if (next.Artwork != null)
            {
                artImage.Source = videoThumb.Source = Decode(next.Artwork, 1208);
                var tiny = Decode(next.Artwork, 12);
                SwapCover(tiny);
                coverAccent = Accent(tiny);
                highlight = settings.HighlightColor == "Cover" ? coverAccent : Graphite.Ink;
            }
            else if (!sameSong) videoThumb.Source = null;
            if (sameSong) return;

            var request = ++lyricsRequest;
            ShowLyrics(LyricsView.Loading, LyricsKind.None, null);
            LyricsResult result;
            try
            {
                result = await LyricsProvider.FetchAsync(next);
            }
            catch (Exception error)
            {
                Log.Write("lyrics: " + error.Message);
                result = new LyricsResult();
            }
            if (request != lyricsRequest) return;
            if (result.Kind == LyricsKind.Instrumental) ShowLyrics(LyricsView.Instrumental, result.Kind, null);
            else if (result.Lines.Count == 0) ShowLyrics(LyricsView.NotFound, result.Kind, null);
            else ShowLyrics(LyricsView.Lines, result.Kind, result.Lines);
        }

        void ShowLyrics(LyricsView next, LyricsKind kind, List<LyricLine> source)
        {
            view = next;
            lyricsKind = kind;
            lyricsFade = 0;
            BuildLines(source ?? new List<LyricLine>());
            emptyState.Visibility = next == LyricsView.Lines ? Visibility.Collapsed : Visibility.Visible;
            emptyBubble.Pill.Visibility = next == LyricsView.NotFound ? Visibility.Collapsed : Visibility.Visible;
            emptyTitle.Text = next == LyricsView.Instrumental ? "Instrumental" : next == LyricsView.NotFound ? "No lyrics for this song" : "Finding lyrics";
            emptyDetail.Text = next == LyricsView.NotFound ? "LRCLIB has no lyrics for “" + track?.Title + "”" : next == LyricsView.Instrumental ? "Nothing to sing along to. Enjoy the music." : "Searching LRCLIB…";

            var status = next == LyricsView.Loading ? Tuple.Create("Searching", Graphite.Muted)
                : next == LyricsView.Instrumental ? Tuple.Create("Instrumental", Graphite.Muted)
                : next == LyricsView.NotFound ? Tuple.Create("Not found", Graphite.Dormant)
                : kind == LyricsKind.Plain ? Tuple.Create("Unsynced", Graphite.Wip)
                : Tuple.Create("Synced", Graphite.Live);
            lyricsValue.Text = status.Item1;
            lyricsDot.Fill = Graphite.Brush(status.Item2);
        }

        static BitmapSource Decode(byte[] bytes, int width)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = width;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }

        static Color Accent(BitmapSource tiny)
        {
            var pixels = new FormatConvertedBitmap(tiny, PixelFormats.Bgra32, null, 0);
            var bytes = new byte[pixels.PixelWidth * pixels.PixelHeight * 4];
            pixels.CopyPixels(bytes, pixels.PixelWidth * 4, 0);
            var best = Graphite.Ink;
            var bestChroma = -1;
            for (var i = 0; i < bytes.Length; i += 4)
            {
                var chroma = Math.Max(bytes[i], Math.Max(bytes[i + 1], bytes[i + 2])) - Math.Min(bytes[i], Math.Min(bytes[i + 1], bytes[i + 2]));
                if (chroma <= bestChroma) continue;
                bestChroma = chroma;
                best = Color.FromRgb(bytes[i + 2], bytes[i + 1], bytes[i]);
            }
            if (bestChroma < 40) return Graphite.Ink;
            byte Lift(byte channel) => (byte)(channel + (255 - channel) * 0.45);
            return Color.FromRgb(Lift(best.R), Lift(best.G), Lift(best.B));
        }

        void SwapCover(BitmapSource tiny)
        {
            var layer = new CoverLayer { Canvas = new Canvas { Width = designWidth, Height = designHeight, Opacity = 0, ClipToBounds = true } };
            layer.Canvas.Effect = new BlurEffect { Radius = 70, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance };
            var stretch = Math.Max(designWidth / BaseWidth, designHeight / BaseHeight);
            var placements = new[] { new { X = 330.0, Y = 260.0, Size = 1500.0, Speed = 4.0 }, new { X = 1500.0, Y = 200.0, Size = 1700.0, Speed = -3.0 }, new { X = 960.0, Y = 40.0, Size = 1300.0, Speed = 5.5 } };
            foreach (var p in placements)
            {
                var size = p.Size * stretch;
                var rotate = new RotateTransform(p.Speed * 17, size / 2, size / 2);
                var scale = new ScaleTransform(1, 1, size / 2, size / 2);
                var image = new Image { Source = tiny, Width = size, Height = size, Stretch = Stretch.Fill, RenderTransform = new TransformGroup { Children = { scale, rotate } } };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.Linear);
                Canvas.SetLeft(image, p.X * designWidth / BaseWidth - size / 2);
                Canvas.SetTop(image, p.Y * designHeight / BaseHeight - size / 2);
                layer.Canvas.Children.Add(image);
                layer.Blobs.Add(Tuple.Create(rotate, scale, p.Speed, p.X));
            }
            coverHost.Children.Add(layer.Canvas);
            if (cover != null) coverFading.Add(cover);
            cover = layer;
        }

        static bool IsRightToLeft(char c) => (c >= 0x0590 && c <= 0x08FF) || (c >= 0xFB1D && c <= 0xFDFF) || (c >= 0xFE70 && c <= 0xFEFF);

        static FlowDirection? Direction(string text)
        {
            foreach (var c in text)
            {
                if (IsRightToLeft(c)) return FlowDirection.RightToLeft;
                if (char.IsLetter(c)) return FlowDirection.LeftToRight;
            }
            return null;
        }

        static List<LyricWord> Retime(List<LyricWord> words, List<LyricWord> original)
        {
            if (words.Count == 0 || original.Count == 0) return words;
            double start = original.First().Start, end = original.Last().End, from = words.First().Start, span = Math.Max(0.01, words.Last().End - from);
            return words.Select(w => new LyricWord { Text = w.Text + " ", Start = start + (w.Start - from) / span * (end - start), End = start + (w.End - from) / span * (end - start) }).ToList();
        }

        string LeadSinger(List<LyricLine> source)
        {
            var singers = source.Where(line => line.Singer != null).Select(line => line.Singer).ToList();
            if (singers.Count == 0) return null;
            var mainArtist = LyricsProvider.Simplify(System.Text.RegularExpressions.Regex.Split(track?.Artist ?? "", @",|&|\s+(?:feat\.?|ft\.?|x|with)\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase)[0]);
            return singers.Distinct().FirstOrDefault(singer => mainArtist.Length > 0 && (LyricsProvider.Simplify(singer).Contains(mainArtist) || mainArtist.Contains(LyricsProvider.Simplify(singer))))
                   ?? (singers.Contains("v1") ? "v1" : singers.GroupBy(singer => singer).OrderByDescending(group => group.Count()).First().Key);
        }

        WrapPanel WordsPanel(LineView lineView, List<LyricWord> words, double size, HorizontalAlignment alignment)
        {
            var panel = new WrapPanel
            {
                MaxWidth = lyricsWidth - 20,
                HorizontalAlignment = alignment,
                Margin = new Thickness(0, 0, alignment == HorizontalAlignment.Right ? 20 : 0, 0),
                FlowDirection = Direction(string.Concat(words.Select(w => w.Text))) ?? FlowDirection.LeftToRight
            };
            var font = LyricFont;
            var gap = WordGap * size / 50;
            var runDirection = panel.FlowDirection;
            WrapPanel run = null;
            TextBlock runFirst = null;
            var previousSpace = false;
            foreach (var word in words)
            {
                var wordDirection = Direction(word.Text) ?? runDirection;
                if (wordDirection != runDirection)
                {
                    if (runFirst != null) runFirst.Margin = new Thickness(previousSpace ? gap : 0, 0, 0, 0);
                    runFirst = null;
                    runDirection = wordDirection;
                    run = wordDirection == panel.FlowDirection ? null : new WrapPanel { FlowDirection = wordDirection };
                    if (run != null) panel.Children.Add(run);
                }
                var space = char.IsWhiteSpace(word.Text.LastOrDefault());
                var forward = wordDirection == FlowDirection.LeftToRight;
                var litStart = new GradientStop(highlight, 0);
                var lit = new GradientStop(highlight, 0);
                var dim = new GradientStop(Dim, 0);
                var brush = new LinearGradientBrush(new GradientStopCollection { litStart, lit, dim, new GradientStop(Dim, 1) }, new Point(forward ? 0 : 1, 0), new Point(forward ? 1 : 0, 0));
                var lift = new TranslateTransform();
                var block = new TextBlock
                {
                    Text = word.Text.TrimEnd(), FontFamily = font, FontSize = size, FontWeight = FontWeights.Bold, Foreground = brush, RenderTransform = lift,
                    Margin = run == null ? new Thickness(0, 0, space ? gap : 0, 0) : new Thickness(runFirst != null && previousSpace ? gap : 0, 0, 0, 0)
                };
                (run ?? panel).Children.Add(block);
                if (run != null && runFirst == null) runFirst = block;
                previousSpace = space;
                lineView.Words.Add(new WordView { Word = word, Block = block, LitStart = litStart, LitEdge = lit, DimEdge = dim, Lift = lift });
            }
            return panel;
        }

        void BuildLines(List<LyricLine> source)
        {
            sourceLines = source;
            japaneseSong = source.Any(l => l.Words.Any(w => w.Text.Any(Romanizer.IsKana)));
            var rightToLeftSong = source.Count(l => Direction(string.Concat(l.Words.Select(w => w.Text))) == FlowDirection.RightToLeft) * 2 > source.Count(l => !l.IsGap);
            var songAlignment = settings.LyricAlign == "Center" ? HorizontalAlignment.Center : rightToLeftSong ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            var otherSide = songAlignment == HorizontalAlignment.Left ? HorizontalAlignment.Right : songAlignment == HorizontalAlignment.Right ? HorizontalAlignment.Left : HorizontalAlignment.Center;
            var lead = LeadSinger(source);
            var size = 50 * LyricScale;
            lyricsStack.Children.Clear();
            lines = new List<LineView>();
            foreach (var line in source)
            {
                var lineView = new LineView { Line = line, Blur = new BlurEffect { RenderingBias = RenderingBias.Performance }, Scale = new ScaleTransform(0.96, 0.96) };
                var alignment = line.Singer == null || line.Singer == lead ? songAlignment : otherSide;
                if (line.IsGap)
                {
                    lineView.Bubble = new Bubble(15 * LyricScale);
                    lineView.Bubble.Pill.Margin = songAlignment == HorizontalAlignment.Right ? new Thickness(0, 6, 22, 30) : new Thickness(2, 6, 0, 30);
                    lineView.Bubble.Pill.HorizontalAlignment = songAlignment;
                    lineView.Element = lineView.Bubble.Pill;
                }
                else
                {
                    var original = string.Concat(line.Words.Select(w => w.Text)).Trim();
                    var romanized = settings.RomanizeMode == "Off" ? null : Romanizer.Romanize(original, japaneseSong);
                    var words = romanized != null && settings.RomanizeMode == "Only" ? Retime(LyricsProvider.BuildLine(romanized, line.Start, line.End).Words, line.Words) : line.Words;
                    var main = new List<LyricWord>();
                    var backing = new List<LyricWord>();
                    var depth = 0;
                    foreach (var word in words)
                    {
                        var inside = depth > 0 || "(（".IndexOf(word.Text.TrimStart().FirstOrDefault()) >= 0;
                        depth = Math.Max(0, depth + word.Text.Count(c => c == '(' || c == '（') - word.Text.Count(c => c == ')' || c == '）'));
                        var text = new string(word.Text.Where(c => "()（）".IndexOf(c) < 0).ToArray());
                        if (text.Trim().Length > 0) (inside ? backing : main).Add(new LyricWord { Text = text, Start = word.Start, End = word.End });
                    }
                    var onlyBacking = main.Count == 0;
                    var container = new StackPanel { Margin = new Thickness(0, 0, 0, 24 * LyricScale), Background = Brushes.Transparent, Cursor = Cursors.Hand };
                    var start = line.Start;
                    container.MouseLeftButtonUp += (s, e) => watcher.Seek(start - settings.LyricOffsetSeconds - (track != null && nudges.TryGetValue(track.Key, out var nudge) ? nudge : 0) + 0.05);
                    if (!onlyBacking) container.Children.Add(WordsPanel(lineView, main, size, alignment));
                    if (backing.Count > 0)
                    {
                        var backingPanel = WordsPanel(lineView, backing, size * 0.62, alignment);
                        backingPanel.Opacity = 0.8;
                        backingPanel.Margin = new Thickness(backingPanel.Margin.Left, onlyBacking ? 0 : 4 * LyricScale, backingPanel.Margin.Right, 0);
                        container.Children.Add(backingPanel);
                    }
                    if (romanized != null && settings.RomanizeMode == "Below")
                        container.Children.Add(new TextBlock { Text = romanized, FontFamily = LyricFont, FontSize = 25 * LyricScale, Foreground = Graphite.Brush(Graphite.Ink, 0xB4), TextWrapping = TextWrapping.Wrap, Width = lyricsWidth - 20, TextAlignment = alignment == HorizontalAlignment.Center ? TextAlignment.Center : alignment == HorizontalAlignment.Right ? TextAlignment.Right : TextAlignment.Left, Margin = new Thickness(2, 2, alignment == HorizontalAlignment.Right ? 20 : 0, 0) });
                    lineView.Element = container;
                }
                lineView.Element.Effect = lineView.Blur;
                lineView.Element.RenderTransformOrigin = new Point(alignment == HorizontalAlignment.Center ? 0.5 : alignment == HorizontalAlignment.Right ? 1 : 0, 0.5);
                lineView.Element.RenderTransform = lineView.Scale;
                lyricsStack.Children.Add(lineView.Element);
                lines.Add(lineView);
            }
            layoutDirty = true;
            snapLines = true;
        }

        void MeasureLines()
        {
            lyricsStack.UpdateLayout();
            emptyState.UpdateLayout();
            Canvas.SetTop(emptyState, (designHeight - emptyState.ActualHeight) / 2);
            statsPanel.UpdateLayout();
            Canvas.SetTop(statsPanel, (designHeight - statsPanel.RenderSize.Height * ((ScaleTransform)statsPanel.LayoutTransform).ScaleY) / 2);
            videoDetails.UpdateLayout();
            Canvas.SetTop(videoDetails, (designHeight - videoDetails.ActualHeight) / 2);
            foreach (var line in lines)
            {
                line.Top = line.Element.TranslatePoint(new Point(0, 0), lyricsStack).Y;
                line.Height = line.Element.ActualHeight;
            }
            layoutDirty = false;
        }

        static double TargetOpacity(int distance) => distance == 0 ? 1 : distance == 1 ? 0.72 : distance == 2 ? 0.5 : distance == 3 ? 0.36 : 0.24;
        static double TargetBlur(int distance) => distance == 0 ? 0 : distance == 1 ? 0.6 : distance == 2 ? 1.8 : distance == 3 ? 3 : 4.5;

        void OnFrame(object sender, EventArgs e)
        {
            var now = clock.Elapsed.TotalSeconds;
            var dt = Math.Min(0.1, now - lastFrame);
            lastFrame = now;
            if (layoutDirty) MeasureLines();

            var active = track != null && (playing || (DateTime.UtcNow - lastPlayingAt).TotalSeconds < settings.IdleAfterPauseSeconds);
            var target = active ? 1.0 : 0.0;
            if (target != showTo)
            {
                showFrom = show;
                showTo = target;
                showStart = now;
                showDuration = target > 0 ? 0.75 : 0.6;
            }
            show = showFrom + (showTo - showFrom) * Graphite.Settle((now - showStart) / showDuration);
            content.Opacity = show;
            coverHost.Opacity = show;
            contentScale.ScaleX = contentScale.ScaleY = 0.97 + 0.03 * show;
            var transitioning = show > 0.001 && show < 0.999;
            contentBlur.Radius = 16 * (1 - show);
            content.Effect = transitioning ? contentBlur : null;
            idle.SetPlaying((show < 0.999 || settings.BackgroundStyle == "Idle") && !blackout);
            var root = (UIElement)Content;
            root.Opacity = Approach(root.Opacity, blackout ? 0 : 1, dt, 4);

            AnimateCover(now, dt);
            var videoTarget = track != null && track.IsVideo && (view == LyricsView.Loading || view == LyricsView.NotFound) ? 1.0 : 0.0;
            if (track != null) videoMix = show <= 0.001 ? videoTarget : Approach(videoMix, videoTarget, dt, 5);
            musicLayer.Opacity = 1 - videoMix;
            videoLayer.Opacity = videoMix;
            musicLayer.Visibility = videoMix > 0.999 ? Visibility.Hidden : Visibility.Visible;
            videoLayer.Visibility = videoMix < 0.001 ? Visibility.Hidden : Visibility.Visible;
            if (show <= 0.001) return;

            lyricsFade = Approach(lyricsFade, 1, dt, 5);
            lyricsStack.Opacity = lyricsFade;
            emptyState.Opacity = lyricsFade;
            if (view != LyricsView.Lines) emptyBubble.Animate(view == LyricsView.Instrumental ? (now % 4.5) / 4.5 * 0.9 : 0.3 + 0.2 * Math.Sin(now * 2), now);

            var position = watcher.Predict();
            UpdateStats(position);
            AnimateControls(dt);
            if (lines.Count == 0) return;

            if (paintedHighlight != highlight)
            {
                paintedHighlight = highlight;
                foreach (var word in lines.SelectMany(line => line.Words))
                    word.LitStart.Color = word.LitEdge.Color = highlight;
            }
            var t = position + settings.LyricOffsetSeconds + (track != null && nudges.TryGetValue(track.Key, out var nudge) ? nudge : 0);
            var unsynced = lyricsKind == LyricsKind.Plain;
            var current = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Line.Start <= t) current = i; else break;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var lineView = lines[i];
                var distance = current < 0 ? i + 1 : Math.Abs(i - current);
                var isCurrent = i == current;
                var visible = distance <= 5;
                lineView.Element.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
                if (!visible) continue;
                var opacity = unsynced ? (distance <= 2 ? 0.82 : 0.45) : TargetOpacity(distance);
                var blur = unsynced ? 0 : TargetBlur(distance);
                lineView.Opacity = snapLines ? opacity : Approach(lineView.Opacity, opacity, dt, 7);
                lineView.BlurRadius = snapLines ? blur : Approach(lineView.BlurRadius, blur, dt, 7);
                lineView.Element.Opacity = lineView.Opacity;
                lineView.Blur.Radius = lineView.BlurRadius;
                var emphasis = unsynced || isCurrent ? 1 : 0.96;
                lineView.Scale.ScaleX = lineView.Scale.ScaleY = snapLines ? emphasis : Approach(lineView.Scale.ScaleX, emphasis, dt, 8);
                if (lineView.Bubble != null) lineView.Bubble.Animate(isCurrent ? Clamp((t - lineView.Line.Start) / Math.Max(0.1, lineView.Line.End - lineView.Line.Start)) : 0, now);
                else if (unsynced) AnimateWords(lineView, t, false, true, true);
                else AnimateWords(lineView, t, isCurrent, i < current, !lineView.Line.WordTimed && settings.Highlight == "Lines");
            }

            var focus = lines[Math.Max(0, current)];
            var scrollTarget = designHeight * 0.46 - (focus.Top + focus.Height / 2);
            scroll.Y = snapLines ? scrollTarget : Approach(scroll.Y, scrollTarget, dt, 6.5);
            snapLines = false;
        }

        void UpdateStats(double position)
        {
            if (track == null) return;
            var duration = Math.Max(track.DurationSeconds, 0);
            var elapsed = (int)Math.Max(0, Math.Min(position, duration > 0 ? duration : position));
            if (elapsed != lastElapsedSecond)
            {
                lastElapsedSecond = elapsed;
                elapsedText.Text = videoElapsed.Text = Clock(elapsed);
                remainingText.Text = videoRemaining.Text = duration > 0 ? "-" + Clock((int)Math.Max(0, duration - elapsed)) : "";
            }
            var fraction = duration > 0 ? Clamp(position / duration) : 0;
            progressFill.Width = StatsInner * fraction;
            videoProgress.Width = videoDetails.Width * fraction;
            seekFill.Width = SeekWidth * (scrubbing ?? fraction);
            controlsElapsed.Text = elapsedText.Text;
            controlsRemaining.Text = remainingText.Text;
        }

        void AnimateControls(double dt)
        {
            var wanted = (IsMouseOver || ForceControls) && track != null;
            if (wanted && (DateTime.UtcNow - volumeReadAt).TotalSeconds > 2 && Mouse.Captured == null)
            {
                volumeReadAt = DateTime.UtcNow;
                var volume = AppVolume.Get(watcher.AppId);
                volumeGroup.Visibility = volume.HasValue ? Visibility.Visible : Visibility.Collapsed;
                volumeFill.Width = VolumeWidth * (volume ?? 0);
            }
            controlsMix = Approach(controlsMix, wanted ? 1 : 0, dt, 9);
            controls.Opacity = controlsMix;
            controls.Visibility = controlsMix < 0.01 ? Visibility.Hidden : Visibility.Visible;
            controls.IsHitTestVisible = controlsMix > 0.5;
            var shape = watcher.IsPlaying ? PauseShape : PlayShape;
            if (playGlyph.Data != shape) playGlyph.Data = shape;
        }

        static string Clock(int seconds) => seconds >= 3600
            ? (seconds / 3600) + ":" + (seconds / 60 % 60).ToString("00") + ":" + (seconds % 60).ToString("00")
            : (seconds / 60) + ":" + (seconds % 60).ToString("00");

        void AnimateWords(LineView lineView, double t, bool isCurrent, bool isPast, bool wholeLine)
        {
            var lineProgress = isCurrent ? Graphite.Settle(Clamp((t - lineView.Line.Start) / 0.35)) : isPast ? 1 : 0;
            foreach (var w in lineView.Words)
            {
                var progress = wholeLine ? lineProgress : isCurrent ? Clamp((t - w.Word.Start) / Math.Max(0.05, w.Word.End - w.Word.Start)) : isPast ? 1 : 0;
                var edge = wholeLine ? (progress > 0 ? 1.2 : -0.2) : progress * 1.25 - 0.12;
                w.LitEdge.Offset = Clamp(edge);
                w.DimEdge.Offset = Clamp(edge + 0.12);
                w.Block.Opacity = wholeLine && isCurrent ? 0.55 + 0.45 * progress : 1;
                var swell = isCurrent ? (wholeLine ? 0.4 * progress : Math.Sin(Math.PI * progress)) : 0;
                w.Lift.Y = wholeLine ? 0 : -4 * swell;
                if (isCurrent && progress > 0)
                {
                    var glow = w.Block.Effect as DropShadowEffect;
                    if (glow == null) w.Block.Effect = glow = new DropShadowEffect { ShadowDepth = 0, Color = Graphite.Ink, BlurRadius = 22, RenderingBias = RenderingBias.Performance };
                    glow.Opacity = 0.16 + 0.6 * swell;
                }
                else if (w.Block.Effect != null) w.Block.Effect = null;
            }
        }

        void AnimateCover(double now, double dt)
        {
            if (cover != null)
            {
                cover.Canvas.Opacity = Approach(cover.Canvas.Opacity, 1, dt, 1.6);
                if (show > 0.001)
                {
                    foreach (var blob in cover.Blobs)
                    {
                        blob.Item1.Angle = blob.Item3 * now * 1.6;
                        blob.Item2.ScaleX = blob.Item2.ScaleY = 1 + 0.12 * Math.Sin(now * 0.21 + blob.Item4);
                    }
                }
            }
            for (int i = coverFading.Count - 1; i >= 0; i--)
            {
                var old = coverFading[i];
                old.Canvas.Opacity = Approach(old.Canvas.Opacity, 0, dt, 1.6);
                if (old.Canvas.Opacity < 0.01)
                {
                    coverHost.Children.Remove(old.Canvas);
                    coverFading.RemoveAt(i);
                }
            }
        }

        static double Approach(double value, double target, double dt, double rate) => value + (target - value) * (1 - Math.Exp(-dt * rate));

        static double Clamp(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
