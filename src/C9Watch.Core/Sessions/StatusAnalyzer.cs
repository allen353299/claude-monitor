using System.Globalization;
using C9Watch.Core.Models;

namespace C9Watch.Core.Sessions;

public static class StatusAnalyzer
{
    public static SessionStatus DetermineStatus(
        IReadOnlyList<SessionEntry> entries,
        PermissionChecker? checker = null,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        checker ??= PermissionChecker.Default;
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

        var hasTrailingProgress = false;
        for (var i = lastMeaningfulIdx + 1; i < entries.Count; i++)
        {
            if (entries[i] is UnknownEntry)
            {
                hasTrailingProgress = true;
                break;
            }
        }

        var last = entries[lastMeaningfulIdx];

        if (last is UserEntry ue)
        {
            if (ue.Message.IsToolResult)
            {
                return IsEntryRecent(ue.Base.Timestamp, 30, nowValue)
                    ? SessionStatus.Working
                    : SessionStatus.WaitingForInput;
            }
            return IsEntryRecent(ue.Base.Timestamp, 30, nowValue)
                ? SessionStatus.Working
                : SessionStatus.WaitingForInput;
        }

        if (last is AssistantEntry ae)
        {
            var msg = ae.Message;
            var rawStatus = AnalyzeAssistantMessage(msg, checker);

            if (HasPendingAskUserQuestion(msg.Content))
            {
                return SessionStatus.NeedsAttention;
            }

            if (IsAssistantAskingQuestion(msg) && !IsEntryRecent(ae.Base.Timestamp, 20, nowValue))
            {
                return SessionStatus.NeedsAttention;
            }

            return rawStatus switch
            {
                SessionStatus.Working => ResolveWorking(msg, ae.Base.Timestamp, hasTrailingProgress, nowValue),
                SessionStatus.NeedsAttention => SessionStatus.NeedsAttention,
                _ => rawStatus,
            };
        }

        return SessionStatus.WaitingForInput;
    }

    public static string? GetPendingToolName(
        IReadOnlyList<SessionEntry> entries,
        PermissionChecker? checker = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        checker ??= PermissionChecker.Default;

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

            if (tu.Name == "AskUserQuestion")
            {
                return "AskUserQuestion";
            }

            if (!checker.IsAutoApproved(tu.Name, tu.Input))
            {
                return tu.Name;
            }
        }

        if (IsAssistantAskingQuestion(lastAssistant))
        {
            return "Question";
        }

        return null;
    }

    private static SessionStatus ResolveWorking(
        AssistantMessage msg,
        string timestamp,
        bool hasTrailingProgress,
        DateTimeOffset now)
    {
        var hasPending = HasPendingToolUses(msg.Content);
        if (hasPending)
        {
            _ = hasTrailingProgress;
            return SessionStatus.Working;
        }

        return IsEntryRecent(timestamp, 20, now)
            ? SessionStatus.Working
            : SessionStatus.WaitingForInput;
    }

    private static SessionStatus AnalyzeAssistantMessage(AssistantMessage msg, PermissionChecker checker)
    {
        var hasToolUse = msg.Content.Any(c => c is ToolUseContent);

        if (hasToolUse)
        {
            var allCompleted = CheckAllToolsCompleted(msg.Content);
            if (allCompleted)
            {
                return msg.StopReason switch
                {
                    "end_turn" => SessionStatus.WaitingForInput,
                    "tool_use" => SessionStatus.Working,
                    "max_tokens" or "stop_sequence" => SessionStatus.WaitingForInput,
                    _ => SessionStatus.WaitingForInput,
                };
            }

            return ArePendingToolsAutoApproved(msg.Content, checker)
                ? SessionStatus.Working
                : SessionStatus.NeedsAttention;
        }

        return msg.StopReason switch
        {
            "end_turn" => SessionStatus.WaitingForInput,
            "max_tokens" or "stop_sequence" => SessionStatus.WaitingForInput,
            null => SessionStatus.Working,
            _ => SessionStatus.WaitingForInput,
        };
    }

    private static bool CheckAllToolsCompleted(IEnumerable<MessageContent> content)
    {
        var toolUseIds = new List<string>();
        var toolResultIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in content)
        {
            if (c is ToolUseContent tu) toolUseIds.Add(tu.Id);
            else if (c is ToolResultContent tr) toolResultIds.Add(tr.ToolUseId);
        }
        if (toolUseIds.Count == 0) return true;
        foreach (var id in toolUseIds)
        {
            if (!toolResultIds.Contains(id)) return false;
        }
        return true;
    }

    private static bool HasPendingToolUses(IReadOnlyList<MessageContent> content)
        => !CheckAllToolsCompleted(content);

    private static bool ArePendingToolsAutoApproved(IReadOnlyList<MessageContent> content, PermissionChecker checker)
    {
        var completedIds = CollectCompletedIds(content);
        foreach (var c in content)
        {
            if (c is not ToolUseContent tu) continue;
            if (completedIds.Contains(tu.Id)) continue;
            if (!checker.IsAutoApproved(tu.Name, tu.Input)) return false;
        }
        return true;
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
        if (string.IsNullOrEmpty(rfc3339Timestamp)) return false;

        if (!DateTimeOffset.TryParse(
                rfc3339Timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var entryTime))
        {
            return false;
        }

        return (now - entryTime).TotalSeconds < seconds;
    }
}
