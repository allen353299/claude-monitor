using System.Linq;
using System.Threading.Tasks;
using ClaudeMonitor.Core.Sessions;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class MemoryReaderTests
{
    [Fact]
    public async Task ReadAsync_NoProjectsRoot_ReturnsEmpty()
    {
        using var tmp = new TempDir();
        var reader = new MemoryReader(new ProjectPathDecoder(), tmp.Path);
        var projects = await reader.ReadAsync();
        Assert.Empty(projects);
    }

    [Fact]
    public async Task ReadAsync_ProjectWithoutMemoryDir_IsSkipped()
    {
        using var tmp = new TempDir();
        // Create project dir but no memory subdir.
        System.IO.Directory.CreateDirectory(tmp.Combine("projects/D--foo"));
        var reader = new MemoryReader(new ProjectPathDecoder(), tmp.Path);
        var projects = await reader.ReadAsync();
        Assert.Empty(projects);
    }

    [Fact]
    public async Task ReadAsync_CollectsMemoryMdFiles()
    {
        using var tmp = new TempDir();
        tmp.WriteFile("projects/D--foo/memory/MEMORY.md", "index");
        tmp.WriteFile("projects/D--foo/memory/alpha.md", "alpha body");
        tmp.WriteFile("projects/D--foo/memory/beta.md", "beta body");
        tmp.WriteFile("projects/D--foo/memory/ignore.txt", "not md");

        var reader = new MemoryReader(new ProjectPathDecoder(), tmp.Path);
        var projects = (await reader.ReadAsync()).ToList();

        Assert.Single(projects);
        var p = projects[0];
        Assert.Equal(3, p.Files.Count);
        Assert.Equal("MEMORY.md", p.Files[0].FileName);
        Assert.Equal("index", p.Files[0].Content);
    }

    [Fact]
    public async Task ReadAsync_MemoryMdSortsFirst_ThenAlphabetical()
    {
        using var tmp = new TempDir();
        tmp.WriteFile("projects/D--foo/memory/z.md", "z");
        tmp.WriteFile("projects/D--foo/memory/a.md", "a");
        tmp.WriteFile("projects/D--foo/memory/MEMORY.md", "i");

        var reader = new MemoryReader(new ProjectPathDecoder(), tmp.Path);
        var p = (await reader.ReadAsync()).Single();

        Assert.Equal(new[] { "MEMORY.md", "a.md", "z.md" }, p.Files.Select(f => f.FileName));
    }

    [Fact]
    public async Task ReadAsync_DecodedPathMatchesDecoder()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var tmp = new TempDir();
        tmp.WriteFile("projects/D--git-rohan/memory/x.md", "x");
        var p = (await new MemoryReader(new ProjectPathDecoder(), tmp.Path).ReadAsync()).Single();
        Assert.Equal(@"D:\git\rohan", p.DecodedPath);
    }

    [Fact]
    public async Task ReadAsync_EmptyMemoryDir_IsSkipped()
    {
        using var tmp = new TempDir();
        System.IO.Directory.CreateDirectory(tmp.Combine("projects/D--foo/memory"));
        var projects = await new MemoryReader(new ProjectPathDecoder(), tmp.Path).ReadAsync();
        Assert.Empty(projects);
    }
}
