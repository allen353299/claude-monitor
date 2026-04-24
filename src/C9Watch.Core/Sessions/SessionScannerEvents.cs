namespace C9Watch.Core.Sessions;

public sealed record SessionUpdatedEventArgs(Session Session);

public sealed record SessionRemovedEventArgs(string SessionId);
