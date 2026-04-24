using ClaudeMonitor.Core.Models;

namespace ClaudeMonitor.Core.Sessions;

public sealed class SessionEnricher : ISessionEnricher
{
    public sealed record EnrichedData(
        DisplayText? FirstPrompt,
        DisplayText? LatestMessage,
        DisplayText? Recap,
        DisplayText? CustomTitle,
        int MessageCount);

    private const int FirstPromptMaxLength = 100;
    private const int LatestMessageMaxLength = 200;
    private const int RecapMaxLength = 200;
    private const int CustomTitleMaxLength = 80;

    public EnrichedData Enrich(IReadOnlyList<SessionEntry> entries)
    {
        return new EnrichedData(
            FirstPrompt: ExtractFirstPrompt(entries),
            LatestMessage: ExtractLatestMessage(entries),
            Recap: ExtractRecap(entries),
            CustomTitle: ExtractCustomTitle(entries),
            MessageCount: CountMessages(entries));
    }

    private static DisplayText? ExtractRecap(IReadOnlyList<SessionEntry> entries)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] is SystemEntry se
                && se.Subtype == "away_summary"
                && DisplayText.TryFrom(se.Content, RecapMaxLength, out var text))
            {
                return text;
            }
        }
        return null;
    }

    private static DisplayText? ExtractFirstPrompt(IReadOnlyList<SessionEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry is UserEntry u
                && !u.Message.IsToolResult
                && DisplayText.TryFrom(u.Message.Content, FirstPromptMaxLength, out var text))
            {
                return text;
            }
        }
        return null;
    }

    private static DisplayText? ExtractLatestMessage(IReadOnlyList<SessionEntry> entries)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            switch (entries[i])
            {
                case AssistantEntry a:
                    if (TryLatestFromAssistant(a.Message.Content, out var assistantText))
                        return assistantText;
                    break;

                case UserEntry u when !u.Message.IsToolResult:
                    if (DisplayText.TryFrom(u.Message.Content, LatestMessageMaxLength, out var userText))
                        return userText;
                    break;
            }
        }
        return null;
    }

    private static bool TryLatestFromAssistant(IReadOnlyList<MessageContent> content, out DisplayText result)
    {
        var textBlock = FindLast<TextContent>(content)?.Text;
        if (DisplayText.TryFrom(textBlock, LatestMessageMaxLength, out result)) return true;

        var toolName = FindLast<ToolUseContent>(content)?.Name;
        if (!string.IsNullOrEmpty(toolName)
            && DisplayText.TryFrom($"Executing {toolName}...", LatestMessageMaxLength, out result))
        {
            return true;
        }

        var thinking = FindLast<ThinkingContent>(content)?.Thinking;
        return DisplayText.TryFrom(thinking, LatestMessageMaxLength, out result);
    }

    private static DisplayText? ExtractCustomTitle(IReadOnlyList<SessionEntry> entries)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] is CustomTitleEntry ct
                && DisplayText.TryFrom(ct.CustomTitle, CustomTitleMaxLength, out var text))
            {
                return text;
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
}
