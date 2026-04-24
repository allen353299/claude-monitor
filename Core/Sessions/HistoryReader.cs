using System.Text.Json;

namespace ClaudeMonitor.Core.Sessions;

public sealed class HistoryReader : IHistoryReader
{
    private readonly string _filePath;

    public HistoryReader(string? claudeHome = null)
    {
        var home = claudeHome
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        _filePath = Path.Combine(home, "history.jsonl");
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

        return Aggregate(raw);
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

    private static IReadOnlyList<HistoryEntry> Aggregate(List<RawLine> raw)
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
                kv.Value.First,
                kv.Value.Last,
                kv.Value.LastDisplay,
                kv.Value.Count));
        }
        list.Sort((a, b) => b.LastActivityAt.CompareTo(a.LastActivityAt));
        return list;
    }

    private readonly record struct RawLine(SessionId Id, string Project, DateTimeOffset Timestamp, string Display);
}
