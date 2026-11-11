using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Stagehand
{
    sealed class LyricWord
    {
        public string Text;
        public double Start;
        public double End;
    }

    sealed class LyricLine
    {
        public double Start;
        public double End;
        public bool IsGap;
        public List<LyricWord> Words = new List<LyricWord>();
    }

    enum LyricsKind { Synced, Plain, Instrumental, None }

    sealed class LyricsResult
    {
        public List<LyricLine> Lines = new List<LyricLine>();
        public LyricsKind Kind = LyricsKind.None;
    }

    static class LyricsProvider
    {
        static readonly HttpClient Http = CreateClient();
        static readonly string CacheDir = Path.Combine(Log.DataDir, "lyrics");
        const string InstrumentalMarker = "[instrumental]";
        const double MinimumAutoGapSeconds = 6;

        static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Stagehand/0.2 (https://github.com/Project-Graphite/stagehand)");
            return client;
        }

        public static async Task<LyricsResult> FetchAsync(TrackInfo track)
        {
            var cacheFile = Path.Combine(CacheDir, SafeName(track.Key) + ".lrc");
            if (File.Exists(cacheFile)) return FromText(File.ReadAllText(cacheFile, Encoding.UTF8), track.DurationSeconds);

            foreach (var query in Queries(track))
            {
                var text = await LookupAsync(query.Item1, query.Item2, track);
                if (text == null) continue;
                Directory.CreateDirectory(CacheDir);
                File.WriteAllText(cacheFile, text, Encoding.UTF8);
                return FromText(text, track.DurationSeconds);
            }
            return new LyricsResult();
        }

        static LyricsResult FromText(string text, double duration)
        {
            return text.Trim() == InstrumentalMarker ? new LyricsResult { Kind = LyricsKind.Instrumental } : Parse(text, duration);
        }

        static IEnumerable<Tuple<string, string>> Queries(TrackInfo track)
        {
            var artist = Regex.Replace(track.Artist, @"\s*-\s*Topic$", "", RegexOptions.IgnoreCase).Trim();
            var title = track.Title.Trim();
            yield return Tuple.Create(artist, title);
            var cleaned = Regex.Replace(title, @"\s*[\(\[][^\)\]]*(official|video|audio|lyric|mv|hd|4k|remaster|visualizer)[^\)\]]*[\)\]]", "", RegexOptions.IgnoreCase).Trim();
            cleaned = Regex.Replace(cleaned, @"\s*(feat\.|ft\.).*$", "", RegexOptions.IgnoreCase).Trim();
            var dash = cleaned.IndexOf(" - ", StringComparison.Ordinal);
            if (dash > 0) yield return Tuple.Create(cleaned.Substring(0, dash).Trim(), cleaned.Substring(dash + 3).Trim());
            if (cleaned != title) yield return Tuple.Create(artist, cleaned);
            var firstArtist = artist.Split(new[] { ",", "&", " x ", "、" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstArtist) && firstArtist != artist) yield return Tuple.Create(firstArtist, cleaned);
        }

        static async Task<string> LookupAsync(string artist, string title, TrackInfo track)
        {
            var url = "https://lrclib.net/api/search?track_name=" + Uri.EscapeDataString(title) + "&artist_name=" + Uri.EscapeDataString(artist);
            var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            var results = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<List<Dictionary<string, object>>>(await response.Content.ReadAsStringAsync());
            var wantedTitle = Simplify(title);
            var wantedArtist = Simplify(artist);
            var all = results.Select(r => new
            {
                Title = Simplify(r.TryGetValue("trackName", out var n) ? n as string : null),
                Artist = Simplify(r.TryGetValue("artistName", out var a) ? a as string : null),
                Synced = r.TryGetValue("syncedLyrics", out var s) ? s as string : null,
                Plain = r.TryGetValue("plainLyrics", out var p) ? p as string : null,
                Instrumental = r.TryGetValue("instrumental", out var i) && i is bool flag && flag,
                Duration = r.TryGetValue("duration", out var d) && d != null ? Convert.ToDouble(d, CultureInfo.InvariantCulture) : 0
            }).ToList();
            all = all.Where(c => c.Title.Length > 0 && (c.Title.Contains(wantedTitle) || wantedTitle.Contains(c.Title))
                                 && (wantedArtist.Length == 0 || c.Artist.Contains(wantedArtist) || (c.Artist.Length > 0 && wantedArtist.Contains(c.Artist)))
                                 && (track.DurationSeconds <= 0 || c.Duration <= 0 || Math.Abs(c.Duration - track.DurationSeconds) <= (track.IsVideo ? Math.Max(45, c.Duration * 0.25) : Math.Max(20, c.Duration * 0.1)))).ToList();
            var candidates = all.Where(c => !string.IsNullOrWhiteSpace(c.Synced) || !string.IsNullOrWhiteSpace(c.Plain)).ToList();
            if (candidates.Count == 0) return all.Any(c => c.Instrumental) ? InstrumentalMarker : null;
            var best = candidates
                .OrderBy(c => string.IsNullOrWhiteSpace(c.Synced) ? 1 : 0)
                .ThenBy(c => track.DurationSeconds > 0 ? Math.Abs(c.Duration - track.DurationSeconds) : 0)
                .First();
            return !string.IsNullOrWhiteSpace(best.Synced) ? best.Synced : best.Plain;
        }

        static string Simplify(string text) => new string((text ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        static string SafeName(string key)
        {
            var name = new string(key.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
            return name.Length > 120 ? name.Substring(0, 120) : name;
        }

        static readonly Regex LineTag = new Regex(@"\[(\d+):(\d+(?:[.:]\d+)?)\]");
        static readonly Regex WordTag = new Regex(@"<(\d+):(\d+(?:[.:]\d+)?)>");

        static double Seconds(Match m) => int.Parse(m.Groups[1].Value) * 60 + double.Parse(m.Groups[2].Value.Replace(':', '.'), CultureInfo.InvariantCulture);

        public static LyricsResult Parse(string text, double duration)
        {
            var result = new LyricsResult();
            var timed = new List<Tuple<double, string>>();
            var plain = new List<string>();
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var tags = LineTag.Matches(raw);
                if (tags.Count == 0)
                {
                    if (!string.IsNullOrWhiteSpace(raw) && !Regex.IsMatch(raw, @"^\[[a-z]+:", RegexOptions.IgnoreCase)) plain.Add(raw.Trim());
                    continue;
                }
                var body = LineTag.Replace(raw, "");
                foreach (Match tag in tags) timed.Add(Tuple.Create(Seconds(tag), body));
            }

            if (timed.Count > 0)
            {
                result.Kind = LyricsKind.Synced;
                timed = timed.OrderBy(t => t.Item1).ToList();
                if (timed[0].Item1 > 4) result.Lines.Add(new LyricLine { Start = 0, End = timed[0].Item1, IsGap = true });
                for (int i = 0; i < timed.Count; i++)
                {
                    var start = timed[i].Item1;
                    var end = i + 1 < timed.Count ? timed[i + 1].Item1 : (duration > start ? duration : start + 6);
                    var body = timed[i].Item2;
                    if (string.IsNullOrWhiteSpace(WordTag.Replace(body, "")))
                    {
                        if (end - start > 3) result.Lines.Add(new LyricLine { Start = start, End = end, IsGap = true });
                        continue;
                    }
                    var line = BuildLine(body, start, end);
                    result.Lines.Add(line);
                    var sungUntil = line.Words.Count > 0 ? line.Words.Last().End : start;
                    var nextIsLyric = i + 1 < timed.Count && !string.IsNullOrWhiteSpace(WordTag.Replace(timed[i + 1].Item2, ""));
                    if (nextIsLyric && end - sungUntil >= MinimumAutoGapSeconds)
                        result.Lines.Add(new LyricLine { Start = sungUntil + 0.6, End = end, IsGap = true });
                }
                return result;
            }

            if (plain.Count > 0)
            {
                result.Kind = LyricsKind.Plain;
                var span = duration > 30 ? duration - 10 : plain.Count * 4.0;
                var step = span / plain.Count;
                for (int i = 0; i < plain.Count; i++) result.Lines.Add(BuildLine(plain[i], 5 + i * step, 5 + (i + 1) * step));
            }
            return result;
        }

        public static LyricLine BuildLine(string body, double start, double end)
        {
            var line = new LyricLine { Start = start, End = end };
            var wordTags = WordTag.Matches(body);
            if (wordTags.Count > 0)
            {
                var pieces = WordTag.Split(body);
                var times = wordTags.Cast<Match>().Select(Seconds).ToList();
                for (int i = 0; i < times.Count; i++)
                {
                    var piece = pieces.Length > 1 + i * 3 + 2 ? pieces[1 + i * 3 + 2] : "";
                    if (string.IsNullOrWhiteSpace(piece)) continue;
                    var wordEnd = i + 1 < times.Count ? times[i + 1] : end;
                    line.Words.Add(new LyricWord { Text = piece.Trim() + " ", Start = times[i], End = wordEnd });
                }
                if (line.Words.Count > 0) return line;
            }

            var tokens = Tokenize(body.Trim());
            var sung = Math.Min((end - start) * 0.92, 0.3 * body.Length + 1.2);
            var weights = tokens.Select(t => 1.0 + t.Trim().Length).ToList();
            var total = weights.Sum();
            var cursor = start;
            for (int i = 0; i < tokens.Count; i++)
            {
                var length = sung * weights[i] / total;
                line.Words.Add(new LyricWord { Text = tokens[i], Start = cursor, End = cursor + length });
                cursor += length;
            }
            return line;
        }

        static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();
            foreach (var ch in text)
            {
                if (IsCjk(ch))
                {
                    if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
                    tokens.Add(ch.ToString());
                }
                else if (ch == ' ')
                {
                    current.Append(' ');
                    tokens.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(ch);
            }
            if (current.Length > 0) tokens.Add(current.ToString());
            return tokens.Where(t => t.Length > 0).ToList();
        }

        static bool IsCjk(char c) => (c >= 0x3040 && c <= 0x30FF) || (c >= 0x3400 && c <= 0x9FFF) || (c >= 0xAC00 && c <= 0xD7AF) || (c >= 0xFF00 && c <= 0xFFEF);
    }

    static class Log
    {
        public static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Stagehand");

        public static void Write(string message)
        {
            Directory.CreateDirectory(DataDir);
            File.AppendAllText(Path.Combine(DataDir, "log.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + message + Environment.NewLine);
        }
    }
}
