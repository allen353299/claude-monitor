using System.IO;
using ClaudeMonitor.Core.Persistence;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class PersistedWindowStateStoreTests
{
    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "window.json");
        var original = new PersistedWindowState(-120, 340, 520, 410, Topmost: true);

        PersistedWindowStateStore.Save(path, original);
        Assert.True(PersistedWindowStateStore.TryLoad(path, out var loaded));
        Assert.Equal(original, loaded);
    }

    [Fact]
    public void TryLoad_NonExistentFile_ReturnsFalse()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "nope.json");
        Assert.False(PersistedWindowStateStore.TryLoad(path, out _));
    }

    [Fact]
    public void TryLoad_MalformedJson_ReturnsFalse()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "bad.json");
        File.WriteAllText(path, "not json{");
        Assert.False(PersistedWindowStateStore.TryLoad(path, out _));
    }

    [Fact]
    public void TryLoad_ZeroWidth_Rejected()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "zero.json");
        File.WriteAllText(path, """{"X":0,"Y":0,"Width":0,"Height":400,"Topmost":false}""");
        Assert.False(PersistedWindowStateStore.TryLoad(path, out _));
    }

    [Fact]
    public void TryLoad_NegativeHeight_Rejected()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "neg.json");
        File.WriteAllText(path, """{"X":0,"Y":0,"Width":400,"Height":-1,"Topmost":false}""");
        Assert.False(PersistedWindowStateStore.TryLoad(path, out _));
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "nested", "dir", "window.json");
        PersistedWindowStateStore.Save(path, new PersistedWindowState(0, 0, 320, 400, false));
        Assert.True(File.Exists(path));
    }
}
