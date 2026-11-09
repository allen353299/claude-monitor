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

    [Fact]
    public void Save_With_Fingerprint_Roundtrips_Per_Layout()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "window.json");
        var stateA = new PersistedWindowState(10, 20, 600, 400, false);
        var stateB = new PersistedWindowState(2000, 100, 600, 400, true);

        PersistedWindowStateStore.Save(path, "layout-A", stateA);
        PersistedWindowStateStore.Save(path, "layout-B", stateB);

        Assert.True(PersistedWindowStateStore.TryLoad(path, "layout-A", out var loadedA));
        Assert.Equal(stateA, loadedA);
        Assert.True(PersistedWindowStateStore.TryLoad(path, "layout-B", out var loadedB));
        Assert.Equal(stateB, loadedB);
    }

    [Fact]
    public void TryLoad_Unknown_Fingerprint_Falls_Back_To_Last()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "window.json");
        var state = new PersistedWindowState(50, 60, 700, 500, false);
        PersistedWindowStateStore.Save(path, "layout-A", state);

        Assert.True(PersistedWindowStateStore.TryLoad(path, "unknown-layout", out var loaded));
        Assert.Equal(state, loaded);
    }

    [Fact]
    public void Legacy_File_Is_Imported_As_Last()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "window.json");
        File.WriteAllText(path, """{"X":42,"Y":24,"Width":640,"Height":480,"Topmost":true}""");

        Assert.True(PersistedWindowStateStore.TryLoad(path, "any-fingerprint", out var loaded));
        Assert.Equal(new PersistedWindowState(42, 24, 640, 480, true), loaded);

        Assert.True(PersistedWindowStateStore.TryLoad(path, out var loadedNoFp));
        Assert.Equal(new PersistedWindowState(42, 24, 640, 480, true), loadedNoFp);
    }

    [Fact]
    public void Save_Updates_Last_Field()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "window.json");

        PersistedWindowStateStore.Save(path, "layout-A", new PersistedWindowState(1, 2, 100, 100, false));
        var newer = new PersistedWindowState(9, 9, 200, 200, true);
        PersistedWindowStateStore.Save(path, "layout-B", newer);

        Assert.True(PersistedWindowStateStore.TryLoad(path, "no-such-layout", out var loaded));
        Assert.Equal(newer, loaded);
    }
}
