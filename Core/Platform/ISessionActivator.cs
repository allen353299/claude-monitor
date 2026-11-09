namespace ClaudeMonitor.Core.Platform;

public interface ISessionActivator
{
    Task<bool> ActivateAsync(SessionId sessionId, string? titleHint = null, CancellationToken ct = default);
}

public sealed class NoopSessionActivator : ISessionActivator
{
    public Task<bool> ActivateAsync(SessionId sessionId, string? titleHint = null, CancellationToken ct = default) => Task.FromResult(false);
}
