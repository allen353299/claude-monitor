using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeMonitor.Core.Persistence;

public readonly record struct PersistedWindowState(int X, int Y, double Width, double Height, bool Topmost);

internal sealed class PersistedWindowStateFile
{
    [JsonPropertyName("Configurations")]
    public Dictionary<string, PersistedWindowState> Configurations { get; set; } = new();

    [JsonPropertyName("Last")]
    public PersistedWindowState? Last { get; set; }
}

public static class PersistedWindowStateStore
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeMonitor");

    public static readonly string DefaultFilePath = Path.Combine(ConfigDirectory, "window.json");

    public static string PathFor(string name) => Path.Combine(ConfigDirectory, name + ".json");

    public static bool TryLoad(out PersistedWindowState state) => TryLoad(DefaultFilePath, out state);

    public static bool TryLoad(string filePath, out PersistedWindowState state)
        => TryLoad(filePath, fingerprint: null, out state);

    public static bool TryLoad(string filePath, string? fingerprint, out PersistedWindowState state)
    {
        state = default;
        if (!TryReadFile(filePath, out var file)) return false;

        if (fingerprint is not null
            && file.Configurations.TryGetValue(fingerprint, out var match)
            && IsValid(match))
        {
            state = match;
            return true;
        }

        if (file.Last is { } last && IsValid(last))
        {
            state = last;
            return true;
        }

        return false;
    }

    public static void Save(PersistedWindowState state) => Save(DefaultFilePath, state);

    public static void Save(string filePath, PersistedWindowState state)
        => Save(filePath, fingerprint: null, state);

    public static void Save(string filePath, string? fingerprint, PersistedWindowState state)
    {
        try
        {
            if (!TryReadFile(filePath, out var file))
            {
                file = new PersistedWindowStateFile();
            }
            if (fingerprint is not null)
            {
                file.Configurations[fingerprint] = state;
            }
            file.Last = state;

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, JsonSerializer.Serialize(file));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static bool TryReadFile(string filePath, out PersistedWindowStateFile file)
    {
        file = new PersistedWindowStateFile();
        try
        {
            if (!File.Exists(filePath)) return false;
            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            if (root.TryGetProperty("Configurations", out _) || root.TryGetProperty("Last", out _))
            {
                var parsed = JsonSerializer.Deserialize<PersistedWindowStateFile>(json);
                if (parsed is null) return false;
                file = parsed;
                return true;
            }

            var legacy = JsonSerializer.Deserialize<PersistedWindowState>(json);
            if (!IsValid(legacy)) return false;
            file.Last = legacy;
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (JsonException) { return false; }
    }

    private static bool IsValid(PersistedWindowState s) => s.Width > 0 && s.Height > 0;
}
