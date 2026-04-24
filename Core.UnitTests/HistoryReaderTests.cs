using System.Linq;
using System.Threading.Tasks;
using ClaudeMonitor.Core.Sessions;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class HistoryReaderTests
{
    [Fact]
    public async Task ReadAsync_NoFile_ReturnsEmpty()
    {
        using var tmp = new TempDir();
        var reader = new HistoryReader(tmp.Path);
        var entries = await reader.ReadAsync();
        Assert.Empty(entries);
    }

    [Fact]
    public async Task ReadAsync_DeduplicatesBySessionId_KeepsEarliestAndLatest()
    {
        using var tmp = new TempDir();
        var home = tmp.Path;
        var lines = string.Join('\n', new[]
        {
            """{"display":"first","timestamp":1000,"project":"C:\\a","sessionId":"s1"}""",
            """{"display":"middle","timestamp":2000,"project":"C:\\a","sessionId":"s1"}""",
            """{"display":"latest","timestamp":3000,"project":"C:\\a","sessionId":"s1"}""",
            """{"display":"other","timestamp":1500,"project":"C:\\b","sessionId":"s2"}""",
        });
        tmp.WriteFile("history.jsonl", lines);

        var reader = new HistoryReader(home);
        var entries = (await reader.ReadAsync()).ToList();

        Assert.Equal(2, entries.Count);
        var s1 = entries.Single(e => e.Id.Value == "s1");
        Assert.Equal(1000, s1.CreatedAt.ToUnixTimeMilliseconds());
        Assert.Equal(3000, s1.LastActivityAt.ToUnixTimeMilliseconds());
        Assert.Equal("latest", s1.LatestDisplay);
        Assert.Equal(3, s1.MessageCount);
    }

    [Fact]
    public async Task ReadAsync_SortsNewestFirst()
    {
        using var tmp = new TempDir();
        tmp.WriteFile("history.jsonl", string.Join('\n', new[]
        {
            """{"display":"old","timestamp":1000,"project":"C:\\a","sessionId":"old"}""",
            """{"display":"new","timestamp":5000,"project":"C:\\b","sessionId":"new"}""",
        }));
        var entries = (await new HistoryReader(tmp.Path).ReadAsync()).ToList();
        Assert.Equal("new", entries[0].Id.Value);
        Assert.Equal("old", entries[1].Id.Value);
    }

    [Fact]
    public async Task ReadAsync_SkipsMalformedLines()
    {
        using var tmp = new TempDir();
        tmp.WriteFile("history.jsonl", string.Join('\n', new[]
        {
            """{"display":"good","timestamp":1000,"project":"C:\\a","sessionId":"s1"}""",
            "not json",
            """{"missing":"fields"}""",
            """{"display":"","timestamp":2000,"project":"","sessionId":""}""",
        }));
        var entries = (await new HistoryReader(tmp.Path).ReadAsync()).ToList();
        Assert.Single(entries);
        Assert.Equal("s1", entries[0].Id.Value);
    }

    [Fact]
    public async Task ReadAsync_BlankLinesIgnored()
    {
        using var tmp = new TempDir();
        tmp.WriteFile("history.jsonl", "\n\n" +
            """{"display":"x","timestamp":1000,"project":"C:\\a","sessionId":"s1"}""" + "\n\n");
        var entries = (await new HistoryReader(tmp.Path).ReadAsync()).ToList();
        Assert.Single(entries);
    }
}
