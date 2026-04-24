using System.IO;
using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Sessions;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class ProjectPathDecoderTests
{
    private readonly ProjectPathDecoder _decoder = new();

    private static JsonlPath Path(string dirName, string fileName = "abc.jsonl")
    {
        var home = OperatingSystem.IsWindows() ? @"C:\home\.claude\projects" : "/home/.claude/projects";
        return JsonlPath.From(System.IO.Path.Combine(home, dirName, fileName));
    }

    [Fact]
    public void Decode_WindowsDrive_PrependsColonBackslash()
    {
        if (!OperatingSystem.IsWindows()) return;
        var decoded = _decoder.Decode(Path("D--git"));
        Assert.Equal(@"D:\git", decoded.Value);
    }

    [Fact]
    public void Decode_WindowsDriveWithSubdir_ReplacesRemainingDashesWithBackslash()
    {
        if (!OperatingSystem.IsWindows()) return;
        var decoded = _decoder.Decode(Path("D--git-rohan"));
        Assert.Equal(@"D:\git\rohan", decoded.Value);
    }

    [Fact]
    public void Decode_LowercaseDriveLetter_UppercasesIt()
    {
        if (!OperatingSystem.IsWindows()) return;
        var decoded = _decoder.Decode(Path("d--git"));
        Assert.Equal(@"D:\git", decoded.Value);
    }

    [Fact]
    public void Decode_ShortDirName_FallsBackToPlainReplace()
    {
        if (!OperatingSystem.IsWindows()) return;
        // No "X--" prefix — treated as plain dash-separated path.
        var decoded = _decoder.Decode(Path("foo-bar"));
        Assert.Equal(@"foo\bar", decoded.Value);
    }

    [Fact]
    public void ExtractProjectName_TakesLastSegmentAfterDash()
    {
        var name = _decoder.ExtractProjectName(Path("D--git-rohan"));
        Assert.Equal("rohan", name);
    }

    [Fact]
    public void ExtractProjectName_SingleSegment_ReturnsIt()
    {
        var name = _decoder.ExtractProjectName(Path("D--git"));
        Assert.Equal("git", name);
    }

}
