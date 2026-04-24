using ClaudeMonitor.Core.Models;

namespace ClaudeMonitor.Core.Sessions;

public interface ISessionEnricher
{
    SessionEnricher.EnrichedData Enrich(IReadOnlyList<SessionEntry> entries);
}
