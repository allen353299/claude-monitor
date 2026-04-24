namespace ClaudeMonitor.Core.Sessions;

public sealed record MemoryFile(string FileName, string FullPath, string Content);

public sealed record MemoryProject(string ProjectDir, string DecodedPath, IReadOnlyList<MemoryFile> Files);

public interface IMemoryReader
{
    Task<IReadOnlyList<MemoryProject>> ReadAsync(CancellationToken ct = default);
}

public sealed class MemoryReader : IMemoryReader
{
    private readonly string _projectsRoot;
    private readonly IProjectPathDecoder _decoder;

    public MemoryReader(IProjectPathDecoder decoder, string? claudeHome = null)
    {
        _decoder = decoder;
        var home = claudeHome
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        _projectsRoot = Path.Combine(home, "projects");
    }

    public async Task<IReadOnlyList<MemoryProject>> ReadAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_projectsRoot)) return Array.Empty<MemoryProject>();

        var projects = new List<MemoryProject>();
        foreach (var projectDir in Directory.EnumerateDirectories(_projectsRoot))
        {
            ct.ThrowIfCancellationRequested();
            var memoryDir = Path.Combine(projectDir, "memory");
            if (!Directory.Exists(memoryDir)) continue;

            var files = new List<MemoryFile>();
            foreach (var file in Directory.EnumerateFiles(memoryDir, "*.md"))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                    files.Add(new MemoryFile(Path.GetFileName(file), file, content));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            if (files.Count == 0) continue;

            files.Sort((a, b) =>
            {
                if (a.FileName.Equals("MEMORY.md", StringComparison.OrdinalIgnoreCase)) return -1;
                if (b.FileName.Equals("MEMORY.md", StringComparison.OrdinalIgnoreCase)) return 1;
                return string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
            });

            var dirName = Path.GetFileName(projectDir);
            var decoded = DecodeSafely(dirName);
            projects.Add(new MemoryProject(dirName, decoded, files));
        }

        projects.Sort((a, b) => string.Compare(a.DecodedPath, b.DecodedPath, StringComparison.OrdinalIgnoreCase));
        return projects;
    }

    private string DecodeSafely(string dirName)
    {
        try
        {
            var fakeJsonlPath = JsonlPath.From(Path.Combine(_projectsRoot, dirName, "x.jsonl"));
            return _decoder.Decode(fakeJsonlPath).Value;
        }
        catch
        {
            return dirName;
        }
    }
}
