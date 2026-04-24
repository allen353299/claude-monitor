namespace ClaudeMonitor.Core.Sessions;

public sealed class ProjectPathDecoder : IProjectPathDecoder
{
    private const string Unknown = "unknown";

    public string ExtractProjectName(JsonlPath jsonlPath)
    {
        var dirName = GetParentDirName(jsonlPath);
        if (dirName is null) return Unknown;

        var trimmed = dirName.TrimStart('-');
        if (string.IsNullOrEmpty(trimmed)) return dirName;

        var segments = trimmed.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 ? dirName : segments[^1];
    }

    public ProjectPath Decode(JsonlPath jsonlPath)
    {
        var dirName = GetParentDirName(jsonlPath);
        if (dirName is null) return ProjectPath.From(Unknown);

        var decoded = dirName.Replace('-', Path.DirectorySeparatorChar);
        return ProjectPath.From(string.IsNullOrWhiteSpace(decoded) ? Unknown : decoded);
    }

    private static string? GetParentDirName(JsonlPath jsonlPath)
    {
        var parentDir = Path.GetDirectoryName(jsonlPath.Value);
        return string.IsNullOrEmpty(parentDir) ? null : new DirectoryInfo(parentDir).Name;
    }
}
