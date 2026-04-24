using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Models;
using ClaudeMonitor.Core.Sessions;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class SessionParserTests
{
    private static JsonlPath WriteLines(TempDir tmp, params string[] lines)
    {
        var path = Path.Combine(tmp.Path, "session.jsonl");
        File.WriteAllText(path, string.Join('\n', lines));
        return JsonlPath.From(path);
    }

    [Fact]
    public async Task ParseAllAsync_EmptyFile_ReturnsEmpty()
    {
        using var tmp = new TempDir();
        var path = WriteLines(tmp);
        Assert.Empty(await SessionParser.ParseAllAsync(path));
    }

    [Fact]
    public async Task ParseAllAsync_ParsesUserAndAssistantEntries()
    {
        using var tmp = new TempDir();
        var path = WriteLines(tmp,
            """{"type":"user","message":{"role":"user","content":"hello"}}""",
            """{"type":"assistant","message":{"model":"x","role":"assistant","content":[{"type":"text","text":"hi"}]}}""");
        var entries = await SessionParser.ParseAllAsync(path);
        Assert.Equal(2, entries.Count);
        Assert.IsType<UserEntry>(entries[0]);
        Assert.IsType<AssistantEntry>(entries[1]);
    }

    [Fact]
    public async Task ParseAllAsync_SkipsMalformedLines()
    {
        using var tmp = new TempDir();
        var path = WriteLines(tmp,
            """{"type":"user","message":{"role":"user","content":"ok"}}""",
            "garbage {{",
            "",
            """{"type":"summary","summary":"done","leafUuid":"x"}""");
        var entries = await SessionParser.ParseAllAsync(path);
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public async Task ParseTailAsync_LimitsToMaxLines()
    {
        using var tmp = new TempDir();
        var path = WriteLines(tmp,
            """{"type":"user","message":{"role":"user","content":"1"}}""",
            """{"type":"user","message":{"role":"user","content":"2"}}""",
            """{"type":"user","message":{"role":"user","content":"3"}}""",
            """{"type":"user","message":{"role":"user","content":"4"}}""",
            """{"type":"user","message":{"role":"user","content":"5"}}""");
        var tail = await SessionParser.ParseTailAsync(path, maxLines: 3);
        Assert.Equal(3, tail.Count);
        // Tail contains the last three by source position.
        Assert.All(tail, e => Assert.IsType<UserEntry>(e));
        Assert.Equal("3", ((UserEntry)tail[0]).Message.Content);
        Assert.Equal("5", ((UserEntry)tail[2]).Message.Content);
    }

    [Fact]
    public async Task ParseAllAsync_CustomTitleAndSystemRecognized()
    {
        using var tmp = new TempDir();
        var path = WriteLines(tmp,
            """{"type":"custom-title","customTitle":"demo","sessionId":"s"}""",
            """{"type":"system","subtype":"away_summary","content":"recap text","timestamp":"2026-04-24T10:00:00Z"}""");
        var entries = await SessionParser.ParseAllAsync(path);
        Assert.Equal(2, entries.Count);
        Assert.Equal("demo", Assert.IsType<CustomTitleEntry>(entries[0]).CustomTitle);
        Assert.Equal("away_summary", Assert.IsType<SystemEntry>(entries[1]).Subtype);
    }

    [Fact]
    public async Task ParseAllAsync_UnknownType_BecomesUnknownEntry()
    {
        using var tmp = new TempDir();
        var path = WriteLines(tmp, """{"type":"something-new","foo":"bar"}""");
        var entries = await SessionParser.ParseAllAsync(path);
        Assert.IsType<UnknownEntry>(Assert.Single(entries));
    }

    [Fact]
    public async Task ParseAllAsync_UserContentAsArrayFlattensTextBlocks()
    {
        using var tmp = new TempDir();
        var path = WriteLines(tmp,
            """{"type":"user","message":{"role":"user","content":[{"type":"text","text":"part1"},{"type":"text","text":"part2"}]}}""");
        var u = Assert.IsType<UserEntry>(Assert.Single(await SessionParser.ParseAllAsync(path)));
        Assert.Contains("part1", u.Message.Content);
        Assert.Contains("part2", u.Message.Content);
    }

    [Fact]
    public async Task ParseAllAsync_UserToolResult_SetsIsToolResult()
    {
        using var tmp = new TempDir();
        var path = WriteLines(tmp,
            """{"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"t1","content":"ok"}]}}""");
        var u = Assert.IsType<UserEntry>(Assert.Single(await SessionParser.ParseAllAsync(path)));
        Assert.True(u.Message.IsToolResult);
    }
}
