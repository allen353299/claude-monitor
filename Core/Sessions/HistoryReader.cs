using System.Text.Json;

namespace ClaudeMonitor.Core.Sessions;

public sealed class HistoryReader : IHistoryReader
{
    private const int EnrichTailLines = 30;

    private readonly string _filePath;
    private readonly string _projectsRoot;
    private readonly SessionEnricher _enricher = new();

    public HistoryReader(string? claudeHome = null)
    {
        var home = claudeHome
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        _filePath = Path.Combine(home, "history.jsonl");
        _projectsRoot = Path.Combine(home, "projects");
    }

    public async Task<IReadOnlyList<HistoryEntry>> ReadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath)) return Array.Empty<HistoryEntry>();

        var raw = new List<RawLine>();
        try
        {
            await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (TryParseLine(line, out var entry)) raw.Add(entry);
            }
        }
        catch (IOException) { return Array.Empty<HistoryEntry>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<HistoryEntry>(); }

        var aggregated = Aggregate(raw, _projectsRoot);
        return await EnrichAsync(aggregated, ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<HistoryEntry>> EnrichAsync(IReadOnlyList<HistoryEntry> entries, CancellationToken ct)
    {
        var enriched = new HistoryEntry[entries.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, entries.Count),
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
            async (index, token) =>
            {
                enriched[index] = await EnrichSingleAsync(entries[index], token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        return enriched;
    }

    private async Task<HistoryEntry> EnrichSingleAsync(HistoryEntry entry, CancellationToken ct)
    {
        if (entry.JsonlPath is null) return entry;
        try
        {
            var tail = await SessionParser.ParseTailAsync(entry.JsonlPath.Value, EnrichTailLines, ct).ConfigureAwait(false);
            var data = _enricher.Enrich(tail);
            return entry with
            {
                CustomTitle = data.CustomTitle?.Value,
                Recap = data.Recap?.Value,
            };
        }
        catch (IOException) { return entry; }
        catch (UnauthorizedAccessException) { return entry; }
        catch (System.Text.Json.JsonException) { return entry; }
    }

    private static bool TryParseLine(string line, out RawLine entry)
    {
        entry = default;
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("sessionId", out var idEl)) return false;
            if (!root.TryGetProperty("timestamp", out var tsEl)) return false;
            var sid = idEl.GetString();
            if (string.IsNullOrWhiteSpace(sid)) return false;

            var ts = DateTimeOffset.FromUnixTimeMilliseconds(tsEl.GetInt64());
            var display = root.TryGetProperty("display", out var dEl) ? dEl.GetString() ?? "" : "";
            var project = root.TryGetProperty("project", out var pEl) ? pEl.GetString() ?? "" : "";
            entry = new RawLine(SessionId.From(sid), project, ts, display);
            return true;
        }
        catch (JsonException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private static IReadOnlyList<HistoryEntry> Aggregate(List<RawLine> raw, string projectsRoot)
    {
        var bySession = new Dictionary<SessionId, (string Project, DateTimeOffset First, DateTimeOffset Last, string LastDisplay, int Count)>();
        foreach (var r in raw)
        {
            if (bySession.TryGetValue(r.Id, out var cur))
            {
                var first = r.Timestamp < cur.First ? r.Timestamp : cur.First;
                var last = r.Timestamp > cur.Last ? r.Timestamp : cur.Last;
                var display = r.Timestamp >= cur.Last ? r.Display : cur.LastDisplay;
                bySession[r.Id] = (cur.Project, first, last, display, cur.Count + 1);
            }
            else
            {
                bySession[r.Id] = (r.Project, r.Timestamp, r.Timestamp, r.Display, 1);
            }
        }

        var list = new List<HistoryEntry>(bySession.Count);
        foreach (var kv in bySession)
        {
            list.Add(new HistoryEntry(
                kv.Key,
                kv.Value.Project,
                ResolveJsonlPath(projectsRoot, kv.Value.Project, kv.Key),
                kv.Value.First,
                kv.Value.Last,
                kv.Value.LastDisplay,
                kv.Value.Count,
                CustomTitle: null,
                Recap: null));
        }
        list.Sort((a, b) => b.LastActivityAt.CompareTo(a.LastActivityAt));
        return list;
    }

    private static JsonlPath? ResolveJsonlPath(string projectsRoot, string projectPath, SessionId sessionId)
    {
        if (string.IsNullOrEmpty(projectPath)) return null;
        var encoded = ProjectPathEncoder.Encode(projectPath);
        var candidate = Path.Combine(projectsRoot, encoded, sessionId.Value + ".jsonl");
        if (!File.Exists(candidate)) return null;
        return JsonlPath.From(candidate);
    }

    private readonly record struct RawLine(SessionId Id, string Project, DateTimeOffset Timestamp, string Display);
}
