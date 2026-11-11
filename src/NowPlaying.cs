using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Stagehand
{
    sealed class TrackInfo
    {
        public string Title = "";
        public string Artist = "";
        public string Album = "";
        public int TrackNumber;
        public int TrackCount;
        public string Source = "";
        public double DurationSeconds;
        public byte[] Artwork;
        public bool IsVideo;

        public string Key => (Artist + "\u0001" + Title).ToLowerInvariant();
    }

    sealed class PlaybackSnapshot
    {
        public TrackInfo Track;
        public bool IsPlaying;
        public double PositionSeconds;
    }

    sealed class NowPlayingWatcher
    {
        GlobalSystemMediaTransportControlsSessionManager manager;
        string sessionApp;
        GlobalSystemMediaTransportControlsSession current;
        TrackInfo track;
        bool fetchingProperties;
        int propertiesRequest;
        DateTime lastPropertiesFetch = DateTime.MinValue;
        double anchorPosition;
        DateTime seekSettlesAt = DateTime.MinValue;
        DateTime anchorTime = DateTime.UtcNow;
        bool playing;
        bool demo;

        public event Action<TrackInfo> TrackChanged;

        public async Task StartAsync()
        {
            manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }

        public void StartDemo(TrackInfo demoTrack, double positionSeconds)
        {
            demo = true;
            demoTrack.IsVideo = LooksLikeVideo(false, "", demoTrack.Album, demoTrack.Artwork);
            track = demoTrack;
            playing = true;
            anchorPosition = positionSeconds;
            anchorTime = DateTime.UtcNow;
            TrackChanged?.Invoke(demoTrack);
        }

        public PlaybackSnapshot Poll()
        {
            if (demo) return new PlaybackSnapshot { Track = track, IsPlaying = true, PositionSeconds = Predict() };
            if (manager == null) return new PlaybackSnapshot();
            var chosen = manager.GetSessions().FirstOrDefault(s => s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                         ?? manager.GetCurrentSession();
            current = chosen;
            if (chosen == null)
            {
                sessionApp = null;
                track = null;
                playing = false;
                return new PlaybackSnapshot();
            }
            if (sessionApp != chosen.SourceAppUserModelId)
            {
                sessionApp = chosen.SourceAppUserModelId;
                track = null;
            }

            var sinceFetch = (DateTime.UtcNow - lastPropertiesFetch).TotalSeconds;
            if (fetchingProperties ? sinceFetch > 4 : track == null || sinceFetch > 1)
            {
                if (fetchingProperties) Log.Write("media properties: no answer from " + sessionApp + " in 4s, asking again");
                lastPropertiesFetch = DateTime.UtcNow;
                _ = RefreshPropertiesAsync(chosen, ++propertiesRequest);
            }

            var wasPlaying = playing;
            playing = chosen.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var timeline = chosen.GetTimelineProperties();
            var reported = timeline.Position.TotalSeconds;
            if (playing) reported += Math.Max(0, (DateTimeOffset.UtcNow - timeline.LastUpdatedTime).TotalSeconds);
            var drift = reported - Predict();
            if (DateTime.UtcNow >= seekSettlesAt)
            {
                if (!wasPlaying || !playing || Math.Abs(drift) > 1.5)
                {
                    anchorPosition = reported;
                    anchorTime = DateTime.UtcNow;
                }
                else anchorPosition += drift * 0.08;
            }
            if (track != null && timeline.EndTime > TimeSpan.Zero) track.DurationSeconds = timeline.EndTime.TotalSeconds;

            return new PlaybackSnapshot { Track = track, IsPlaying = playing, PositionSeconds = Predict() };
        }

        public double Predict()
        {
            return playing ? anchorPosition + (DateTime.UtcNow - anchorTime).TotalSeconds : anchorPosition;
        }

        public string AppId => sessionApp;

        public bool IsPlaying => playing;

        public void TogglePlayPause() => current?.TryTogglePlayPauseAsync();

        public void Next() => current?.TrySkipNextAsync();

        public void Previous() => current?.TrySkipPreviousAsync();

        public void Seek(double seconds)
        {
            if (current == null) return;
            seconds = Math.Max(0, seconds);
            _ = current.TryChangePlaybackPositionAsync((long)(seconds * TimeSpan.TicksPerSecond));
            anchorPosition = seconds;
            anchorTime = DateTime.UtcNow;
            seekSettlesAt = DateTime.UtcNow.AddSeconds(1.5);
        }

        static string FriendlySource(string appId)
        {
            var id = (appId ?? "").ToLowerInvariant();
            if (id.Contains("spotify")) return "Spotify";
            if (id.Contains("chrome")) return "Chrome";
            if (id.Contains("msedge") || id.Contains("edge")) return "Edge";
            if (id.Contains("firefox")) return "Firefox";
            if (id.Contains("applemusic") || id.Contains("apple")) return "Apple Music";
            if (id.Contains("zunemusic") || id.Contains("media")) return "Media Player";
            if (id.Contains("tidal")) return "Tidal";
            if (id.Contains("deezer")) return "Deezer";
            if (id.Contains("youtube")) return "YouTube Music";
            var name = id.Split('!', '\\').Last().Replace(".exe", "");
            return name.Length == 0 ? "Unknown" : char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        static bool LooksLikeVideo(bool reportedVideo, string appId, string album, byte[] artwork)
        {
            if (reportedVideo) return true;
            if (artwork == null) return string.IsNullOrWhiteSpace(album) && new[] { "chrome", "msedge", "firefox", "brave", "opera", "vivaldi" }.Any((appId ?? "").ToLowerInvariant().Contains);
            var frame = BitmapDecoder.Create(new MemoryStream(artwork), BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
            return frame.PixelWidth >= frame.PixelHeight * 1.25;
        }

        async Task RefreshPropertiesAsync(GlobalSystemMediaTransportControlsSession source, int request)
        {
            fetchingProperties = true;
            try
            {
                var props = await source.TryGetMediaPropertiesAsync();
                if (request != propertiesRequest || props == null || string.IsNullOrWhiteSpace(props.Title)) return;
                var next = new TrackInfo
                {
                    Title = props.Title,
                    Artist = props.Artist ?? "",
                    Album = props.AlbumTitle ?? "",
                    TrackNumber = props.TrackNumber,
                    TrackCount = props.AlbumTrackCount,
                    Source = FriendlySource(source.SourceAppUserModelId),
                    DurationSeconds = source.GetTimelineProperties().EndTime.TotalSeconds
                };
                var sameTrack = track != null && track.Key == next.Key;
                if (sameTrack && (track.Artwork != null || props.Thumbnail == null)) return;
                if (sameTrack) next.DurationSeconds = track.DurationSeconds;
                if (props.Thumbnail != null)
                {
                    var thumbnail = ReadThumbnailAsync(props.Thumbnail);
                    if (await Task.WhenAny(thumbnail, Task.Delay(3000)) == thumbnail) next.Artwork = await thumbnail;
                }
                next.IsVideo = LooksLikeVideo(props.PlaybackType == MediaPlaybackType.Video, source.SourceAppUserModelId, next.Album, next.Artwork);
                if (request != propertiesRequest || sessionApp != source.SourceAppUserModelId) return;
                track = next;
                TrackChanged?.Invoke(next);
            }
            catch (Exception error)
            {
                Log.Write("media properties: " + error.Message);
            }
            finally
            {
                if (request == propertiesRequest) fetchingProperties = false;
            }
        }

        static async Task<byte[]> ReadThumbnailAsync(IRandomAccessStreamReference thumbnail)
        {
            using (var stream = await thumbnail.OpenReadAsync())
            using (var reader = new DataReader(stream))
            {
                var size = (uint)stream.Size;
                await reader.LoadAsync(size);
                var bytes = new byte[size];
                reader.ReadBytes(bytes);
                return bytes;
            }
        }
    }
}
