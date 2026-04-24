using System.Text.RegularExpressions;
using ClaudeMonitor.Core.Models;

namespace ClaudeMonitor.Core.Sessions;

public static class SessionEnricher
{
    public sealed record EnrichedData(
        string? FirstPrompt,
        string? LatestMessage,
        string? Recap,
        int MessageCount);

    private const int FirstPromptMaxLen = 100;
    private const int LatestMessageMaxLen = 200;

    private static readonly Regex CommandTag = new(
        "<command-(?:name|message|args)>.*?</command-(?:name|message|args)>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static EnrichedData Enrich(IReadOnlyList<SessionEntry> entries)
    {
        return new EnrichedData(
            FirstPrompt: ExtractFirstPrompt(entries),
            LatestMessage: ExtractLatestMessage(entries),
            Recap: ExtractRecap(entries),
            MessageCount: CountMessages(entries));
    }

    private static string? ExtractRecap(IReadOnlyList<SessionEntry> entries)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] is SystemEntry se
                && se.Subtype == "away_summary"
                && !string.IsNullOrWhiteSpace(se.Content))
            {
                return Truncate(se.Content, LatestMessageMaxLen);
            }
        }
        return null;
    }

    private static string? ExtractFirstPrompt(IReadOnlyList<SessionEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry is UserEntry u && !u.Message.IsToolResult)
            {
                var cleaned = CleanCommandTags(u.Message.Content).Trim();
                if (cleaned.Length > 0)
                {
                    return Truncate(cleaned, FirstPromptMaxLen);
                }
            }
        }
        return null;
    }

    private static string? ExtractLatestMessage(IReadOnlyList<SessionEntry> entries)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            switch (entries[i])
            {
                case AssistantEntry a:
                    {
                        var text = FindLast<TextContent>(a.Message.Content)?.Text;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return Truncate(text.Trim(), LatestMessageMaxLen);
                        }

                        var toolName = FindLast<ToolUseContent>(a.Message.Content)?.Name;
                        if (!string.IsNullOrEmpty(toolName))
                        {
                            return Truncate($"Executing {toolName}...", LatestMessageMaxLen);
                        }

                        var thinking = FindLast<ThinkingContent>(a.Message.Content)?.Thinking;
                        if (!string.IsNullOrWhiteSpace(thinking))
                        {
                            return Truncate(thinking.Trim(), LatestMessageMaxLen);
                        }
                        break;
                    }

                case UserEntry u when !u.Message.IsToolResult:
                    {
                        var cleaned = CleanCommandTags(u.Message.Content).Trim();
                        if (cleaned.Length > 0)
                        {
                            return Truncate(cleaned, LatestMessageMaxLen);
                        }
                        break;
                    }
            }
        }
        return null;
    }

    private static int CountMessages(IReadOnlyList<SessionEntry> entries)
    {
        int count = 0;
        foreach (var entry in entries)
        {
            if (entry is UserEntry or AssistantEntry) count++;
        }
        return count;
    }

    private static T? FindLast<T>(IReadOnlyList<MessageContent> content) where T : MessageContent
    {
        for (int i = content.Count - 1; i >= 0; i--)
        {
            if (content[i] is T match) return match;
        }
        return null;
    }

    private static string CleanCommandTags(string input)
    {
        return string.IsNullOrEmpty(input) ? input : CommandTag.Replace(input, "");
    }

    private static string Truncate(string s, int max)
    {
        var flat = CollapseWhitespace(s);
        if (flat.Length <= max) return flat;
        return flat.AsSpan(0, max).ToString() + "…";
    }

    private static string CollapseWhitespace(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        bool prevSpace = false;
        foreach (var ch in s)
        {
            if (ch is '\n' or '\r' or '\t' or ' ')
            {
                if (!prevSpace) sb.Append(' ');
                prevSpace = true;
            }
            else
            {
                sb.Append(ch);
                prevSpace = false;
            }
        }
        return sb.ToString().Trim();
    }
}
