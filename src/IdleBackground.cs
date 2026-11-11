using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Stagehand
{
    sealed class IdleBackground : Canvas
    {
        static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };
        MediaElement video;
        string anchor = "Center";
        bool wantPlaying;

        public IdleBackground()
        {
            ClipToBounds = true;
            Background = Graphite.Brush(Graphite.Paper);
            SizeChanged += (s, e) => Fit();
        }

        public void Load(string path, string anchorName)
        {
            anchor = anchorName;
            Children.Clear();
            video = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Background = new RadialGradientBrush(Color.FromRgb(0x1C, 0x1C, 0x21), Graphite.Paper) { Center = new Point(0.72, 0.35), GradientOrigin = new Point(0.72, 0.35), RadiusX = 0.6, RadiusY = 1.6 };
                return;
            }
            Background = Graphite.Brush(Graphite.Paper);

            if (ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path);
                image.EndInit();
                image.Freeze();
                var element = new Image { Source = image, Stretch = Stretch.Fill };
                RenderOptions.SetBitmapScalingMode(element, BitmapScalingMode.HighQuality);
                Children.Add(element);
                Fit();
                return;
            }

            video = new MediaElement { Source = new Uri(path), LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual, IsMuted = true, Stretch = Stretch.Fill };
            video.MediaOpened += (s, e) => Fit();
            video.MediaEnded += (s, e) => { video.Position = TimeSpan.Zero; video.Play(); };
            Children.Add(video);
            if (wantPlaying) video.Play(); else { video.Play(); video.Pause(); }
        }

        public void SetPlaying(bool play)
        {
            wantPlaying = play;
            if (video == null) return;
            if (play) video.Play(); else video.Pause();
        }

        void Fit()
        {
            var element = Children.OfType<FrameworkElement>().FirstOrDefault();
            if (element == null || ActualWidth <= 0) return;
            double sourceWidth, sourceHeight;
            if (element is Image image && image.Source is BitmapSource bitmap) { sourceWidth = bitmap.PixelWidth; sourceHeight = bitmap.PixelHeight; }
            else if (element is MediaElement media && media.NaturalVideoWidth > 0) { sourceWidth = media.NaturalVideoWidth; sourceHeight = media.NaturalVideoHeight; }
            else return;

            var scale = Math.Max(ActualWidth / sourceWidth, ActualHeight / sourceHeight);
            element.Width = sourceWidth * scale;
            element.Height = sourceHeight * scale;
            SetLeft(element, (ActualWidth - element.Width) / 2);
            SetTop(element, anchor == "Top" ? 0 : anchor == "Bottom" ? ActualHeight - element.Height : (ActualHeight - element.Height) / 2);
        }
    }
}
