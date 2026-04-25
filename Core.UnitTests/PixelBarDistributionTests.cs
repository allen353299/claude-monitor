using ClaudeMonitor.Core.Sessions;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class PixelBarDistributionTests
{
    [Fact]
    public void ZeroSessions_AllEmpty()
    {
        var layout = PixelBarDistribution.Distribute(new[] { 0, 0, 0, 0 }, 0, 28);
        Assert.All(layout, c => Assert.Equal(BarColumn.Empty, c));
    }

    [Fact]
    public void OneAttention_AndOneConnecting_LeavesUnusedColumnsEmpty()
    {
        // The original bug: untallied Connecting + default(0)=Attention layout
        // caused unallocated columns to render Attention orange. Connecting now
        // counts as Idle and unfilled defaults to Empty.
        var layout = PixelBarDistribution.Distribute(new[] { 1, 0, 0, 1 }, 2, 28);
        int attention = 0, idle = 0, empty = 0;
        foreach (var c in layout)
        {
            switch (c)
            {
                case BarColumn.Attention: attention++; break;
                case BarColumn.Idle: idle++; break;
                case BarColumn.Empty: empty++; break;
            }
        }
        Assert.Equal(14, attention);
        Assert.Equal(14, idle);
        Assert.Equal(0, empty);
    }

    [Fact]
    public void AllSameStatus_FillsEveryColumn()
    {
        var layout = PixelBarDistribution.Distribute(new[] { 0, 0, 5, 0 }, 5, 28);
        Assert.All(layout, c => Assert.Equal(BarColumn.Working, c));
    }

    [Fact]
    public void MixedStatus_TotalEqualsColumnCount()
    {
        // 1 attention, 1 done, 1 working, 1 idle, total 4, 28 columns
        // each gets 7 columns.
        var layout = PixelBarDistribution.Distribute(new[] { 1, 1, 1, 1 }, 4, 28);
        int attn = 0, done = 0, work = 0, idle = 0;
        foreach (var c in layout)
        {
            if (c == BarColumn.Attention) attn++;
            else if (c == BarColumn.Done) done++;
            else if (c == BarColumn.Working) work++;
            else if (c == BarColumn.Idle) idle++;
        }
        Assert.Equal(7, attn);
        Assert.Equal(7, done);
        Assert.Equal(7, work);
        Assert.Equal(7, idle);
    }

    [Fact]
    public void MoreSessionsThanColumns_StillDistributes()
    {
        var layout = PixelBarDistribution.Distribute(new[] { 50, 0, 0, 50 }, 100, 28);
        // 50/100 * 28 = 14 each
        int attn = 0, idle = 0;
        foreach (var c in layout)
        {
            if (c == BarColumn.Attention) attn++;
            else if (c == BarColumn.Idle) idle++;
        }
        Assert.Equal(14, attn);
        Assert.Equal(14, idle);
    }

    [Fact]
    public void OnlySomeSessionsTallied_RemainderStaysEmpty()
    {
        // 2 sessions but only one tallied (e.g. status not in any category).
        // capacity caps at totalSessions (2) so only 2 columns filled.
        var layout = PixelBarDistribution.Distribute(new[] { 1, 0, 0, 0 }, 2, 28);
        int attn = 0, empty = 0;
        foreach (var c in layout)
        {
            if (c == BarColumn.Attention) attn++;
            else if (c == BarColumn.Empty) empty++;
        }
        Assert.True(attn <= 14);
        Assert.True(empty >= 14);
    }
}
