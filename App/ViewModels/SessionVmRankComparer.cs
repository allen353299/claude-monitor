using System.Collections.Generic;
using ClaudeMonitor.Core.Models;

namespace ClaudeMonitor.App.ViewModels;

public sealed class SessionVmRankComparer : IComparer<SessionVm>
{
    public static SessionVmRankComparer Default { get; } = new();

    public int Compare(SessionVm? x, SessionVm? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return 1;
        if (y is null) return -1;

        var rankDelta = Rank(x) - Rank(y);
        if (rankDelta != 0) return rankDelta;
        return y.LastActivity.CompareTo(x.LastActivity);
    }

    private static int Rank(SessionVm v)
    {
        if (v.Status == SessionStatus.NeedsAttention) return 0;
        if (v.Status == SessionStatus.WaitingForInput && v.IsDoneRecent) return 1;
        if (v.Status == SessionStatus.Working) return 2;
        if (v.Status == SessionStatus.WaitingForInput) return 3;
        return 4;
    }
}
