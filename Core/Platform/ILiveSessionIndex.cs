namespace ClaudeMonitor.Core.Platform;

public interface ILiveSessionIndex
{
    IReadOnlyDictionary<SessionId, int> Snapshot();
    bool TryGetPid(SessionId sessionId, out int pid);
}
