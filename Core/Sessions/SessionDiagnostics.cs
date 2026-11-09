namespace ClaudeMonitor.Core.Sessions;

public sealed record DailyCost(string Day, double Usd, int Turns, long TotalCacheRead);

public sealed record SessionDiagnostics(
    SessionId Id,
    int ReadCalls,
    int WastedReads,
    double ReReadPercent,
    IReadOnlyList<DailyCost> Daily,
    double PeakDayUsd,
    string? PeakDay,
    double TopPromptUsd,
    string? TopPromptPreview,
    long BiggestToolResultBytes,
    int LargeToolResultCount);

public interface ISessionDiagnosticsReader
{
    Task<IReadOnlyDictionary<SessionId, SessionDiagnostics>> ReadAsync(CancellationToken ct = default);
}
