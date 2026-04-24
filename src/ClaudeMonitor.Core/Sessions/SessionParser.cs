using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        string filePath,
        int maxLines = 20,
        CancellationToken ct = default)
    {
        var lines = await ReadAllLinesSharedAsync(filePath, ct).ConfigureAwait(false);

        var start = Math.Max(0, lines.Count - maxLines);
        var results = new List<SessionEntry>(Math.Min(maxLines, lines.Count - start));

        for (var i = start; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var entry = JsonSerializer.Deserialize<SessionEntry>(line, Options);
                if (entry != null) results.Add(entry);
            }
            catch (JsonException)
            {
            }
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

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line == null) break;
            lines.Add(line);
        }

        return lines;
    }
}
