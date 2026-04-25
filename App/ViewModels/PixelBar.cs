using System.Collections.Generic;
using System.Collections.ObjectModel;
using ClaudeMonitor.App.Theme;
using ClaudeMonitor.Core.Models;
using ClaudeMonitor.Core.Sessions;

namespace ClaudeMonitor.App.ViewModels;

public sealed class PixelBar
{
    private const int ColumnCount = 28;

    public ObservableCollection<PixelBlock> Blocks { get; } = new();

    public PixelBar()
    {
        for (int i = 0; i < ColumnCount; i++)
        {
            Blocks.Add(new PixelBlock { Color = StatusPalette.PixelEmpty });
        }
    }

    public void Refresh(IReadOnlyCollection<SessionVm> sessions)
    {
        var counts = TallyByStatus(sessions);
        var layout = PixelBarDistribution.Distribute(counts, sessions.Count, ColumnCount);

        for (int i = 0; i < ColumnCount; i++)
        {
            Blocks[i].Color = layout[i] switch
            {
                BarColumn.Attention => StatusPalette.Attention,
                BarColumn.Done => StatusPalette.Done,
                BarColumn.Working => StatusPalette.Working,
                BarColumn.Idle => StatusPalette.Idle,
                _ => StatusPalette.PixelEmpty,
            };
        }
    }

    private static int[] TallyByStatus(IEnumerable<SessionVm> sessions)
    {
        var counts = new int[PixelBarDistribution.CategoryCount];
        foreach (var s in sessions)
        {
            switch (s.Status)
            {
                case SessionStatus.NeedsAttention:
                    counts[(int)BarColumn.Attention]++;
                    break;
                case SessionStatus.Working:
                    counts[(int)BarColumn.Working]++;
                    break;
                case SessionStatus.WaitingForInput:
                    counts[(int)(s.IsDoneRecent ? BarColumn.Done : BarColumn.Idle)]++;
                    break;
                case SessionStatus.Connecting:
                    counts[(int)BarColumn.Idle]++;
                    break;
            }
        }
        return counts;
    }
}
