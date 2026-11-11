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
        public bool WordTimed;
        public string Singer;
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
        static readonly string CacheDir = Path.Combine(Log.DataDir, "lyrics-cache");
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

            var exact = await LookupExactAsync(track);
            var synced = new List<string>();
            string fallback = null;
            if (exact != null && !string.IsNullOrWhiteSpace(exact.Synced)) synced.Add(exact.Synced);
            else if (exact != null) fallback = !string.IsNullOrWhiteSpace(exact.Plain) ? exact.Plain : exact.Instrumental ? InstrumentalMarker : null;
            foreach (var query in Queries(track))
            {
                var found = await LookupAsync(query.Item1, query.Item2, track);
                synced.AddRange(found.Synced.Where(version => !synced.Contains(version)));
                fallback = fallback ?? found.Fallback;
                if (synced.Count > 0) break;
            }
            var text = synced.Count > 0 ? Consensus(synced) : fallback;
            if (text == null) return new LyricsResult();
            Directory.CreateDirectory(CacheDir);
            File.WriteAllText(cacheFile, text, Encoding.UTF8);
            return FromText(text, track.DurationSeconds);
        }

        static string Consensus(List<string> versions)
        {
            if (versions.Count <= 2) return versions[0];
            var timings = versions.Select(version => LineTag.Matches(version).Cast<Match>().Select(Seconds).OrderBy(seconds => seconds).Take(12).ToArray()).ToList();
            double Distance(double[] a, double[] b)
            {
                var count = Math.Min(a.Length, b.Length);
                return count == 0 ? double.MaxValue : Enumerable.Range(0, count).Average(i => Math.Abs(a[i] - b[i]));
            }
            double Median(List<double> values) => values.OrderBy(value => value).ElementAt(values.Count / 2);
            return versions[Enumerable.Range(0, versions.Count).OrderBy(i => Median(timings.Where((other, j) => j != i).Select(other => Distance(timings[i], other)).ToList())).First()];
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

        sealed class Candidate
        {
            public string Title, Artist, Synced, Plain;
            public bool Instrumental;
            public double Duration;

            public static Candidate From(Dictionary<string, object> record) => new Candidate
            {
                Title = Simplify(record.TryGetValue("trackName", out var n) ? n as string : null),
                Artist = Simplify(record.TryGetValue("artistName", out var a) ? a as string : null),
                Synced = record.TryGetValue("syncedLyrics", out var s) ? s as string : null,
                Plain = record.TryGetValue("plainLyrics", out var p) ? p as string : null,
                Instrumental = record.TryGetValue("instrumental", out var i) && i is bool flag && flag,
                Duration = record.TryGetValue("duration", out var d) && d != null ? Convert.ToDouble(d, CultureInfo.InvariantCulture) : 0
            };

            public bool HasLyrics => !string.IsNullOrWhiteSpace(Synced) || !string.IsNullOrWhiteSpace(Plain);
        }

        static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        static async Task<Candidate> LookupExactAsync(TrackInfo track)
        {
            if (track.DurationSeconds <= 0 || track.IsVideo) return null;
            var url = "https://lrclib.net/api/get?track_name=" + Uri.EscapeDataString(track.Title) + "&artist_name=" + Uri.EscapeDataString(track.Artist)
                      + "&album_name=" + Uri.EscapeDataString(track.Album) + "&duration=" + (int)Math.Round(track.DurationSeconds);
            var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            return Candidate.From(Json.Deserialize<Dictionary<string, object>>(await response.Content.ReadAsStringAsync()));
        }

        static async Task<(List<string> Synced, string Fallback)> LookupAsync(string artist, string title, TrackInfo track)
        {
            var url = "https://lrclib.net/api/search?track_name=" + Uri.EscapeDataString(title) + "&artist_name=" + Uri.EscapeDataString(artist);
            var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return (new List<string>(), null);
            var wantedTitle = Simplify(title);
            var wantedArtist = Simplify(artist);
            var known = track.DurationSeconds > 0;
            double Gap(Candidate c) => known && c.Duration > 0 ? Math.Abs(c.Duration - track.DurationSeconds) : 0;
            var all = Json.Deserialize<List<Dictionary<string, object>>>(await response.Content.ReadAsStringAsync()).Select(Candidate.From)
                .Where(c => c.Title.Length > 0 && (c.Title.Contains(wantedTitle) || wantedTitle.Contains(c.Title))
                            && (wantedArtist.Length == 0 || c.Artist.Contains(wantedArtist) || (c.Artist.Length > 0 && wantedArtist.Contains(c.Artist)))
                            && Gap(c) <= (track.IsVideo ? Math.Max(45, c.Duration * 0.25) : Math.Max(20, c.Duration * 0.1)))
                .OrderBy(Gap).ToList();
            var synced = all.Where(c => !string.IsNullOrWhiteSpace(c.Synced) && (track.IsVideo || Gap(c) <= SameVersionSeconds)).Select(c => c.Synced).ToList();
            var other = all.FirstOrDefault(c => c.HasLyrics);
            var fallback = other == null ? (all.Any(c => c.Instrumental) ? InstrumentalMarker : null) : !string.IsNullOrWhiteSpace(other.Plain) ? other.Plain : Unsynced(other.Synced);
            return (synced, fallback);
        }

        const double SameVersionSeconds = 3;

        static string Unsynced(string synced) => string.Join("\n", synced.Replace("\r", "").Split('\n').Select(line => WordTag.Replace(LineTag.Replace(line, ""), "").Trim()).Where(line => line.Length > 0));

        public static string Simplify(string text) => new string((text ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        static string SafeName(string key)
        {
            var name = new string(key.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
            return name.Length > 120 ? name.Substring(0, 120) : name;
        }

        static readonly Regex SingerMarker = new Regex(@"^\s*(v\d+|[A-ZÀ-ɏ][\w .&'’-]{0,23}):\s+");
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

            var labels = timed.Select(t => t.Item2).Concat(plain).Select(body => SingerMarker.Match(body)).Where(m => m.Success).GroupBy(m => m.Groups[1].Value)
                .Where(group => group.Count() >= 2 || Regex.IsMatch(group.Key, @"^v\d+$")).Select(group => group.Key).ToList();
            string singer = null;
            string Voice(ref string body)
            {
                var marker = SingerMarker.Match(body);
                if (marker.Success && labels.Contains(marker.Groups[1].Value))
                {
                    singer = marker.Groups[1].Value;
                    body = body.Substring(marker.Length);
                }
                return singer;
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
                    var voice = Voice(ref body);
                    var line = BuildLine(body, start, end);
                    line.Singer = voice;
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
                for (int i = 0; i < plain.Count; i++)
                {
                    var body = plain[i];
                    var voice = Voice(ref body);
                    var line = BuildLine(body, 5 + i * step, 5 + (i + 1) * step);
                    line.Singer = voice;
                    result.Lines.Add(line);
                }
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
                if (line.Words.Count > 0)
                {
                    line.WordTimed = true;
                    return line;
                }
            }

            var tokens = Tokenize(body.Trim());
            var weights = tokens.Select(token => (double)Syllables(token)).ToList();
            var sung = Math.Min((end - start) * 0.92, 0.45 * weights.Sum() + 0.8);
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

        static int Syllables(string token)
        {
            var letters = token.Where(char.IsLetter).ToList();
            if (letters.Count == 0) return 1;
            if (letters.All(c => c < 0x0250))
            {
                var groups = 0;
                var inVowel = false;
                foreach (var c in letters)
                {
                    var vowel = "aeiouyàáâäèéêëìíîïòóôöùúûü".IndexOf(char.ToLowerInvariant(c)) >= 0;
                    if (vowel && !inVowel) groups++;
                    inVowel = vowel;
                }
                return Math.Max(1, groups);
            }
            return letters.All(c => c >= 0x3000) ? letters.Count : Math.Max(1, (letters.Count + 1) / 2);
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
