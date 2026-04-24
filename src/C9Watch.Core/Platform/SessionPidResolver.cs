using System.Text.Json;

namespace C9Watch.Core.Platform;

public static class SessionPidResolver
{
    public static int? ResolvePid(string sessionId, string? claudeHome = null)
    {
        if (string.IsNullOrEmpty(sessionId)) return null;

        claudeHome ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        var sessionsDir = Path.Combine(claudeHome, "sessions");
        if (!Directory.Exists(sessionsDir)) return null;

        foreach (var file in Directory.EnumerateFiles(sessionsDir, "*.json"))
        {
            try
            {
                using var stream = File.OpenRead(file);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;
                if (!doc.RootElement.TryGetProperty("sessionId", out var sidEl)) continue;
                if (sidEl.ValueKind != JsonValueKind.String) continue;
                if (sidEl.GetString() != sessionId) continue;

                var name = Path.GetFileNameWithoutExtension(file);
                if (int.TryParse(name, out var pid)) return pid;
            }
            catch (IOException) { }
            catch (JsonException) { }
            catch (UnauthorizedAccessException) { }
        }

        return null;
    }
}
