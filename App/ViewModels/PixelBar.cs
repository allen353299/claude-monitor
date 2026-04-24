using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ClaudeMonitor.App.Theme;
using ClaudeMonitor.Core.Models;

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
        var layout = DistributeColumns(counts, sessions.Count);

        for (int i = 0; i < ColumnCount; i++)
        {
            Blocks[i].Color = layout[i] switch
            {
                ColumnKind.Attention => StatusPalette.Attention,
                ColumnKind.Done => StatusPalette.Done,
                ColumnKind.Working => StatusPalette.Working,
                ColumnKind.Idle => StatusPalette.Idle,
                _ => StatusPalette.PixelEmpty,
            };
        }
    }

    private static int[] TallyByStatus(IEnumerable<SessionVm> sessions)
    {
        var counts = new int[4];
        foreach (var s in sessions)
        {
            switch (s.Status)
            {
                case SessionStatus.NeedsAttention: counts[(int)ColumnKind.Attention]++; break;
                case SessionStatus.Working: counts[(int)ColumnKind.Working]++; break;
                case SessionStatus.WaitingForInput:
                    counts[(int)(s.IsDoneRecent ? ColumnKind.Done : ColumnKind.Idle)]++;
                    break;
            }
        }
        return counts;
    }

    private static ColumnKind[] DistributeColumns(int[] counts, int sessionCount)
    {
        var layout = new ColumnKind[ColumnCount];
        if (sessionCount == 0) return layout;

        var percentages = counts.Select(c => (double)c / sessionCount * ColumnCount).ToArray();
        var allocated = percentages.Select(p => (int)System.Math.Floor(p)).ToArray();
        var remainders = percentages.Select((p, i) => p - allocated[i]).ToArray();

        int totalAllocated = allocated.Sum();
        while (totalAllocated < ColumnCount)
        {
            int bestIdx = 0;
            double bestRemainder = remainders[0];
            for (int i = 1; i < remainders.Length; i++)
            {
                if (remainders[i] > bestRemainder)
                {
                    bestRemainder = remainders[i];
                    bestIdx = i;
                }
            }
            if (bestRemainder < 0) break;
            allocated[bestIdx]++;
            remainders[bestIdx] = -1;
            totalAllocated++;
        }

        int cursor = 0;
        for (int kind = 0; kind < allocated.Length; kind++)
        {
            for (int j = 0; j < allocated[kind] && cursor < ColumnCount; j++)
            {
                layout[cursor++] = (ColumnKind)kind;
            }
        }
        return layout;
    }

    private enum ColumnKind
    {
        Attention = 0,
        Done = 1,
        Working = 2,
        Idle = 3,
        Empty = 4,
    }
}
