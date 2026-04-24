using ClaudeMonitor.Core.Models;

namespace ClaudeMonitor.Core.Sessions;

public interface IStatusAnalyzer
{
    SessionStatus DetermineStatus(
        IReadOnlyList<SessionEntry> entries,
        DateTimeOffset? fileMtime = null,
        DateTimeOffset? now = null);

    string? GetPendingToolName(IReadOnlyList<SessionEntry> entries);
}
