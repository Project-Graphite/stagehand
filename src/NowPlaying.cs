using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace GraphiteLyrics
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
        GlobalSystemMediaTransportControlsSession session;
        TrackInfo track;
        bool fetchingProperties;
        DateTime lastPropertiesFetch = DateTime.MinValue;
        double anchorPosition;
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
            if (chosen == null)
            {
                session = null;
                track = null;
                playing = false;
                return new PlaybackSnapshot();
            }
            if (session == null || session.SourceAppUserModelId != chosen.SourceAppUserModelId)
            {
                session = chosen;
                track = null;
            }

            if (!fetchingProperties && (track == null || (DateTime.UtcNow - lastPropertiesFetch).TotalSeconds > 1))
            {
                lastPropertiesFetch = DateTime.UtcNow;
                _ = RefreshPropertiesAsync(chosen);
            }

            var wasPlaying = playing;
            playing = chosen.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var timeline = chosen.GetTimelineProperties();
            var reported = timeline.Position.TotalSeconds;
            if (playing) reported += Math.Max(0, (DateTimeOffset.UtcNow - timeline.LastUpdatedTime).TotalSeconds);
            if (!wasPlaying || !playing || Math.Abs(reported - Predict()) > 0.35)
            {
                anchorPosition = reported;
                anchorTime = DateTime.UtcNow;
            }
            if (track != null && timeline.EndTime > TimeSpan.Zero) track.DurationSeconds = timeline.EndTime.TotalSeconds;

            return new PlaybackSnapshot { Track = track, IsPlaying = playing, PositionSeconds = Predict() };
        }

        public double Predict()
        {
            return playing ? anchorPosition + (DateTime.UtcNow - anchorTime).TotalSeconds : anchorPosition;
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

        async Task RefreshPropertiesAsync(GlobalSystemMediaTransportControlsSession source)
        {
            fetchingProperties = true;
            try
            {
                var props = await source.TryGetMediaPropertiesAsync();
                if (props == null || string.IsNullOrWhiteSpace(props.Title)) return;
                var next = new TrackInfo
                {
                    Title = props.Title,
                    Artist = props.Artist ?? "",
                    Album = props.AlbumTitle ?? "",
                    TrackNumber = props.TrackNumber,
                    TrackCount = props.AlbumTrackCount,
                    Source = FriendlySource(source.SourceAppUserModelId)
                };
                var sameTrack = track != null && track.Key == next.Key;
                if (sameTrack && (track.Artwork != null || props.Thumbnail == null)) return;
                if (sameTrack) next.DurationSeconds = track.DurationSeconds;
                if (props.Thumbnail != null)
                {
                    using (var stream = await props.Thumbnail.OpenReadAsync())
                    using (var reader = new DataReader(stream))
                    {
                        var size = (uint)stream.Size;
                        await reader.LoadAsync(size);
                        var bytes = new byte[size];
                        reader.ReadBytes(bytes);
                        next.Artwork = bytes;
                    }
                }
                if (session != source) return;
                track = next;
                TrackChanged?.Invoke(next);
            }
            catch (Exception error)
            {
                Log.Write("media properties: " + error.Message);
            }
            finally
            {
                fetchingProperties = false;
            }
        }
    }
}
