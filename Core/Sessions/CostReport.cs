namespace ClaudeMonitor.Core.Sessions;

public sealed record SessionCost(
    SessionId Id,
    JsonlPath JsonlPath,
    string ProjectPath,
    string? CustomTitle,
    string Model,
    long InputTokens,
    long OutputTokens,
    long CacheWriteTokens,
    long CacheReadTokens,
    double UsdTotal,
    DateTimeOffset LastActivity);

public sealed record CostReport(
    IReadOnlyList<SessionCost> Sessions,
    double GrandTotalUsd);

public interface ICostReader
{
    Task<CostReport> ReadAsync(CancellationToken ct = default);
}
