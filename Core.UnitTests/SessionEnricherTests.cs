using System.Collections.Generic;
using ClaudeMonitor.Core.Models;
using ClaudeMonitor.Core.Sessions;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class SessionEnricherTests
{
    private static readonly SessionEnricher Enricher = new();

    private static UserEntry User(string content, bool isToolResult = false) =>
        new() { Base = new SessionEntryBase(), Message = new UserMessage { Content = content, IsToolResult = isToolResult } };

    private static AssistantEntry Assistant(params MessageContent[] blocks)
    {
        var entry = new AssistantEntry { Base = new SessionEntryBase() };
        foreach (var b in blocks) entry.Message.Content.Add(b);
        return entry;
    }

    [Fact]
    public void Enrich_Empty_AllNullZero()
    {
        var result = Enricher.Enrich(new List<SessionEntry>());
        Assert.Null(result.FirstPrompt);
        Assert.Null(result.LatestMessage);
        Assert.Null(result.Recap);
        Assert.Null(result.CustomTitle);
        Assert.Equal(0, result.MessageCount);
    }

    [Fact]
    public void FirstPrompt_UsesFirstUserNonToolResult()
    {
        var entries = new List<SessionEntry>
        {
            User("tool result payload", isToolResult: true),
            User("real first question"),
            User("second question"),
        };
        var result = Enricher.Enrich(entries);
        Assert.Equal("real first question", result.FirstPrompt?.Value);
    }

    [Fact]
    public void LatestMessage_PrefersAssistantText()
    {
        var entries = new List<SessionEntry>
        {
            User("hi"),
            Assistant(new TextContent { Text = "assistant reply" }),
        };
        var result = Enricher.Enrich(entries);
        Assert.Equal("assistant reply", result.LatestMessage?.Value);
    }

    [Fact]
    public void LatestMessage_FallsBackToToolUseName_WhenNoText()
    {
        var entries = new List<SessionEntry>
        {
            User("hi"),
            Assistant(new ToolUseContent { Name = "Read" }),
        };
        var result = Enricher.Enrich(entries);
        Assert.Equal("Executing Read...", result.LatestMessage?.Value);
    }

    [Fact]
    public void LatestMessage_FallsBackToThinking_WhenNoTextOrTool()
    {
        var entries = new List<SessionEntry>
        {
            Assistant(new ThinkingContent { Thinking = "pondering" }),
        };
        var result = Enricher.Enrich(entries);
        Assert.Equal("pondering", result.LatestMessage?.Value);
    }

    [Fact]
    public void Recap_PicksLastAwaySummary()
    {
        var entries = new List<SessionEntry>
        {
            new SystemEntry { Subtype = "other", Content = "ignore" },
            new SystemEntry { Subtype = "away_summary", Content = "older recap" },
            new SystemEntry { Subtype = "away_summary", Content = "newer recap" },
        };
        var result = Enricher.Enrich(entries);
        Assert.Equal("newer recap", result.Recap?.Value);
    }

    [Fact]
    public void CustomTitle_PicksLast()
    {
        var entries = new List<SessionEntry>
        {
            new CustomTitleEntry { CustomTitle = "first title", SessionId = "s" },
            new CustomTitleEntry { CustomTitle = "final title", SessionId = "s" },
        };
        var result = Enricher.Enrich(entries);
        Assert.Equal("final title", result.CustomTitle?.Value);
    }

    [Fact]
    public void MessageCount_CountsUserAndAssistantOnly()
    {
        var entries = new List<SessionEntry>
        {
            User("a"),
            Assistant(new TextContent { Text = "b" }),
            new SystemEntry(),
            new CustomTitleEntry(),
            new SummaryEntry(),
            User("c", isToolResult: true), // tool result still a UserEntry, counted
        };
        Assert.Equal(3, Enricher.Enrich(entries).MessageCount);
    }

    [Fact]
    public void FirstPrompt_SkipsEmptyUserContent()
    {
        var entries = new List<SessionEntry>
        {
            User(""),
            User("   "),
            User("real content"),
        };
        Assert.Equal("real content", Enricher.Enrich(entries).FirstPrompt?.Value);
    }
}
