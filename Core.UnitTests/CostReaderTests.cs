using System.Linq;
using System.Threading.Tasks;
using ClaudeMonitor.Core.Sessions;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class CostReaderTests
{
    [Fact]
    public async Task ReadAsync_NoProjects_EmptyReport()
    {
        using var tmp = new TempDir();
        var report = await new CostReader(new ProjectPathDecoder(), tmp.Path).ReadAsync();
        Assert.Empty(report.Sessions);
        Assert.Equal(0, report.GrandTotalUsd);
    }

    [Fact]
    public async Task ReadAsync_AccumulatesAssistantUsage()
    {
        using var tmp = new TempDir();
        var jsonl = string.Join('\n', new[]
        {
            """{"type":"assistant","message":{"model":"claude-sonnet-4-6","usage":{"input_tokens":1000,"output_tokens":2000,"cache_creation_input_tokens":0,"cache_read_input_tokens":500}}}""",
            """{"type":"assistant","message":{"model":"claude-sonnet-4-6","usage":{"input_tokens":500,"output_tokens":800,"cache_creation_input_tokens":200,"cache_read_input_tokens":100}}}""",
        });
        tmp.WriteFile("projects/D--foo/abc.jsonl", jsonl);

        var report = await new CostReader(new ProjectPathDecoder(), tmp.Path).ReadAsync();
        var cost = report.Sessions.Single();

        Assert.Equal(1500, cost.InputTokens);
        Assert.Equal(2800, cost.OutputTokens);
        Assert.Equal(200, cost.CacheWriteTokens);
        Assert.Equal(600, cost.CacheReadTokens);
        Assert.Equal("claude-sonnet-4-6", cost.Model);
        // Sonnet pricing: 3/15/3.75/0.30 per million
        var expected = (1500 * 3.0 + 2800 * 15.0 + 200 * 3.75 + 600 * 0.30) / 1_000_000.0;
        Assert.Equal(expected, cost.UsdTotal, 6);
        Assert.Equal(expected, report.GrandTotalUsd, 6);
    }

    [Fact]
    public async Task ReadAsync_SkipsFilesWithZeroTokens()
    {
        using var tmp = new TempDir();
        tmp.WriteFile("projects/D--foo/abc.jsonl", "");
        tmp.WriteFile("projects/D--foo/def.jsonl",
            """{"type":"user","message":{"content":"hi"}}""");
        var report = await new CostReader(new ProjectPathDecoder(), tmp.Path).ReadAsync();
        Assert.Empty(report.Sessions);
    }

    [Fact]
    public async Task ReadAsync_SortsByUsdDescending()
    {
        using var tmp = new TempDir();
        tmp.WriteFile("projects/D--a/low.jsonl",
            """{"type":"assistant","message":{"model":"claude-haiku-4-5","usage":{"input_tokens":100,"output_tokens":100,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}}""");
        tmp.WriteFile("projects/D--b/high.jsonl",
            """{"type":"assistant","message":{"model":"claude-opus-4-6","usage":{"input_tokens":10000,"output_tokens":10000,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}}""");

        var report = await new CostReader(new ProjectPathDecoder(), tmp.Path).ReadAsync();
        Assert.Equal(2, report.Sessions.Count);
        Assert.True(report.Sessions[0].UsdTotal > report.Sessions[1].UsdTotal);
        Assert.Equal("high", report.Sessions[0].Id.Value);
    }

    [Fact]
    public async Task ReadAsync_UnknownModel_ZeroCost()
    {
        using var tmp = new TempDir();
        tmp.WriteFile("projects/D--foo/x.jsonl",
            """{"type":"assistant","message":{"model":"gpt-4o","usage":{"input_tokens":1000,"output_tokens":1000,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}}""");
        var report = await new CostReader(new ProjectPathDecoder(), tmp.Path).ReadAsync();
        var cost = report.Sessions.Single();
        Assert.Equal("gpt-4o", cost.Model);
        Assert.Equal(0, cost.UsdTotal);
        Assert.Equal(1000, cost.InputTokens);
    }

    [Fact]
    public async Task ReadAsync_LatestTimestampBecomesLastActivity()
    {
        using var tmp = new TempDir();
        tmp.WriteFile("projects/D--foo/x.jsonl", string.Join('\n', new[]
        {
            """{"type":"assistant","timestamp":"2026-04-20T10:00:00Z","message":{"model":"claude-sonnet-4-6","usage":{"input_tokens":100,"output_tokens":100,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}}""",
            """{"type":"assistant","timestamp":"2026-04-22T10:00:00Z","message":{"model":"claude-sonnet-4-6","usage":{"input_tokens":100,"output_tokens":100,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}}""",
            """{"type":"assistant","timestamp":"2026-04-21T10:00:00Z","message":{"model":"claude-sonnet-4-6","usage":{"input_tokens":100,"output_tokens":100,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}}""",
        }));
        var cost = (await new CostReader(new ProjectPathDecoder(), tmp.Path).ReadAsync()).Sessions.Single();
        Assert.Equal(new System.DateTimeOffset(2026, 4, 22, 10, 0, 0, System.TimeSpan.Zero), cost.LastActivity);
    }
}
