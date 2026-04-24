namespace ClaudeMonitor.Core.Platform;

public static class SessionPidResolver
{
    public static int? ResolvePid(string sessionId, string? claudeHome = null)
    {
        if (string.IsNullOrEmpty(sessionId)) return null;
        var snapshot = LiveSessionIndex.Snapshot(claudeHome);
        return snapshot.TryGetValue(sessionId, out var pid) ? pid : null;
    }
}
