using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using ClaudeMonitor.App.Theme;
using ClaudeMonitor.Core.Models;
using ClaudeMonitor.Core.Sessions;

namespace ClaudeMonitor.App.ViewModels;

public sealed class PixelBar
{
    private const int ColumnCount = 28;
    private static readonly TimeSpan WaveStep = TimeSpan.FromMilliseconds(150);

    public ObservableCollection<PixelBlock> Blocks { get; } = new();

    private readonly DispatcherTimer _waveTimer;
    private readonly List<int> _workingIndices = new();
    private int _waveCursor;
    private int _previouslyLit = -1;

    public PixelBar()
    {
        for (int i = 0; i < ColumnCount; i++)
        {
            Blocks.Add(new PixelBlock { Color = StatusPalette.PixelEmpty });
        }

        _waveTimer = new DispatcherTimer(WaveStep, DispatcherPriority.Background, OnWaveTick);
        _waveTimer.Stop();
    }

    public void Refresh(IReadOnlyCollection<SessionVm> sessions)
    {
        var counts = TallyByStatus(sessions);
        var layout = PixelBarDistribution.Distribute(counts, sessions.Count, ColumnCount);

        _workingIndices.Clear();
        for (int i = 0; i < ColumnCount; i++)
        {
            var kind = layout[i];
            var block = Blocks[i];
            block.Color = kind switch
            {
                BarColumn.Attention => StatusPalette.Attention,
                BarColumn.Done => StatusPalette.Done,
                BarColumn.Working => StatusPalette.Working,
                BarColumn.Idle => StatusPalette.Idle,
                _ => StatusPalette.PixelEmpty,
            };
            var working = kind == BarColumn.Working;
            block.IsWorking = working;
            block.IsLit = false;
            if (working) _workingIndices.Add(i);
        }

        _previouslyLit = -1;
        _waveCursor = 0;
        if (_workingIndices.Count > 0) _waveTimer.Start();
        else _waveTimer.Stop();
    }

    private void OnWaveTick(object? sender, EventArgs e)
    {
        if (_workingIndices.Count == 0)
        {
            _waveTimer.Stop();
            return;
        }
        if (_previouslyLit >= 0 && _previouslyLit < Blocks.Count)
        {
            Blocks[_previouslyLit].IsLit = false;
        }
        var currentIdx = _workingIndices[_waveCursor % _workingIndices.Count];
        Blocks[currentIdx].IsLit = true;
        _previouslyLit = currentIdx;
        _waveCursor = (_waveCursor + 1) % _workingIndices.Count;
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
