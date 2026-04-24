namespace ClaudeMonitor.Core.Sessions;

public interface IHistoryReader
{
    Task<IReadOnlyList<HistoryEntry>> ReadAsync(CancellationToken ct = default);
}
