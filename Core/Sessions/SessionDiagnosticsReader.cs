using System.Collections.Concurrent;
using System.Text.Json;

namespace ClaudeMonitor.Core.Sessions;

public sealed class SessionDiagnosticsReader : ISessionDiagnosticsReader
{
    private const int LargeToolResultThreshold = 10 * 1024;
    private const int PromptPreviewLength = 120;

    private readonly string _projectsRoot;

    public SessionDiagnosticsReader(string? claudeHome = null)
    {
        var home = claudeHome
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        _projectsRoot = Path.Combine(home, "projects");
    }

    public async Task<IReadOnlyDictionary<SessionId, SessionDiagnostics>> ReadAsync(CancellationToken ct = default)
    {
        var result = new ConcurrentDictionary<SessionId, SessionDiagnostics>();
        if (!Directory.Exists(_projectsRoot)) return result;

        var files = new List<string>();
        foreach (var projectDir in EnumerateDirectoriesSafe(_projectsRoot))
            files.AddRange(EnumerateFilesSafe(projectDir, "*.jsonl"));

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
            async (file, token) =>
            {
                var diag = await ParseFileAsync(file, token).ConfigureAwait(false);
                if (diag is not null) result[diag.Id] = diag;
            }).ConfigureAwait(false);

        return result;
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string root)
    {
        try { return Directory.EnumerateDirectories(root); }
        catch (IOException) { return Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string dir, string pattern)
    {
        try { return Directory.EnumerateFiles(dir, pattern); }
        catch (IOException) { return Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    private static async Task<SessionDiagnostics?> ParseFileAsync(string filePath, CancellationToken ct)
    {
        var state = new ParseState();
        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line)) continue;
                ProcessLine(line, state);
            }
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }

        state.FlushOpenBlock();
        if (state.AssistantTurns == 0 && state.ReadCalls == 0) return null;

        var sessionId = SessionId.From(Path.GetFileNameWithoutExtension(filePath));
        return state.Build(sessionId);
    }

    private static void ProcessLine(string line, ParseState state)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) return;
            var type = typeEl.GetString();

            if (type == "user") ProcessUser(root, state);
            else if (type == "assistant") ProcessAssistant(root, state);
        }
        catch (JsonException) { }
    }

    private static void ProcessUser(JsonElement root, ParseState state)
    {
        if (!root.TryGetProperty("message", out var msgEl)) return;
        if (!msgEl.TryGetProperty("content", out var contentEl)) return;

        if (contentEl.ValueKind == JsonValueKind.String)
        {
            var text = contentEl.GetString();
            if (string.IsNullOrWhiteSpace(text)) return;
            state.OpenBlock(text!.Trim());
            return;
        }

        if (contentEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in contentEl.EnumerateArray())
            {
                if (!item.TryGetProperty("type", out var itemTypeEl)) continue;
                if (itemTypeEl.GetString() != "tool_result") continue;
                if (!item.TryGetProperty("content", out var resContent)) continue;

                var size = resContent.ValueKind == JsonValueKind.String
                    ? (resContent.GetString()?.Length ?? 0)
                    : resContent.GetRawText().Length;
                state.RecordToolResultSize(size);
            }
        }
    }

    private static void ProcessAssistant(JsonElement root, ParseState state)
    {
        if (!root.TryGetProperty("message", out var msgEl)) return;

        string? model = null;
        if (msgEl.TryGetProperty("model", out var modelEl) && modelEl.ValueKind == JsonValueKind.String)
            model = modelEl.GetString();

        long ti = 0, to = 0, tw = 0, tr = 0;
        if (msgEl.TryGetProperty("usage", out var usageEl))
        {
            ti = ReadLong(usageEl, "input_tokens");
            to = ReadLong(usageEl, "output_tokens");
            tw = ReadLong(usageEl, "cache_creation_input_tokens");
            tr = ReadLong(usageEl, "cache_read_input_tokens");
        }

        var pricing = CostCalculator.PriceFor(model);
        var usd = CostCalculator.ComputeUsd(pricing, ti, to, tw, tr);

        var day = ExtractDay(root);
        state.RecordAssistantTurn(usd, tr, day);

        if (msgEl.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in contentEl.EnumerateArray())
            {
                if (!block.TryGetProperty("type", out var blockTypeEl)) continue;
                if (blockTypeEl.GetString() != "tool_use") continue;
                if (!block.TryGetProperty("name", out var nameEl)) continue;

                var toolName = nameEl.GetString();
                if (toolName != "Read") continue;

                if (block.TryGetProperty("input", out var inputEl)
                    && inputEl.TryGetProperty("file_path", out var pathEl)
                    && pathEl.ValueKind == JsonValueKind.String)
                {
                    state.RecordRead(pathEl.GetString()!);
                }
            }
        }
    }

    private static string? ExtractDay(JsonElement root)
    {
        if (!root.TryGetProperty("timestamp", out var tsEl)) return null;
        if (tsEl.ValueKind != JsonValueKind.String) return null;
        var s = tsEl.GetString();
        return s is { Length: >= 10 } ? s.Substring(0, 10) : null;
    }

    private static long ReadLong(JsonElement parent, string property)
        => parent.TryGetProperty(property, out var el) && el.TryGetInt64(out var v) ? v : 0;

    private sealed class ParseState
    {
        private readonly Dictionary<string, int> _reads = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DailyAcc> _byDay = new();

        public int ReadCalls { get; private set; }
        public int AssistantTurns { get; private set; }

        private long _biggestResult;
        private int _largeResultCount;

        private string? _currentPrompt;
        private double _currentPromptUsd;
        private double _topPromptUsd;
        private string? _topPromptPreview;

        public void OpenBlock(string text)
        {
            FlushOpenBlock();
            _currentPrompt = text;
            _currentPromptUsd = 0;
        }

        public void FlushOpenBlock()
        {
            if (_currentPrompt is null) return;
            if (_currentPromptUsd > _topPromptUsd)
            {
                _topPromptUsd = _currentPromptUsd;
                _topPromptPreview = Preview(_currentPrompt);
            }
            _currentPrompt = null;
            _currentPromptUsd = 0;
        }

        public void RecordRead(string path)
        {
            _reads[path] = _reads.TryGetValue(path, out var n) ? n + 1 : 1;
            ReadCalls++;
        }

        public void RecordToolResultSize(int size)
        {
            if (size > _biggestResult) _biggestResult = size;
            if (size > LargeToolResultThreshold) _largeResultCount++;
        }

        public void RecordAssistantTurn(double usd, long cacheRead, string? day)
        {
            AssistantTurns++;
            if (_currentPrompt is not null) _currentPromptUsd += usd;
            if (day is null) return;

            if (!_byDay.TryGetValue(day, out var acc))
            {
                acc = new DailyAcc();
                _byDay[day] = acc;
            }
            acc.Usd += usd;
            acc.Turns++;
            acc.CacheRead += cacheRead;
        }

        public SessionDiagnostics Build(SessionId id)
        {
            var wasted = _reads.Values.Sum(n => n > 1 ? n - 1 : 0);
            var pct = ReadCalls > 0 ? wasted * 100.0 / ReadCalls : 0;

            var daily = _byDay
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => new DailyCost(kv.Key, kv.Value.Usd, kv.Value.Turns, kv.Value.CacheRead))
                .ToList();
            var peak = daily.OrderByDescending(d => d.Usd).FirstOrDefault();

            return new SessionDiagnostics(
                id, ReadCalls, wasted, pct, daily,
                peak?.Usd ?? 0, peak?.Day,
                _topPromptUsd, _topPromptPreview,
                _biggestResult, _largeResultCount);
        }

        private static string Preview(string text)
        {
            var normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return normalized.Length <= PromptPreviewLength ? normalized : normalized.Substring(0, PromptPreviewLength);
        }

        private sealed class DailyAcc
        {
            public double Usd;
            public int Turns;
            public long CacheRead;
        }
    }
}
