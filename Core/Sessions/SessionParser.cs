using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Models;

namespace ClaudeMonitor.Core.Sessions;

public static class SessionParser
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static async Task<IReadOnlyList<SessionEntry>> ParseTailAsync(
        JsonlPath filePath,
        int maxLines = 20,
        CancellationToken ct = default)
    {
        var lines = await ReadAllLinesSharedAsync(filePath.Value, ct).ConfigureAwait(false);
        return DeserializeLines(lines, Math.Max(0, lines.Count - maxLines), lines.Count);
    }

    public static async Task<IReadOnlyList<SessionEntry>> ParseAllAsync(
        JsonlPath filePath,
        CancellationToken ct = default)
    {
        var lines = await ReadAllLinesSharedAsync(filePath.Value, ct).ConfigureAwait(false);
        return DeserializeLines(lines, 0, lines.Count);
    }

    private static List<SessionEntry> DeserializeLines(IReadOnlyList<string> lines, int start, int endExclusive)
    {
        var results = new List<SessionEntry>(endExclusive - start);
        for (var i = start; i < endExclusive; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var entry = JsonSerializer.Deserialize<SessionEntry>(line, Options);
                if (entry != null) results.Add(entry);
            }
            catch (JsonException) { }
        }
        return results;
    }

    private static async Task<List<string>> ReadAllLinesSharedAsync(string filePath, CancellationToken ct)
    {
        var lines = new List<string>();
        await using var fs = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            useAsync: true);
        using var reader = new StreamReader(fs, Encoding.UTF8);

        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            lines.Add(line);
        }

        return lines;
    }
}
