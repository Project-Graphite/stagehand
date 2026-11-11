using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        public const double ScreenWidth = 1920, ScreenHeight = 464;
        const double ArtLeft = 48, ArtTop = 24, ArtSize = 352;
        const double WordGap = 14;
        const double LyricsLeft = ArtLeft + ArtSize + 52;
        const double StatsLeft = 1496, StatsWidth = 384, StatsInner = StatsWidth - 56;
        static readonly Color Dim = Color.FromArgb(0x8C, Graphite.Ink.R, Graphite.Ink.G, Graphite.Ink.B);
        static readonly Color Soft = Color.FromArgb(0xB4, Graphite.Ink.R, Graphite.Ink.G, Graphite.Ink.B);

        readonly Settings settings;
        readonly NowPlayingWatcher watcher = new NowPlayingWatcher();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly IdleBackground idle = new IdleBackground();
        readonly Grid coverHost = new Grid();
        readonly Grid content = new Grid { Opacity = 0 };
        readonly ScaleTransform contentScale = new ScaleTransform(0.97, 0.97, ScreenWidth / 2, ScreenHeight / 2);
        readonly BlurEffect contentBlur = new BlurEffect { Radius = 16, RenderingBias = RenderingBias.Performance };
        readonly Image artImage = new Image { Width = ArtSize, Height = ArtSize, Stretch = Stretch.UniformToFill };
        readonly TextBlock titleText = Text(26, Graphite.Ink, FontWeights.SemiBold);
        readonly TextBlock artistText = Text(20, Graphite.Muted, FontWeights.Normal);
        readonly Canvas lyricsViewport = new Canvas { Height = ScreenHeight, ClipToBounds = true };
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
        readonly StackPanel videoDetails = new StackPanel { Width = 1120 };
        readonly TextBlock videoSource = Text(15, Graphite.Muted, FontWeights.SemiBold);
        readonly TextBlock videoTitle = Text(52, Graphite.Ink, FontWeights.SemiBold);
        readonly TextBlock videoChannel = Text(26, Graphite.Muted, FontWeights.Normal);
        readonly TextBlock videoElapsed = Text(30, Graphite.Ink, FontWeights.Normal);
        readonly TextBlock videoRemaining = Text(20, Graphite.Muted, FontWeights.Normal);
        readonly Border videoProgress = new Border { Height = 8, CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left, Background = Graphite.Brush(Graphite.Ink) };
        double videoMix;

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
            Width = ScreenWidth;
            Height = ScreenHeight;

            var root = new Grid { Width = ScreenWidth, Height = ScreenHeight, ClipToBounds = true };
            root.Children.Add(idle);
            root.Children.Add(coverHost);
            root.Children.Add(content);
            Content = root;
            content.RenderTransform = contentScale;
            BuildContent();
            ApplySettings();
            settings.Changed += ApplySettings;

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
            var bitmap = new RenderTargetBitmap((int)ScreenWidth, (int)ScreenHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render((Visual)Content);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(path)) encoder.Save(file);
        }

        static TextBlock Text(double size, Color color, FontWeight weight) => new TextBlock
        {
            FontFamily = Graphite.Sans, FontSize = size, FontWeight = weight, Foreground = Graphite.Brush(color), TextTrimming = TextTrimming.CharacterEllipsis
        };

        double LyricsWidth => settings.ShowStats ? StatsLeft - LyricsLeft - 40 : ScreenWidth - LyricsLeft - 60;

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

            artImage.Clip = new RectangleGeometry(new Rect(0, 0, ArtSize, ArtSize), 18, 18);
            RenderOptions.SetBitmapScalingMode(artImage, BitmapScalingMode.HighQuality);
            Place(musicLayer, artImage, ArtLeft, ArtTop);
            Place(musicLayer, new Border { Width = ArtSize, Height = ArtSize, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), BorderBrush = Graphite.Brush(Graphite.Ink, 0x22) }, ArtLeft, ArtTop);
            titleText.Width = artistText.Width = ArtSize;
            Place(musicLayer, titleText, ArtLeft, ArtTop + ArtSize + 10);
            Place(musicLayer, artistText, ArtLeft, ArtTop + ArtSize + 46);

            lyricsViewport.OpacityMask = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(Colors.Transparent, 0),
                new GradientStop(Colors.Black, 0.18),
                new GradientStop(Colors.Black, 0.82),
                new GradientStop(Colors.Transparent, 1)
            }, new Point(0, 0), new Point(0, ScreenHeight)) { MappingMode = BrushMappingMode.Absolute };
            lyricsStack.RenderTransform = scroll;
            lyricsViewport.Children.Add(lyricsStack);
            emptyState.Children.Add(emptyBubble.Pill);
            emptyBubble.Pill.Margin = new Thickness(0, 0, 0, 22);
            emptyState.Children.Add(emptyTitle);
            emptyState.Children.Add(emptyDetail);
            emptyDetail.Margin = new Thickness(0, 8, 0, 0);
            lyricsViewport.Children.Add(emptyState);
            Place(musicLayer, lyricsViewport, LyricsLeft, 0);

            BuildStats();
            Place(musicLayer, statsPanel, StatsLeft, 64);

            content.Children.Add(videoLayer);
            BuildVideo();
        }

        void BuildVideo()
        {
            videoThumb.Clip = new RectangleGeometry(new Rect(0, 0, 604, 340), 16, 16);
            RenderOptions.SetBitmapScalingMode(videoThumb, BitmapScalingMode.HighQuality);
            Place(videoLayer, new Border { Width = 604, Height = 340, CornerRadius = new CornerRadius(16), Background = Graphite.Brush(Graphite.Surface, 0xB8) }, 64, 62);
            Place(videoLayer, videoThumb, 64, 62);
            Place(videoLayer, new Border { Width = 604, Height = 340, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), BorderBrush = Graphite.Brush(Graphite.Ink, 0x22) }, 64, 62);

            videoTitle.TextWrapping = TextWrapping.Wrap;
            videoTitle.MaxHeight = 142;
            videoTitle.Margin = new Thickness(0, 12, 0, 0);
            videoChannel.Margin = new Thickness(0, 10, 0, 0);
            videoDetails.Children.Add(videoSource);
            videoDetails.Children.Add(videoTitle);
            videoDetails.Children.Add(videoChannel);
            AddProgress(videoDetails, videoElapsed, videoRemaining, videoProgress, new Thickness(0, 34, 0, 0), new Thickness(0, 12, 0, 0));
            Place(videoLayer, videoDetails, 720, 0);
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

        static void Place(Canvas canvas, UIElement element, double x, double y)
        {
            Canvas.SetLeft(element, x);
            Canvas.SetTop(element, y);
            canvas.Children.Add(element);
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
            statsPanel.Visibility = settings.ShowStats ? Visibility.Visible : Visibility.Collapsed;
            lyricsViewport.Width = LyricsWidth;
            lyricsStack.Width = LyricsWidth;
            emptyState.Width = LyricsWidth;
            if (sourceLines.Count > 0) BuildLines(sourceLines);
            ScreenPlacement.CursorGuardEnabled = settings.KeepMouseOff;
            layoutDirty = true;
        }

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
                SwapCover(Decode(next.Artwork, 12));
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
                : kind == LyricsKind.Plain ? Tuple.Create("Plain, timing estimated", Graphite.Wip)
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

        void SwapCover(BitmapSource tiny)
        {
            var layer = new CoverLayer { Canvas = new Canvas { Width = ScreenWidth, Height = ScreenHeight, Opacity = 0, ClipToBounds = true } };
            layer.Canvas.Effect = new BlurEffect { Radius = 70, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance };
            var placements = new[] { new { X = 330.0, Y = 260.0, Size = 1500.0, Speed = 4.0 }, new { X = 1500.0, Y = 200.0, Size = 1700.0, Speed = -3.0 }, new { X = 960.0, Y = 40.0, Size = 1300.0, Speed = 5.5 } };
            foreach (var p in placements)
            {
                var rotate = new RotateTransform(p.Speed * 17, p.Size / 2, p.Size / 2);
                var scale = new ScaleTransform(1, 1, p.Size / 2, p.Size / 2);
                var image = new Image { Source = tiny, Width = p.Size, Height = p.Size, Stretch = Stretch.Fill, RenderTransform = new TransformGroup { Children = { scale, rotate } } };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.Linear);
                Canvas.SetLeft(image, p.X - p.Size / 2);
                Canvas.SetTop(image, p.Y - p.Size / 2);
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

        void BuildLines(List<LyricLine> source)
        {
            sourceLines = source;
            japaneseSong = source.Any(l => l.Words.Any(w => w.Text.Any(Romanizer.IsKana)));
            var rightToLeftSong = source.Count(l => Direction(string.Concat(l.Words.Select(w => w.Text))) == FlowDirection.RightToLeft) * 2 > source.Count(l => !l.IsGap);
            var alignment = rightToLeftSong ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            lyricsStack.Children.Clear();
            lines = new List<LineView>();
            foreach (var line in source)
            {
                var lineView = new LineView { Line = line, Blur = new BlurEffect { RenderingBias = RenderingBias.Performance } };
                if (line.IsGap)
                {
                    lineView.Bubble = new Bubble(15);
                    lineView.Bubble.Pill.Margin = rightToLeftSong ? new Thickness(0, 6, 22, 30) : new Thickness(2, 6, 0, 30);
                    lineView.Bubble.Pill.HorizontalAlignment = alignment;
                    lineView.Element = lineView.Bubble.Pill;
                }
                else
                {
                    var original = string.Concat(line.Words.Select(w => w.Text)).Trim();
                    var romanized = settings.RomanizeMode == "Off" ? null : Romanizer.Romanize(original, japaneseSong);
                    var words = romanized != null && settings.RomanizeMode == "Only" ? Retime(LyricsProvider.BuildLine(romanized, line.Start, line.End).Words, line.Words) : line.Words;
                    var panel = new WrapPanel
                    {
                        MaxWidth = LyricsWidth - 20,
                        HorizontalAlignment = alignment,
                        Margin = new Thickness(0, 0, rightToLeftSong ? 20 : 0, 0),
                        FlowDirection = Direction(string.Concat(words.Select(w => w.Text))) ?? FlowDirection.LeftToRight
                    };
                    var runDirection = panel.FlowDirection;
                    WrapPanel run = null;
                    TextBlock runFirst = null;
                    var previousSpace = false;
                    foreach (var word in words)
                    {
                        var wordDirection = Direction(word.Text) ?? runDirection;
                        if (wordDirection != runDirection)
                        {
                            if (runFirst != null) runFirst.Margin = new Thickness(previousSpace ? WordGap : 0, 0, 0, 0);
                            runFirst = null;
                            runDirection = wordDirection;
                            run = wordDirection == panel.FlowDirection ? null : new WrapPanel { FlowDirection = wordDirection };
                            if (run != null) panel.Children.Add(run);
                        }
                        var space = char.IsWhiteSpace(word.Text.LastOrDefault());
                        var forward = wordDirection == FlowDirection.LeftToRight;
                        var lit = new GradientStop(Graphite.Ink, 0);
                        var dim = new GradientStop(Dim, 0);
                        var brush = new LinearGradientBrush(new GradientStopCollection { new GradientStop(Graphite.Ink, 0), lit, dim, new GradientStop(Dim, 1) }, new Point(forward ? 0 : 1, 0), new Point(forward ? 1 : 0, 0));
                        var lift = new TranslateTransform();
                        var block = new TextBlock
                        {
                            Text = word.Text.TrimEnd(), FontFamily = Graphite.Sans, FontSize = 50, FontWeight = FontWeights.SemiBold, Foreground = brush, RenderTransform = lift,
                            Margin = run == null ? new Thickness(0, 0, space ? WordGap : 0, 0) : new Thickness(runFirst != null && previousSpace ? WordGap : 0, 0, 0, 0)
                        };
                        (run ?? panel).Children.Add(block);
                        if (run != null && runFirst == null) runFirst = block;
                        previousSpace = space;
                        lineView.Words.Add(new WordView { Word = word, Block = block, LitEdge = lit, DimEdge = dim, Lift = lift });
                    }
                    var container = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };
                    container.Children.Add(panel);
                    if (romanized != null && settings.RomanizeMode == "Below")
                        container.Children.Add(new TextBlock { Text = romanized, FontFamily = Graphite.Sans, FontSize = 25, Foreground = Graphite.Brush(Graphite.Ink, 0xB4), TextWrapping = TextWrapping.Wrap, Width = LyricsWidth - 20, Margin = new Thickness(2, 2, 0, 0) });
                    lineView.Element = container;
                }
                lineView.Element.Effect = lineView.Blur;
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
            Canvas.SetTop(emptyState, (ScreenHeight - emptyState.ActualHeight) / 2);
            statsPanel.UpdateLayout();
            Canvas.SetTop(statsPanel, (ScreenHeight - statsPanel.ActualHeight) / 2);
            videoDetails.UpdateLayout();
            Canvas.SetTop(videoDetails, (ScreenHeight - videoDetails.ActualHeight) / 2);
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
            idle.SetPlaying(show < 0.999 && !blackout);
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
            if (lines.Count == 0) return;

            var t = position + settings.LyricOffsetSeconds;
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
                var opacity = TargetOpacity(distance);
                var blur = TargetBlur(distance);
                lineView.Opacity = snapLines ? opacity : Approach(lineView.Opacity, opacity, dt, 7);
                lineView.BlurRadius = snapLines ? blur : Approach(lineView.BlurRadius, blur, dt, 7);
                lineView.Element.Opacity = lineView.Opacity;
                lineView.Blur.Radius = lineView.BlurRadius;
                if (lineView.Bubble != null) lineView.Bubble.Animate(isCurrent ? Clamp((t - lineView.Line.Start) / Math.Max(0.1, lineView.Line.End - lineView.Line.Start)) : 0, now);
                else AnimateWords(lineView, t, isCurrent, i < current);
            }

            var focus = lines[Math.Max(0, current)];
            var scrollTarget = ScreenHeight * 0.46 - (focus.Top + focus.Height / 2);
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
        }

        static string Clock(int seconds) => seconds >= 3600
            ? (seconds / 3600) + ":" + (seconds / 60 % 60).ToString("00") + ":" + (seconds % 60).ToString("00")
            : (seconds / 60) + ":" + (seconds % 60).ToString("00");

        void AnimateWords(LineView lineView, double t, bool isCurrent, bool isPast)
        {
            foreach (var w in lineView.Words)
            {
                var progress = isCurrent ? Clamp((t - w.Word.Start) / Math.Max(0.05, w.Word.End - w.Word.Start)) : isPast ? 1 : 0;
                var edge = progress * 1.25 - 0.12;
                w.LitEdge.Offset = Clamp(edge);
                w.DimEdge.Offset = Clamp(edge + 0.12);
                var swell = isCurrent ? Math.Sin(Math.PI * progress) : 0;
                w.Lift.Y = -4 * swell;
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
