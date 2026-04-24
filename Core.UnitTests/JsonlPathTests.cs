using System;
using System.IO;
using ClaudeMonitor.Core;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class JsonlPathTests
{
    [Fact]
    public void From_PreservesCasing()
    {
        if (!OperatingSystem.IsWindows()) return;
        var p = JsonlPath.From(@"D:\Git\Foo.JSONL");
        Assert.Equal(@"D:\Git\Foo.JSONL", p.Value);
    }

    [Fact]
    public void From_NullOrWhitespace_Throws()
    {
        Assert.Throws<ArgumentException>(() => JsonlPath.From(""));
        Assert.Throws<ArgumentException>(() => JsonlPath.From("   "));
    }

    [Fact]
    public void From_RelativePath_Throws()
    {
        Assert.Throws<ArgumentException>(() => JsonlPath.From("some/relative.jsonl"));
    }

    [Fact]
    public void TryGetLastWrite_MissingFile_ReturnsFalse()
    {
        var missing = JsonlPath.From(OperatingSystem.IsWindows()
            ? @"C:\definitely\does\not\exist\claude-monitor-test.jsonl"
            : "/tmp/definitely-does-not-exist-claude-monitor-test.jsonl");
        Assert.False(missing.TryGetLastWrite(out var _));
    }

    [Fact]
    public void TryGetLastWrite_ExistingFile_ReturnsTrueWithRecentTimestamp()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "claude-monitor-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var file = Path.Combine(tempDir, "abc.jsonl");
            File.WriteAllText(file, "{}\n");
            var path = JsonlPath.From(file);
            Assert.True(path.TryGetLastWrite(out var lastWrite));
            Assert.True((DateTimeOffset.UtcNow - lastWrite).Duration() < TimeSpan.FromMinutes(1));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ToString_DefaultInstance_DoesNotThrow()
    {
        // default(JsonlPath).Value is null. ToString must not NRE.
        var def = default(JsonlPath);
        Assert.Equal(string.Empty, def.ToString());
    }

    [Fact]
    public void Equality_IsCaseSensitive()
    {
        if (!OperatingSystem.IsWindows()) return;
        // Case preservation means equality is case-sensitive — document this.
        var a = JsonlPath.From(@"D:\Git\Foo.jsonl");
        var b = JsonlPath.From(@"D:\git\foo.jsonl");
        Assert.NotEqual(a, b);
    }
}
