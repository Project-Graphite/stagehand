using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Windows.Globalization;

namespace Stagehand
{
    static class Romanizer
    {
        static readonly Dictionary<string, string> Kana = BuildKana();

        static Dictionary<string, string> BuildKana()
        {
            var table = new Dictionary<string, string>();
            void Row(string kana, params string[] romaji) { for (int i = 0; i < kana.Length; i++) table[kana[i].ToString()] = romaji[i]; }
            Row("あいうえお", "a", "i", "u", "e", "o");
            Row("かきくけこ", "ka", "ki", "ku", "ke", "ko");
            Row("がぎぐげご", "ga", "gi", "gu", "ge", "go");
            Row("さしすせそ", "sa", "shi", "su", "se", "so");
            Row("ざじずぜぞ", "za", "ji", "zu", "ze", "zo");
            Row("たちつてと", "ta", "chi", "tsu", "te", "to");
            Row("だぢづでど", "da", "ji", "zu", "de", "do");
            Row("なにぬねの", "na", "ni", "nu", "ne", "no");
            Row("はひふへほ", "ha", "hi", "fu", "he", "ho");
            Row("ばびぶべぼ", "ba", "bi", "bu", "be", "bo");
            Row("ぱぴぷぺぽ", "pa", "pi", "pu", "pe", "po");
            Row("まみむめも", "ma", "mi", "mu", "me", "mo");
            Row("やゆよ", "ya", "yu", "yo");
            Row("らりるれろ", "ra", "ri", "ru", "re", "ro");
            Row("わゐゑをん", "wa", "i", "e", "o", "n");
            Row("ぁぃぅぇぉゔ", "a", "i", "u", "e", "o", "vu");
            foreach (var pair in new[] { "き:k", "ぎ:g", "し:sh", "じ:j", "ち:ch", "ぢ:j", "に:n", "ひ:h", "び:b", "ぴ:p", "み:m", "り:r" })
            {
                var parts = pair.Split(':');
                var stem = parts[1];
                var palatal = stem.EndsWith("h") || stem == "j" ? stem : stem + "y";
                table[parts[0] + "ゃ"] = palatal + "a";
                table[parts[0] + "ゅ"] = palatal + "u";
                table[parts[0] + "ょ"] = palatal + "o";
            }
            table["ふぁ"] = "fa"; table["ふぃ"] = "fi"; table["ふぇ"] = "fe"; table["ふぉ"] = "fo";
            table["てぃ"] = "ti"; table["でぃ"] = "di"; table["うぃ"] = "wi"; table["うぇ"] = "we"; table["しぇ"] = "she"; table["じぇ"] = "je"; table["ちぇ"] = "che";
            return table;
        }

        public static bool IsKana(char c) => (c >= 0x3041 && c <= 0x3096) || (c >= 0x30A1 && c <= 0x30FA) || c == 0x30FC;
        public static bool IsHangul(char c) => c >= 0xAC00 && c <= 0xD7A3;
        public static bool IsHan(char c) => c >= 0x4E00 && c <= 0x9FFF;

        public static string Romanize(string text, bool japaneseSong)
        {
            if (text.Any(IsHangul)) return Korean(text);
            if (japaneseSong && text.Any(c => IsKana(c) || IsHan(c))) return Japanese(text);
            return null;
        }

        static string Japanese(string text)
        {
            var phonemes = JapanesePhoneticAnalyzer.GetWords(text);
            if (phonemes.Count == 0) return FromDictionary(text);
            var output = new StringBuilder();
            foreach (var phoneme in phonemes)
            {
                var reading = phoneme.YomiText;
                if (string.IsNullOrEmpty(reading)) reading = phoneme.DisplayText;
                var romaji = KanaToRomaji(reading);
                if (output.Length > 0 && phoneme.IsPhraseStart && !char.IsWhiteSpace(output[output.Length - 1]) && romaji.Length > 0 && char.IsLetterOrDigit(romaji[0])) output.Append(' ');
                output.Append(romaji);
            }
            return Tidy(output.ToString());
        }

        sealed class KanjiReadings
        {
            public readonly Dictionary<string, string> Words = new Dictionary<string, string>();
            public readonly Dictionary<string, string> VerbStems = new Dictionary<string, string>();
            public readonly Dictionary<string, string> AdjectiveStems = new Dictionary<string, string>();
            public int Longest;
        }

        static readonly Lazy<KanjiReadings> Readings = new Lazy<KanjiReadings>(() =>
        {
            var readings = new KanjiReadings();
            using (var gzip = new GZipStream(typeof(Romanizer).Assembly.GetManifestResourceStream("kanji-readings.gz"), CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var tab = line.IndexOf('\t');
                    var table = line[0] == '~' ? readings.VerbStems : line[0] == '^' ? readings.AdjectiveStems : readings.Words;
                    var key = table == readings.Words ? line.Substring(0, tab) : line.Substring(1, tab - 1);
                    table[key] = line.Substring(tab + 1);
                    readings.Longest = Math.Max(readings.Longest, key.Length);
                }
            }
            return readings;
        });

        static string FromDictionary(string text)
        {
            var kana = new StringBuilder();
            var i = 0;
            var afterWord = false;
            while (i < text.Length)
            {
                if (!IsHan(text[i]))
                {
                    var c = text[i++];
                    var particle = afterWord && (i == text.Length || !IsHiragana(text[i]));
                    kana.Append(particle && c == 'は' ? 'わ' : particle && c == 'へ' ? 'え' : c);
                    afterWord = false;
                    continue;
                }
                var word = Longest(Readings.Value.Words, text, i, end => true);
                var verb = Longest(Readings.Value.VerbStems, text, i, end => end < text.Length && IsHiragana(text[end]) && !(word.Length > 0 && "はへをのがもとで".IndexOf(text[end]) >= 0));
                var adjective = Longest(Readings.Value.AdjectiveStems, text, i, end => end < text.Length && "くかけさそみ".IndexOf(text[end]) >= 0);
                var stem = adjective.Length >= verb.Length ? adjective : verb;
                if (stem.Length > 0 && stem.Length >= word.Length)
                {
                    kana.Append(' ').Append(stem.Reading);
                    i += stem.Length;
                    afterWord = false;
                }
                else if (word.Length > 0)
                {
                    kana.Append(' ').Append(word.Reading).Append(' ');
                    i += word.Length;
                    afterWord = true;
                }
                else
                {
                    kana.Append(text[i++]);
                    afterWord = false;
                }
            }
            return Tidy(KanaToRomaji(kana.ToString()));
        }

        static (string Reading, int Length) Longest(Dictionary<string, string> table, string text, int start, Func<int, bool> fits)
        {
            for (var length = Math.Min(Readings.Value.Longest, text.Length - start); length > 0; length--)
                if (fits(start + length) && table.TryGetValue(text.Substring(start, length), out var reading)) return (reading, length);
            return (null, 0);
        }

        static bool IsHiragana(char c) => c >= 0x3041 && c <= 0x3096;

        static string KanaToRomaji(string kana)
        {
            var hira = new string(kana.Select(c => c >= 0x30A1 && c <= 0x30F6 ? (char)(c - 0x60) : c).ToArray());
            var output = new StringBuilder();
            var doubleNext = false;
            for (int i = 0; i < hira.Length; i++)
            {
                var c = hira[i];
                if (c == 'っ') { doubleNext = true; continue; }
                if (c == 'ー')
                {
                    if (output.Length > 0) output.Append(output[output.Length - 1]);
                    continue;
                }
                string syllable = null;
                if (i + 1 < hira.Length && Kana.TryGetValue(hira.Substring(i, 2), out var pair)) { syllable = pair; i++; }
                else if (Kana.TryGetValue(c.ToString(), out var single)) syllable = single;
                if (syllable == null)
                {
                    output.Append(c == '　' ? ' ' : c == '、' ? ',' : c == '。' ? '.' : c == '！' ? '!' : c == '？' ? '?' : c);
                    doubleNext = false;
                    continue;
                }
                if (doubleNext) output.Append(syllable.StartsWith("ch") ? 't' : syllable[0]);
                doubleNext = false;
                if (syllable == "n" && i + 1 < hira.Length && "あいうえおやゆよ".IndexOf(hira[i + 1]) >= 0) syllable = "n'";
                output.Append(syllable);
            }
            return output.ToString();
        }

        static readonly string[] Initials = { "g", "kk", "n", "d", "tt", "r", "m", "b", "pp", "s", "ss", "", "j", "jj", "ch", "k", "t", "p", "h" };
        static readonly string[] Vowels = { "a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", "wa", "wae", "oe", "yo", "u", "wo", "we", "wi", "yu", "eu", "ui", "i" };
        static readonly string[] Finals = { "", "k", "k", "k", "n", "n", "n", "t", "l", "k", "m", "l", "l", "l", "p", "l", "m", "p", "p", "t", "t", "ng", "t", "t", "k", "t", "p", "t" };

        static string Korean(string text)
        {
            var output = new StringBuilder();
            foreach (var c in text)
            {
                if (!IsHangul(c)) { output.Append(c); continue; }
                var index = c - 0xAC00;
                output.Append(Initials[index / 588]).Append(Vowels[index % 588 / 28]).Append(Finals[index % 28]);
            }
            return Tidy(output.ToString());
        }

        static string Tidy(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
    }
}
