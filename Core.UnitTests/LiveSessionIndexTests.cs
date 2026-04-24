using System.IO;
using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Platform;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class LiveSessionIndexTests
{
    [Fact]
    public void Snapshot_NoSessionsDir_ReturnsEmpty()
    {
        using var tmp = new TempDir();
        var index = new LiveSessionIndex(tmp.Path);
        Assert.Empty(index.Snapshot());
    }

    [Fact]
    public void Snapshot_NonIntegerFileName_Skipped()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "sessions"));
        File.WriteAllText(Path.Combine(tmp.Path, "sessions", "not-a-pid.json"),
            """{"sessionId":"abc"}""");

        var index = new LiveSessionIndex(tmp.Path);
        Assert.Empty(index.Snapshot());
    }

    [Fact]
    public void Snapshot_IntegerFileNameButDeadPid_Skipped()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "sessions"));
        // PID 1 on Windows is always System — its name won't match claude/node.
        File.WriteAllText(Path.Combine(tmp.Path, "sessions", "1.json"),
            """{"sessionId":"abc"}""");

        Assert.Empty(new LiveSessionIndex(tmp.Path).Snapshot());
    }

    [Fact]
    public void TryGetPid_EmptySessionId_ReturnsFalse()
    {
        using var tmp = new TempDir();
        var index = new LiveSessionIndex(tmp.Path);
        Assert.False(index.TryGetPid(default, out var pid));
        Assert.Equal(0, pid);
    }

    [Fact]
    public void TryGetPid_UnknownSession_ReturnsFalse()
    {
        using var tmp = new TempDir();
        var index = new LiveSessionIndex(tmp.Path);
        Assert.False(index.TryGetPid(SessionId.From("never-seen"), out _));
    }
}
