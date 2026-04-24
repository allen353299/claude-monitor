namespace ClaudeMonitor.Core.Platform;

public static class SessionPidResolver
{
    public static int? ResolvePid(SessionId sessionId, string? claudeHome = null)
    {
        if (string.IsNullOrEmpty(sessionId.Value)) return null;
        var snapshot = LiveSessionIndex.Snapshot(claudeHome);
        return snapshot.TryGetValue(sessionId, out var pid) ? pid : null;
    }
}
