namespace ClaudeMonitor.Core.Sessions;

public sealed record SessionUpdatedEventArgs(Session Session);

public sealed record SessionRemovedEventArgs(SessionId SessionId);
