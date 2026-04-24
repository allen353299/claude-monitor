using ClaudeMonitor.Core;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class DisplayTextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n ")]
    public void TryFrom_NullOrWhitespace_ReturnsFalse(string? input)
    {
        Assert.False(DisplayText.TryFrom(input, 50, out _));
    }

    [Fact]
    public void TryFrom_StripsCommandNameTag()
    {
        Assert.True(DisplayText.TryFrom("<command-name>/compact</command-name>summary", 100, out var t));
        Assert.Equal("summary", t.Value);
    }

    [Fact]
    public void TryFrom_StripsCommandMessageAndArgsTags()
    {
        var raw = "hello <command-message>run</command-message>world<command-args>-v</command-args>!";
        Assert.True(DisplayText.TryFrom(raw, 100, out var t));
        Assert.Equal("hello world!", t.Value);
    }

    [Fact]
    public void TryFrom_AllTagsStripped_EmptyReturnsFalse()
    {
        Assert.False(DisplayText.TryFrom("<command-name>only tag</command-name>", 50, out _));
    }

    [Fact]
    public void TryFrom_CollapsesInternalWhitespace()
    {
        Assert.True(DisplayText.TryFrom("foo   bar\n\nbaz\tqux", 50, out var t));
        Assert.Equal("foo bar baz qux", t.Value);
    }

    [Fact]
    public void TryFrom_TrimsLeadingTrailingWhitespace()
    {
        Assert.True(DisplayText.TryFrom("   hello   ", 50, out var t));
        Assert.Equal("hello", t.Value);
    }

    [Fact]
    public void TryFrom_ExactLength_NoEllipsis()
    {
        Assert.True(DisplayText.TryFrom("abcdefghij", 10, out var t));
        Assert.Equal("abcdefghij", t.Value);
    }

    [Fact]
    public void TryFrom_OverLength_TruncatesWithEllipsis()
    {
        Assert.True(DisplayText.TryFrom("abcdefghijklmn", 10, out var t));
        Assert.Equal("abcdefghij…", t.Value);
    }

    [Fact]
    public void HasContent_Reflects_Value()
    {
        Assert.True(DisplayText.TryFrom("hello", 50, out var t));
        Assert.True(t.HasContent);
        Assert.False(default(DisplayText).HasContent);
    }

    [Fact]
    public void ToString_DefaultInstance_EmptyString()
    {
        Assert.Equal("", default(DisplayText).ToString());
    }
}
