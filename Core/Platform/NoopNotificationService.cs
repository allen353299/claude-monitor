namespace ClaudeMonitor.Core.Platform;

public sealed class NoopNotificationService : INotificationService
{
    public void NotifyAttention(SessionId sessionId, string title, string body) { }
}
