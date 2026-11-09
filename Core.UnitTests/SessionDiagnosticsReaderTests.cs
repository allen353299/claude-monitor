using System.Linq;
using System.Threading.Tasks;
using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Sessions;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class SessionDiagnosticsReaderTests
{
    [Fact]
    public async Task ReadAsync_NoProjects_Empty()
    {
        using var tmp = new TempDir();
        var report = await new SessionDiagnosticsReader(tmp.Path).ReadAsync();
        Assert.Empty(report);
    }

    [Fact]
    public async Task ReadAsync_CountsWastedReads()
    {
        using var tmp = new TempDir();
        var jsonl = string.Join('\n', new[]
        {
            AssistantTurn(model: "claude-opus-4-7", readPath: @"D:\a.cs"),
            AssistantTurn(model: "claude-opus-4-7", readPath: @"D:\a.cs"),
            AssistantTurn(model: "claude-opus-4-7", readPath: @"D:\a.cs"),
            AssistantTurn(model: "claude-opus-4-7", readPath: @"D:\b.cs"),
        });
        tmp.WriteFile("projects/D--foo/abc.jsonl", jsonl);

        var diag = (await new SessionDiagnosticsReader(tmp.Path).ReadAsync()).Single().Value;
        Assert.Equal(4, diag.ReadCalls);
        Assert.Equal(2, diag.WastedReads);
        Assert.Equal(50.0, diag.ReReadPercent, 1);
    }

    [Fact]
    public async Task ReadAsync_AttributesPromptCostToOpeningUserMessage()
    {
        using var tmp = new TempDir();
        var jsonl = string.Join('\n', new[]
        {
            UserText("plan first"),
            AssistantTurn(model: "claude-opus-4-7", inputTokens: 1_000_000),
            AssistantTurn(model: "claude-opus-4-7", inputTokens: 1_000_000),
            UserText("go on"),
            AssistantTurn(model: "claude-opus-4-7", inputTokens: 1_000_000),
        });
        tmp.WriteFile("projects/D--foo/abc.jsonl", jsonl);

        var diag = (await new SessionDiagnosticsReader(tmp.Path).ReadAsync()).Single().Value;
        // "plan first" block = 2 Opus turns @ 1M input = 2 * $15 = $30; "go on" = $15
        Assert.Equal(30.0, diag.TopPromptUsd, 2);
        Assert.Equal("plan first", diag.TopPromptPreview);
    }

    [Fact]
    public async Task ReadAsync_AggregatesDailyAndPicksPeak()
    {
        using var tmp = new TempDir();
        var jsonl = string.Join('\n', new[]
        {
            AssistantTurn(model: "claude-opus-4-7", inputTokens: 1_000_000, timestamp: "2026-05-12T10:00:00Z"),
            AssistantTurn(model: "claude-opus-4-7", inputTokens: 2_000_000, timestamp: "2026-05-12T11:00:00Z"),
            AssistantTurn(model: "claude-opus-4-7", inputTokens: 1_000_000, timestamp: "2026-05-13T10:00:00Z"),
        });
        tmp.WriteFile("projects/D--foo/abc.jsonl", jsonl);

        var diag = (await new SessionDiagnosticsReader(tmp.Path).ReadAsync()).Single().Value;
        Assert.Equal(2, diag.Daily.Count);
        Assert.Equal("2026-05-12", diag.PeakDay);
        Assert.Equal(45.0, diag.PeakDayUsd, 2); // 3M input * $15/M
    }

    [Fact]
    public async Task ReadAsync_TracksBiggestToolResult()
    {
        using var tmp = new TempDir();
        var small = new string('x', 5 * 1024);
        var big = new string('y', 50 * 1024);
        var jsonl = string.Join('\n', new[]
        {
            AssistantTurn(model: "claude-opus-4-7", inputTokens: 1),
            ToolResultUser(small),
            ToolResultUser(big),
            ToolResultUser(small),
        });
        tmp.WriteFile("projects/D--foo/abc.jsonl", jsonl);

        var diag = (await new SessionDiagnosticsReader(tmp.Path).ReadAsync()).Single().Value;
        Assert.Equal(50 * 1024, diag.BiggestToolResultBytes);
        Assert.Equal(1, diag.LargeToolResultCount); // only the 50KB one exceeds 10KB threshold
    }

    private static string UserText(string text)
        => "{\"type\":\"user\",\"message\":{\"content\":\"" + text + "\"}}";

    private static string ToolResultUser(string content)
        => "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"content\":\"" + content + "\"}]}}";

    private static string AssistantTurn(
        string model,
        long inputTokens = 0,
        long outputTokens = 0,
        long cacheRead = 0,
        long cacheWrite = 0,
        string? readPath = null,
        string? timestamp = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"type\":\"assistant\"");
        if (timestamp is not null) sb.Append(",\"timestamp\":\"").Append(timestamp).Append('"');
        sb.Append(",\"message\":{\"model\":\"").Append(model).Append('"');
        sb.Append(",\"usage\":{\"input_tokens\":").Append(inputTokens)
          .Append(",\"output_tokens\":").Append(outputTokens)
          .Append(",\"cache_creation_input_tokens\":").Append(cacheWrite)
          .Append(",\"cache_read_input_tokens\":").Append(cacheRead).Append('}');
        if (readPath is not null)
        {
            var escaped = readPath.Replace("\\", "\\\\");
            sb.Append(",\"content\":[{\"type\":\"tool_use\",\"name\":\"Read\",\"input\":{\"file_path\":\"")
              .Append(escaped).Append("\"}}]");
        }
        sb.Append("}}");
        return sb.ToString();
    }
}
