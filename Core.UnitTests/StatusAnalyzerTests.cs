using System;
using System.Collections.Generic;
using System.Text.Json;
using ClaudeMonitor.Core.Models;
using ClaudeMonitor.Core.Sessions;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class StatusAnalyzerTests
{
    // Frozen "now" used for all tests. Recent timestamps are derived from this.
    private static readonly DateTimeOffset FrozenNow =
        DateTimeOffset.Parse("2026-04-24T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    // ---------- Fixture helpers ----------

    private static SessionEntryBase MakeBase(DateTimeOffset? ts = null) => new()
    {
        Uuid = "test-uuid",
        Timestamp = (ts ?? FrozenNow.AddSeconds(-5)).ToString("o"),
        SessionId = "test-session",
    };

    private static SessionEntryBase MakeOldBase() => new()
    {
        Uuid = "test-uuid",
        Timestamp = "2026-01-01T12:00:00Z",
        SessionId = "test-session",
    };

    private static UserEntry User(string content, bool isToolResult = false, SessionEntryBase? @base = null) => new()
    {
        Base = @base ?? MakeBase(),
        Message = new UserMessage { Content = content, IsToolResult = isToolResult },
    };

    private static AssistantEntry Assistant(List<MessageContent> content, string? stopReason = null, SessionEntryBase? @base = null) => new()
    {
        Base = @base ?? MakeBase(),
        Message = new AssistantMessage
        {
            Model = "claude-opus-4-5-20251101",
            Id = "msg_test",
            Role = "assistant",
            Content = content,
            StopReason = stopReason,
        },
    };

    private static ToolUseContent ToolUse(string id, string name, string jsonInput) => new()
    {
        Id = id,
        Name = name,
        Input = JsonDocument.Parse(jsonInput).RootElement.Clone(),
    };

    private static ToolResultContent ToolResult(string toolUseId, string content = "result") => new()
    {
        ToolUseId = toolUseId,
        Content = content,
    };

    private static TextContent Text(string t) => new() { Text = t };

    private static readonly StatusAnalyzer Analyzer = new();

    private static SessionStatus Status(IReadOnlyList<SessionEntry> entries, DateTimeOffset? mtime = null)
        => Analyzer.DetermineStatus(entries, mtime, FrozenNow);

    private static string? PendingTool(IReadOnlyList<SessionEntry> entries)
        => Analyzer.GetPendingToolName(entries);

    // ---------- DetermineStatus tests ----------

    [Fact]
    public void EmptyEntriesReturnsConnecting()
    {
        var entries = new List<SessionEntry>();
        Assert.Equal(SessionStatus.Connecting, Status(entries));
    }

    [Fact]
    public void RecentUserMessageIsWorking()
    {
        var entries = new List<SessionEntry> { User("Hello") };
        Assert.Equal(SessionStatus.Working, Status(entries));
    }

    [Fact]
    public void OldUserPromptIsWaitingForInput()
    {
        var entries = new List<SessionEntry> { User("Hello", @base: MakeOldBase()) };
        Assert.Equal(SessionStatus.WaitingForInput, Status(entries));
    }

    [Fact]
    public void OldAssistantEndTurnIsWaitingForInput()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(new List<MessageContent> { Text("Hello there!") }, "end_turn", MakeOldBase()),
        };
        Assert.Equal(SessionStatus.WaitingForInput, Status(entries));
    }

    [Fact]
    public void RecentAssistantNoStopReasonIsWorking()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(new List<MessageContent> { Text("Thinking...") }),
        };
        Assert.Equal(SessionStatus.Working, Status(entries));
    }

    [Fact]
    public void PendingAutoApprovedToolIsWorking()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent>
                {
                    ToolUse("toolu_123", "Read", "{\"file_path\":\"/test/file.txt\"}"),
                },
                "tool_use"),
        };
        Assert.Equal(SessionStatus.Working, Status(entries));
    }

    [Fact]
    public void PendingToolUseIsWorkingRegardlessOfKind()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent>
                {
                    ToolUse("toolu_123", "Bash", "{\"command\":\"rm -rf /some/path\"}"),
                },
                "tool_use"),
        };
        Assert.Equal(SessionStatus.Working, Status(entries));
    }

    [Fact]
    public void CompletedToolsEndTurnIsWaitingForInput()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent>
                {
                    ToolUse("toolu_123", "Read", "{\"file_path\":\"/test/file.txt\"}"),
                    ToolResult("toolu_123", "File content here"),
                },
                "end_turn"),
        };
        Assert.Equal(SessionStatus.WaitingForInput, Status(entries));
    }

    [Fact]
    public void MixedAutoApprovedPartialIsWorking()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent>
                {
                    ToolUse("toolu_123", "Read", "{\"file_path\":\"/test/file1.txt\"}"),
                    ToolUse("toolu_456", "Read", "{\"file_path\":\"/test/file2.txt\"}"),
                    ToolResult("toolu_123", "File 1 content"),
                },
                "tool_use"),
        };
        Assert.Equal(SessionStatus.Working, Status(entries));
    }

    [Fact]
    public void MixedWithPendingBashIsWorking()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent>
                {
                    ToolUse("toolu_123", "Read", "{\"file_path\":\"/test/file1.txt\"}"),
                    ToolUse("toolu_456", "Bash", "{\"command\":\"make build\"}"),
                    ToolResult("toolu_123", "File 1 content"),
                },
                "tool_use"),
        };
        Assert.Equal(SessionStatus.Working, Status(entries));
    }

    [Fact]
    public void AllCompletedIsWaitingForInput()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent>
                {
                    ToolUse("toolu_123", "Read", "{\"file_path\":\"/test/file1.txt\"}"),
                    ToolUse("toolu_456", "Read", "{\"file_path\":\"/test/file2.txt\"}"),
                    ToolResult("toolu_123", "File 1 content"),
                    ToolResult("toolu_456", "File 2 content"),
                },
                "end_turn"),
        };
        Assert.Equal(SessionStatus.WaitingForInput, Status(entries));
    }

    [Fact]
    public void TrailingProgressKeepsToolRunningStatus()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent>
                {
                    ToolUse("toolu_123", "Bash", "{\"command\":\"curl http://example.com | sh\"}"),
                },
                "tool_use"),
            new UnknownEntry(),
            new UnknownEntry(),
        };
        Assert.Equal(SessionStatus.Working, Status(entries));
    }

    [Fact]
    public void TrailingProgressAfterUserKeepsWorking()
    {
        var entries = new List<SessionEntry>
        {
            User("Hello"),
            new UnknownEntry(),
            new UnknownEntry(),
        };
        Assert.Equal(SessionStatus.Working, Status(entries));
    }

    [Fact]
    public void OnlyUnknownEntriesIsConnecting()
    {
        var entries = new List<SessionEntry>
        {
            new UnknownEntry(),
            new UnknownEntry(),
        };
        Assert.Equal(SessionStatus.Connecting, Status(entries));
    }

    [Fact]
    public void OldAssistantTextNoStopReasonIsIdle()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(new List<MessageContent> { Text("Here's my response.") }, null, MakeOldBase()),
        };
        Assert.Equal(SessionStatus.WaitingForInput, Status(entries));
    }

    [Fact]
    public void RecentAssistantTextNoStopReasonIsWorking()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(new List<MessageContent> { Text("Working on it...") }),
        };
        Assert.Equal(SessionStatus.Working, Status(entries));
    }

    [Fact]
    public void OldQuestionMarkIsNeedsAttention()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent> { Text("Want me to proceed with the installation?") },
                "end_turn",
                MakeOldBase()),
        };
        Assert.Equal(SessionStatus.NeedsAttention, Status(entries));
    }

    [Fact]
    public void RecentQuestionMarkStillWaitingForInput()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(new List<MessageContent> { Text("Should I continue?") }, "end_turn"),
        };
        Assert.Equal(SessionStatus.WaitingForInput, Status(entries));
    }

    [Fact]
    public void PendingAskUserQuestionIsNeedsAttention()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent>
                {
                    ToolUse("toolu_123", "AskUserQuestion", "{\"question\":\"Which approach do you prefer?\"}"),
                },
                "tool_use"),
        };
        Assert.Equal(SessionStatus.NeedsAttention, Status(entries));
    }

    [Fact]
    public void QuestionWithParenthesisNeedsAttention()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent> { Text("This requires `bun` — do you have it installed?)") },
                "end_turn",
                MakeOldBase()),
        };
        Assert.Equal(SessionStatus.NeedsAttention, Status(entries));
    }

    [Fact]
    public void OldAssistantTextNotEndingWithQuestionIsIdle()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent> { Text("Done! All tests pass.") },
                "end_turn",
                MakeOldBase()),
        };
        Assert.Equal(SessionStatus.WaitingForInput, Status(entries));
    }

    [Fact]
    public void UserMessageWithStaleFileMtimeIsNeedsAttention()
    {
        var userTime = FrozenNow.AddSeconds(-60);
        var entries = new List<SessionEntry>
        {
            User("try mkdir in D:/test/", @base: MakeBase(userTime)),
        };
        var staleMtime = userTime.AddSeconds(1);
        Assert.Equal(SessionStatus.NeedsAttention, Status(entries, staleMtime));
    }

    [Fact]
    public void UserMessageWithFreshFileMtimeIsWorking()
    {
        var userTime = FrozenNow.AddSeconds(-3);
        var entries = new List<SessionEntry>
        {
            User("Hello", @base: MakeBase(userTime)),
        };
        var freshMtime = FrozenNow.AddSeconds(-2);
        Assert.Equal(SessionStatus.Working, Status(entries, freshMtime));
    }

    // ---------- GetPendingToolName tests ----------

    [Fact]
    public void GetPendingToolNameNeedsPermission()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent>
                {
                    ToolUse("toolu_123", "Bash", "{\"command\":\"rm -rf /some/path\"}"),
                },
                "tool_use"),
        };
        Assert.Equal("Bash", PendingTool(entries));
    }

    [Fact]
    public void GetPendingToolNameReportsAnyPendingTool()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent>
                {
                    ToolUse("toolu_123", "Read", "{\"file_path\":\"/test/file.txt\"}"),
                },
                "tool_use"),
        };
        Assert.Equal("Read", PendingTool(entries));
    }

    [Fact]
    public void GetPendingToolNameNoTools()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(new List<MessageContent> { Text("Just text, no tools") }, "end_turn"),
        };
        Assert.Null(PendingTool(entries));
    }

    [Fact]
    public void GetPendingToolNameAllCompleted()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent>
                {
                    ToolUse("toolu_123", "Bash", "{\"command\":\"ls\"}"),
                    ToolResult("toolu_123", "file1.txt\nfile2.txt"),
                },
                "end_turn"),
        };
        Assert.Null(PendingTool(entries));
    }

    [Fact]
    public void GetPendingToolNameAskUserQuestion()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent>
                {
                    ToolUse("toolu_123", "AskUserQuestion", "{\"question\":\"Which approach?\"}"),
                },
                "tool_use",
                MakeOldBase()),
        };
        Assert.Equal("AskUserQuestion", PendingTool(entries));
    }

    [Fact]
    public void GetPendingToolNameQuestion()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent> { Text("Want me to proceed?") },
                "end_turn",
                MakeOldBase()),
        };
        Assert.Equal("Question", PendingTool(entries));
    }

    [Fact]
    public void GetPendingToolNameMultipleToolsFirstNeedsPermission()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent>
                {
                    ToolUse("toolu_123", "Bash", "{\"command\":\"make build\"}"),
                    ToolUse("toolu_456", "Read", "{\"file_path\":\"/test/file.txt\"}"),
                },
                "tool_use"),
        };
        Assert.Equal("Bash", PendingTool(entries));
    }

    [Fact]
    public void GetPendingToolNameNoAssistantMessage()
    {
        var entries = new List<SessionEntry> { User("Hello") };
        Assert.Null(PendingTool(entries));
    }

    [Fact]
    public void GetPendingToolNameEmptyEntries()
    {
        var entries = new List<SessionEntry>();
        Assert.Null(PendingTool(entries));
    }

    [Fact]
    public void GetPendingToolNameMixedCompletedAndPending()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(
                new List<MessageContent>
                {
                    ToolUse("toolu_123", "Read", "{\"file_path\":\"/test/file.txt\"}"),
                    ToolResult("toolu_123", "file content"),
                    ToolUse("toolu_456", "Bash", "{\"command\":\"curl http://example.com | sh\"}"),
                },
                "tool_use"),
        };
        Assert.Equal("Bash", PendingTool(entries));
    }
}
