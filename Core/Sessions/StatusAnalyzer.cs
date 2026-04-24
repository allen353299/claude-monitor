using System.Globalization;
using ClaudeMonitor.Core.Models;

namespace ClaudeMonitor.Core.Sessions;

public static class StatusAnalyzer
{
    private const int PermissionStaleSeconds = 15;
    private const int UserRecencySeconds = 30;
    private const int AssistantRecencySeconds = 20;

    public static SessionStatus DetermineStatus(
        IReadOnlyList<SessionEntry> entries,
        DateTimeOffset? fileMtime = null,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var nowValue = now ?? DateTimeOffset.UtcNow;

        if (entries.Count == 0) return SessionStatus.Connecting;

        int lastMeaningfulIdx = -1;
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] is UserEntry or AssistantEntry)
            {
                lastMeaningfulIdx = i;
                break;
            }
        }

        if (lastMeaningfulIdx < 0) return SessionStatus.Connecting;

        var last = entries[lastMeaningfulIdx];

        if (last is UserEntry ue)
        {
            if (ue.Message.IsToolResult)
            {
                return IsEntryRecent(ue.Base.Timestamp, UserRecencySeconds, nowValue)
                    ? SessionStatus.Working
                    : SessionStatus.WaitingForInput;
            }

            if (fileMtime.HasValue
                && (nowValue - fileMtime.Value).TotalSeconds > PermissionStaleSeconds)
            {
                return SessionStatus.NeedsAttention;
            }

            return IsEntryRecent(ue.Base.Timestamp, UserRecencySeconds, nowValue)
                ? SessionStatus.Working
                : SessionStatus.WaitingForInput;
        }

        var ae = (AssistantEntry)last;
        var msg = ae.Message;

        if (HasPendingAskUserQuestion(msg.Content))
        {
            return SessionStatus.NeedsAttention;
        }

        if (IsAssistantAskingQuestion(msg) && !IsEntryRecent(ae.Base.Timestamp, AssistantRecencySeconds, nowValue))
        {
            return SessionStatus.NeedsAttention;
        }

        if (HasPendingToolUses(msg.Content))
        {
            return SessionStatus.Working;
        }

        return msg.StopReason switch
        {
            "end_turn" => SessionStatus.WaitingForInput,
            "max_tokens" or "stop_sequence" => SessionStatus.WaitingForInput,
            "tool_use" => SessionStatus.Working,
            null => IsEntryRecent(ae.Base.Timestamp, AssistantRecencySeconds, nowValue)
                ? SessionStatus.Working
                : SessionStatus.WaitingForInput,
            _ => SessionStatus.WaitingForInput,
        };
    }

    public static string? GetPendingToolName(IReadOnlyList<SessionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        AssistantMessage? lastAssistant = null;
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] is AssistantEntry ae)
            {
                lastAssistant = ae.Message;
                break;
            }
        }

        if (lastAssistant == null) return null;

        var completedIds = CollectCompletedIds(lastAssistant.Content);

        foreach (var item in lastAssistant.Content)
        {
            if (item is not ToolUseContent tu) continue;
            if (completedIds.Contains(tu.Id)) continue;
            return tu.Name;
        }

        if (IsAssistantAskingQuestion(lastAssistant))
        {
            return "Question";
        }

        return null;
    }

    private static bool HasPendingToolUses(IReadOnlyList<MessageContent> content)
    {
        var toolUseIds = new List<string>();
        var toolResultIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in content)
        {
            if (c is ToolUseContent tu) toolUseIds.Add(tu.Id);
            else if (c is ToolResultContent tr) toolResultIds.Add(tr.ToolUseId);
        }
        if (toolUseIds.Count == 0) return false;
        foreach (var id in toolUseIds)
        {
            if (!toolResultIds.Contains(id)) return true;
        }
        return false;
    }

    private static bool HasPendingAskUserQuestion(IReadOnlyList<MessageContent> content)
    {
        var completedIds = CollectCompletedIds(content);
        foreach (var c in content)
        {
            if (c is ToolUseContent tu && tu.Name == "AskUserQuestion" && !completedIds.Contains(tu.Id))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsAssistantAskingQuestion(AssistantMessage msg)
    {
        if (HasPendingAskUserQuestion(msg.Content)) return true;

        if (msg.StopReason == "end_turn")
        {
            string? lastText = null;
            for (var i = msg.Content.Count - 1; i >= 0; i--)
            {
                if (msg.Content[i] is TextContent t)
                {
                    lastText = t.Text;
                    break;
                }
            }

            if (lastText != null)
            {
                var trimmed = lastText.Trim();
                if (trimmed.EndsWith('?') || trimmed.EndsWith("?)", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static HashSet<string> CollectCompletedIds(IEnumerable<MessageContent> content)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in content)
        {
            if (c is ToolResultContent tr) ids.Add(tr.ToolUseId);
        }
        return ids;
    }

    private static bool IsEntryRecent(string rfc3339Timestamp, int seconds, DateTimeOffset now)
    {
        if (!TryParseTimestamp(rfc3339Timestamp, out var entryTime)) return false;
        return (now - entryTime).TotalSeconds < seconds;
    }

    private static bool TryParseTimestamp(string rfc3339Timestamp, out DateTimeOffset value)
    {
        value = default;
        if (string.IsNullOrEmpty(rfc3339Timestamp)) return false;
        return DateTimeOffset.TryParse(
            rfc3339Timestamp,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out value);
    }
}
