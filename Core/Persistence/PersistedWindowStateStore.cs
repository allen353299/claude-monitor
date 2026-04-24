using System;
using System.IO;
using System.Text.Json;

namespace ClaudeMonitor.Core.Persistence;

public readonly record struct PersistedWindowState(int X, int Y, double Width, double Height, bool Topmost);

public static class PersistedWindowStateStore
{
    public static readonly string DefaultFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeMonitor",
        "window.json");

    public static bool TryLoad(out PersistedWindowState state) => TryLoad(DefaultFilePath, out state);

    public static bool TryLoad(string filePath, out PersistedWindowState state)
    {
        state = default;
        try
        {
            if (!File.Exists(filePath)) return false;
            var json = File.ReadAllText(filePath);
            var parsed = JsonSerializer.Deserialize<PersistedWindowState>(json);
            if (parsed.Width <= 0 || parsed.Height <= 0) return false;
            state = parsed;
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (JsonException) { return false; }
    }

    public static void Save(PersistedWindowState state) => Save(DefaultFilePath, state);

    public static void Save(string filePath, PersistedWindowState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, JsonSerializer.Serialize(state));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
