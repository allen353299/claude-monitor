using System.Diagnostics;
using System.Text.Json;

namespace ClaudeMonitor.Core.Platform;

public sealed class LiveSessionIndex : ILiveSessionIndex
{
    private readonly string _sessionsDir;

    public LiveSessionIndex(string? claudeHome = null)
    {
        var home = claudeHome
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        _sessionsDir = Path.Combine(home, "sessions");
    }

    public IReadOnlyDictionary<SessionId, int> Snapshot()
    {
        var result = new Dictionary<SessionId, int>();
        if (!Directory.Exists(_sessionsDir)) return result;

        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(_sessionsDir, "*.json"); }
        catch (IOException) { return result; }
        catch (UnauthorizedAccessException) { return result; }

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!int.TryParse(name, out var pid)) continue;
            if (!IsClaudeProcessAlive(pid)) continue;

            var sessionId = ReadSessionId(file);
            if (!string.IsNullOrEmpty(sessionId)) result[SessionId.From(sessionId)] = pid;
        }

        return result;
    }

    public bool TryGetPid(SessionId sessionId, out int pid)
    {
        if (string.IsNullOrEmpty(sessionId.Value))
        {
            pid = 0;
            return false;
        }
        return Snapshot().TryGetValue(sessionId, out pid);
    }

    private static string? ReadSessionId(string pidFilePath)
    {
        try
        {
            using var stream = File.OpenRead(pidFilePath);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty("sessionId", out var el)) return null;
            if (el.ValueKind != JsonValueKind.String) return null;
            return el.GetString();
        }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static bool IsClaudeProcessAlive(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (p.HasExited) return false;
            var name = p.ProcessName.ToLowerInvariant();
            return name.Contains("claude") || name.Contains("node");
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }
}
