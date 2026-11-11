using System.Collections.Generic;
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

        public static bool HasJapaneseReadings => JapanesePhoneticAnalyzer.GetWords("日本").Count > 0;

        static string Japanese(string text)
        {
            var phonemes = JapanesePhoneticAnalyzer.GetWords(text);
            if (phonemes.Count == 0) return KanaOnly(text);
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

        static string KanaOnly(string text)
        {
            var output = new StringBuilder();
            var run = new StringBuilder();
            bool? runIsHan = null;
            foreach (var c in text + " ")
            {
                bool? isHan = IsHan(c) ? true : IsKana(c) ? false : (bool?)null;
                if (isHan != runIsHan && run.Length > 0)
                {
                    if (output.Length > 0 && !char.IsWhiteSpace(output[output.Length - 1])) output.Append(' ');
                    output.Append(runIsHan == false ? KanaToRomaji(run.ToString()) : run.ToString());
                    run.Clear();
                }
                runIsHan = isHan;
                run.Append(c);
            }
            return Tidy(output.ToString());
        }

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
