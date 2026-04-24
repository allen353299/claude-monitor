using ClaudeMonitor.Core.Models;

namespace ClaudeMonitor.Core.Sessions;

public enum AttentionTrigger
{
    None,
    NeedsAttention,
    TaskComplete,
}

public static class StatusTransition
{
    public static AttentionTrigger Decide(SessionStatus? previous, SessionStatus current)
    {
        if (!previous.HasValue) return AttentionTrigger.None;
        if (previous.Value == current) return AttentionTrigger.None;
        if (current == SessionStatus.NeedsAttention) return AttentionTrigger.NeedsAttention;
        if (current == SessionStatus.WaitingForInput && previous.Value == SessionStatus.Working)
            return AttentionTrigger.TaskComplete;
        return AttentionTrigger.None;
    }
}
