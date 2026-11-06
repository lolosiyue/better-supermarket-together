using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BetterSupermarketTogether;

// Pure parsing/formatting rules for talking to the companion through the native game chat.
internal static class CompanionChatPolicy
{
    internal const int ChunkLength = 200;
    internal const int MaxChunks = 8;
    internal static readonly string[] DefaultAliases = { "DeepSeek娘", "DeepSeek 娘", "DeepSeek", "DS", "AI" };

    // A message is for the companion when it starts with @ (or full-width ＠), unless the
    // first word after @ is another player's name. A leading companion alias is stripped.
    internal static bool TryParse(string message, IEnumerable<string> aliases, IEnumerable<string> playerNames, out string prompt)
    {
        prompt = null;
        string text = (message ?? "").Trim();
        if (text.Length < 2 || (text[0] != '@' && text[0] != '＠')) return false;
        text = text.Substring(1).TrimStart();
        string alias = (aliases ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
            .OrderByDescending(x => x.Length).FirstOrDefault(x => StartsWithWord(text, x));
        if (alias != null) text = text.Substring(alias.Length);
        else if ((playerNames ?? Enumerable.Empty<string>()).Any(x => !string.IsNullOrWhiteSpace(x) && StartsWithWord(text, x.Trim()))) return false;
        text = text.TrimStart(' ', '\t', ':', '：', ',', '，', '、').Trim();
        if (text.Length == 0) return false;
        prompt = text;
        return true;
    }

    private static bool StartsWithWord(string text, string word)
    {
        if (!text.StartsWith(word, StringComparison.OrdinalIgnoreCase)) return false;
        if (text.Length == word.Length) return true;
        char next = text[word.Length];
        return char.IsWhiteSpace(next) || char.IsPunctuation(next) || next == '：' || next == '，' || next == '、';
    }

    // The native receiver drops lines with closing rich-text tags and renders TMP markup,
    // so model text is neutralised instead of trusted.
    internal static string Plain(string text) => (text ?? "").Replace('<', '＜').Replace('>', '＞').Replace("\r", "");

    internal static List<string> Chunks(string answer)
    {
        var result = new List<string>();
        foreach (string raw in Plain(answer).Split('\n'))
        {
            string line = raw.Trim();
            while (line.Length > 0)
            {
                if (result.Count == MaxChunks) { result[MaxChunks - 1] = Trim(result[MaxChunks - 1]); return result; }
                int take = Math.Min(ChunkLength, line.Length);
                if (take < line.Length && char.IsHighSurrogate(line[take - 1])) take--;
                result.Add(line.Substring(0, take));
                line = line.Substring(take).TrimStart();
            }
        }
        return result;
    }

    internal static bool IsSkip(string answer) =>
        string.IsNullOrWhiteSpace(answer) || answer.Trim().Trim('.', '。', '!', '！', '"', '「', '」').Equals("SKIP", StringComparison.OrdinalIgnoreCase);

    // Roughly a quick typist: first line is short because the API wait already felt like typing.
    internal static float TypingDelay(string line, bool first)
    {
        float seconds = .5f + (line ?? "").Length * .07f;
        seconds = seconds < .6f ? .6f : seconds > 3.5f ? 3.5f : seconds;
        return first ? seconds * .4f : seconds;
    }

    private static string Trim(string chunk) => chunk.Length > ChunkLength - 1 ? chunk.Substring(0, ChunkLength - 1) + "…" : chunk + "…";
}
