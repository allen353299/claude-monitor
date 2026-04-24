namespace ClaudeMonitor.Core.Platform;

public interface INotificationService
{
    void NotifyAttention(SessionId sessionId, string title, string body);
}
