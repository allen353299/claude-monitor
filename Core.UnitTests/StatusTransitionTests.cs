using ClaudeMonitor.Core.Models;
using ClaudeMonitor.Core.Sessions;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class StatusTransitionTests
{
    [Theory]
    [InlineData(SessionStatus.NeedsAttention)]
    [InlineData(SessionStatus.WaitingForInput)]
    [InlineData(SessionStatus.Working)]
    [InlineData(SessionStatus.Connecting)]
    public void FirstSighting_NeverFires(SessionStatus current)
    {
        Assert.Equal(AttentionTrigger.None, StatusTransition.Decide(previous: null, current));
    }

    [Theory]
    [InlineData(SessionStatus.Working)]
    [InlineData(SessionStatus.NeedsAttention)]
    [InlineData(SessionStatus.WaitingForInput)]
    public void SameStatus_DoesNotFire(SessionStatus same)
    {
        Assert.Equal(AttentionTrigger.None, StatusTransition.Decide(same, same));
    }

    [Theory]
    [InlineData(SessionStatus.Working)]
    [InlineData(SessionStatus.WaitingForInput)]
    [InlineData(SessionStatus.Connecting)]
    public void AnyNonAttention_ToNeedsAttention_FiresNeedsAttention(SessionStatus previous)
    {
        Assert.Equal(AttentionTrigger.NeedsAttention,
            StatusTransition.Decide(previous, SessionStatus.NeedsAttention));
    }

    [Fact]
    public void Working_ToWaitingForInput_FiresTaskComplete()
    {
        Assert.Equal(AttentionTrigger.TaskComplete,
            StatusTransition.Decide(SessionStatus.Working, SessionStatus.WaitingForInput));
    }

    [Fact]
    public void NeedsAttention_ToWaitingForInput_DoesNotFireDone()
    {
        // Only Working -> WaitingForInput counts as "task complete".
        Assert.Equal(AttentionTrigger.None,
            StatusTransition.Decide(SessionStatus.NeedsAttention, SessionStatus.WaitingForInput));
    }

    [Fact]
    public void Connecting_ToWorking_DoesNotFire()
    {
        Assert.Equal(AttentionTrigger.None,
            StatusTransition.Decide(SessionStatus.Connecting, SessionStatus.Working));
    }
}
