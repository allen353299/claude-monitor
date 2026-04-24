using System.Text.Json;

namespace ClaudeMonitor.Core.Sessions;

public sealed class CostReader : ICostReader
{
    private readonly string _projectsRoot;
    private readonly IProjectPathDecoder _decoder;

    public CostReader(IProjectPathDecoder decoder, string? claudeHome = null)
    {
        _decoder = decoder;
        var home = claudeHome
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        _projectsRoot = Path.Combine(home, "projects");
    }

    public async Task<CostReport> ReadAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_projectsRoot))
            return new CostReport(Array.Empty<SessionCost>(), 0);

        var sessions = new List<SessionCost>();
        double grand = 0;

        foreach (var projectDir in Directory.EnumerateDirectories(_projectsRoot))
        {
            foreach (var file in Directory.EnumerateFiles(projectDir, "*.jsonl"))
            {
                ct.ThrowIfCancellationRequested();
                var cost = await ParseFileAsync(file, ct).ConfigureAwait(false);
                if (cost is null) continue;
                sessions.Add(cost);
                grand += cost.UsdTotal;
            }
        }

        sessions.Sort((a, b) => b.UsdTotal.CompareTo(a.UsdTotal));
        return new CostReport(sessions, grand);
    }

    private async Task<SessionCost?> ParseFileAsync(string filePath, CancellationToken ct)
    {
        long input = 0, output = 0, cacheWrite = 0, cacheRead = 0;
        string? model = null;
        DateTimeOffset lastActivity = DateTimeOffset.MinValue;

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line)) continue;
                AccumulateLine(line, ref input, ref output, ref cacheWrite, ref cacheRead, ref model, ref lastActivity);
            }
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }

        if (input == 0 && output == 0 && cacheWrite == 0 && cacheRead == 0) return null;

        var sessionId = SessionId.From(Path.GetFileNameWithoutExtension(filePath));
        var pricing = CostCalculator.PriceFor(model);
        var usd = CostCalculator.ComputeUsd(pricing, input, output, cacheWrite, cacheRead);

        var projectPath = DecodeProjectPath(filePath);

        return new SessionCost(
            sessionId,
            projectPath,
            model ?? "unknown",
            input, output, cacheWrite, cacheRead,
            usd,
            lastActivity);
    }

    private static void AccumulateLine(
        string line,
        ref long input, ref long output, ref long cacheWrite, ref long cacheRead,
        ref string? model, ref DateTimeOffset lastActivity)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) return;
            if (typeEl.GetString() != "assistant") return;

            if (root.TryGetProperty("message", out var msgEl))
            {
                if (msgEl.TryGetProperty("model", out var modelEl) && model is null)
                    model = modelEl.GetString();

                if (msgEl.TryGetProperty("usage", out var usageEl))
                {
                    input += ReadLong(usageEl, "input_tokens");
                    output += ReadLong(usageEl, "output_tokens");
                    cacheWrite += ReadLong(usageEl, "cache_creation_input_tokens");
                    cacheRead += ReadLong(usageEl, "cache_read_input_tokens");
                }
            }

            if (root.TryGetProperty("timestamp", out var tsEl) && tsEl.ValueKind == JsonValueKind.String)
            {
                if (DateTimeOffset.TryParse(tsEl.GetString(), out var ts) && ts > lastActivity)
                    lastActivity = ts;
            }
        }
        catch (JsonException) { }
    }

    private static long ReadLong(JsonElement parent, string property)
    {
        return parent.TryGetProperty(property, out var el) && el.TryGetInt64(out var v) ? v : 0;
    }

    private string DecodeProjectPath(string jsonlFilePath)
    {
        try
        {
            var decoded = _decoder.Decode(JsonlPath.From(jsonlFilePath));
            return decoded.Value;
        }
        catch
        {
            return Path.GetFileName(Path.GetDirectoryName(jsonlFilePath) ?? "");
        }
    }
}
