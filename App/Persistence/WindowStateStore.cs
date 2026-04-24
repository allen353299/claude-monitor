using System;
using System.IO;
using System.Text.Json;

namespace ClaudeMonitor.App.Persistence;

public readonly record struct PersistedWindowState(int X, int Y, double Width, double Height, bool Topmost);

public static class PersistedWindowStateStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeMonitor",
        "window.json");

    public static bool TryLoad(out PersistedWindowState state)
    {
        state = default;
        try
        {
            if (!File.Exists(FilePath)) return false;
            var json = File.ReadAllText(FilePath);
            var parsed = JsonSerializer.Deserialize<PersistedWindowState>(json);
            if (parsed.Width <= 0 || parsed.Height <= 0) return false;
            state = parsed;
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (JsonException) { return false; }
    }

    public static void Save(PersistedWindowState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(state));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
