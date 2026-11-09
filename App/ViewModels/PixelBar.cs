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
    private const int InitialColumnCount = 28;
    private const double TargetCycleMs = 720;
    private const double MinStepMs = 30;
    private const double MaxStepMs = 250;
    private static readonly TimeSpan InitialWaveStep = TimeSpan.FromMilliseconds(80);

    public ObservableCollection<PixelBlock> Blocks { get; } = new();

    private readonly DispatcherTimer _waveTimer;
    private readonly List<int> _workingIndices = new();
    private int _waveCursor;
    private int _columnCount = InitialColumnCount;
    private IReadOnlyCollection<SessionVm>? _lastSessions;

    public PixelBar()
    {
        ResizeBlocks(InitialColumnCount);
        _waveTimer = new DispatcherTimer(InitialWaveStep, DispatcherPriority.Background, OnWaveTick);
        _waveTimer.Stop();
    }

    public void SetColumnCount(int count)
    {
        if (count < 1) count = 1;
        if (count == _columnCount) return;
        _columnCount = count;
        ResizeBlocks(count);
        if (_lastSessions is not null) Refresh(_lastSessions);
    }

    public void Refresh(IReadOnlyCollection<SessionVm> sessions)
    {
        _lastSessions = sessions;
        var counts = TallyByStatus(sessions);
        var layout = PixelBarDistribution.Distribute(counts, sessions.Count, _columnCount);

        _workingIndices.Clear();
        for (int i = 0; i < _columnCount; i++)
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
            block.IsLit2 = false;
            block.IsLit3 = false;
            if (working) _workingIndices.Add(i);
        }

        _waveCursor = 0;
        if (_workingIndices.Count > 0)
        {
            var stepMs = Math.Clamp(TargetCycleMs / _workingIndices.Count, MinStepMs, MaxStepMs);
            _waveTimer.Interval = TimeSpan.FromMilliseconds(stepMs);
            _waveTimer.Start();
        }
        else _waveTimer.Stop();
    }

    private void ResizeBlocks(int count)
    {
        while (Blocks.Count > count) Blocks.RemoveAt(Blocks.Count - 1);
        while (Blocks.Count < count) Blocks.Add(new PixelBlock { Color = StatusPalette.PixelEmpty });
    }

    private void OnWaveTick(object? sender, EventArgs e)
    {
        if (_workingIndices.Count == 0)
        {
            _waveTimer.Stop();
            return;
        }

        foreach (var idx in _workingIndices)
        {
            var b = Blocks[idx];
            b.IsLit = false;
            b.IsLit2 = false;
            b.IsLit3 = false;
        }

        var n = _workingIndices.Count;
        var head = _workingIndices[_waveCursor % n];
        Blocks[head].IsLit = true;
        if (n > 1) Blocks[_workingIndices[((_waveCursor - 1) % n + n) % n]].IsLit2 = true;
        if (n > 2) Blocks[_workingIndices[((_waveCursor - 2) % n + n) % n]].IsLit3 = true;

        _waveCursor = (_waveCursor + 1) % n;
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
