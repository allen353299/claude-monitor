namespace ClaudeMonitor.Core.Sessions;

public interface ISessionScanner : IAsyncDisposable
{
    event EventHandler<SessionUpdatedEventArgs>? SessionUpdated;
    event EventHandler<SessionRemovedEventArgs>? SessionRemoved;

    IReadOnlyCollection<Session> CurrentSessions { get; }

    Task StartAsync(CancellationToken ct = default);
}
