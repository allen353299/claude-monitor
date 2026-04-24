namespace ClaudeMonitor.Core.Sessions;

public sealed record HistoryEntry(
    SessionId Id,
    string ProjectPath,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    string LatestDisplay,
    int MessageCount);
