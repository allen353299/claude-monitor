using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using ClaudeMonitor.App.Theme;
using ClaudeMonitor.Core.Models;
using ClaudeMonitor.Core.Platform;
using ClaudeMonitor.Core.Sessions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeMonitor.App.ViewModels;

public sealed class PopoverViewModel : ObservableObject, IAsyncDisposable
{
    private const int PixelColumns = 28;
    private static readonly TimeSpan TimeBasedPropertyRefreshInterval = TimeSpan.FromMinutes(1);

    private readonly ISessionScanner _scanner;
    private readonly ISessionActivator _activator;
    private readonly DispatcherTimer _ageTimer;

    public ObservableCollection<SessionVm> Sessions { get; } = new();

    public ObservableCollection<PixelBlock> PixelBlocks { get; } = new();

    public string SessionCountText => $"{Sessions.Count} SESSION{(Sessions.Count == 1 ? "" : "S")}";

    public bool IsEmpty => Sessions.Count == 0;

    public IAsyncRelayCommand<string> ActivateSessionCommand { get; }

    public PopoverViewModel(ISessionScanner scanner, ISessionActivator activator)
    {
        _scanner = scanner;
        _activator = activator;
        ActivateSessionCommand = new AsyncRelayCommand<string>(ActivateSessionAsync);

        _scanner.SessionUpdated += OnSessionUpdated;
        _scanner.SessionRemoved += OnSessionRemoved;
        Sessions.CollectionChanged += OnSessionsChanged;

        for (int i = 0; i < PixelColumns; i++)
        {
            PixelBlocks.Add(new PixelBlock { Color = StatusPalette.PixelEmpty });
        }

        foreach (var s in _scanner.CurrentSessions)
        {
            var vm = new SessionVm { Id = s.Id };
            vm.UpdateFrom(s);
            Sessions.Add(vm);
        }

        SortSessions();
        RefreshPixelBlocks();

        _ageTimer = new DispatcherTimer(TimeBasedPropertyRefreshInterval, DispatcherPriority.Background, (_, _) =>
        {
            foreach (var vm in Sessions)
            {
                vm.NotifyTimeBasedProperties();
            }
        });
        _ageTimer.Start();
    }

    public Task StartAsync(CancellationToken ct = default) => _scanner.StartAsync(ct);

    private async Task ActivateSessionAsync(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        var vm = Sessions.FirstOrDefault(v => v.Id == sessionId);
        if (vm is not null)
        {
            vm.Acknowledge();
            SortSessions();
        }
        try
        {
            await _activator.ActivateAsync(sessionId).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SessionCountText));
        OnPropertyChanged(nameof(IsEmpty));
        RefreshPixelBlocks();
    }

    private void OnSessionUpdated(object? sender, SessionUpdatedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var session = e.Session;
            var existing = Sessions.FirstOrDefault(v => v.Id == session.Id);
            if (existing is not null)
            {
                existing.UpdateFrom(session);
            }
            else
            {
                var vm = new SessionVm { Id = session.Id };
                vm.UpdateFrom(session);
                Sessions.Add(vm);
            }
            SortSessions();
            RefreshPixelBlocks();
        });
    }

    private void OnSessionRemoved(object? sender, SessionRemovedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var existing = Sessions.FirstOrDefault(v => v.Id == e.SessionId);
            if (existing is not null)
                Sessions.Remove(existing);
        });
    }

    private static int SessionRank(SessionVm v)
    {
        if (v.Status == SessionStatus.NeedsAttention) return 0;
        if (v.Status == SessionStatus.WaitingForInput && v.IsDoneRecent) return 1;
        if (v.Status == SessionStatus.Working) return 2;
        if (v.Status == SessionStatus.WaitingForInput) return 3;
        return 4;
    }

    private void SortSessions()
    {
        var sorted = Sessions
            .OrderBy(SessionRank)
            .ThenByDescending(v => v.LastActivity)
            .ToList();

        for (int target = 0; target < sorted.Count; target++)
        {
            var desired = sorted[target];
            var current = Sessions[target];
            if (!ReferenceEquals(current, desired))
            {
                var currentIdx = Sessions.IndexOf(desired);
                Sessions.Move(currentIdx, target);
            }
        }
    }

    private void RefreshPixelBlocks()
    {
        int sessionCount = Sessions.Count;
        int working = 0, attention = 0, done = 0, idle = 0;
        foreach (var s in Sessions)
        {
            switch (s.Status)
            {
                case SessionStatus.Working: working++; break;
                case SessionStatus.NeedsAttention: attention++; break;
                case SessionStatus.WaitingForInput:
                    if (s.IsDoneRecent) done++; else idle++;
                    break;
            }
        }

        var columnLayout = DistributeBlocks(attention, done, working, idle, sessionCount, PixelColumns);

        for (int i = 0; i < PixelColumns; i++)
        {
            PixelBlocks[i].Color = columnLayout[i] switch
            {
                'w' => StatusPalette.Working,
                'a' => StatusPalette.Attention,
                'd' => StatusPalette.Done,
                'i' => StatusPalette.Idle,
                _ => StatusPalette.PixelEmpty,
            };
        }
    }

    private static char[] DistributeBlocks(int attention, int done, int working, int idle, int sessionCount, int columns)
    {
        var result = new char[columns];
        Array.Fill(result, ' ');
        if (sessionCount == 0) return result;

        var counts = new[] { attention, done, working, idle };
        var keys = new[] { 'a', 'd', 'w', 'i' };

        double[] percentages = counts.Select(c => (double)c / sessionCount * columns).ToArray();
        int[] allocated = percentages.Select(p => (int)Math.Floor(p)).ToArray();
        double[] remainders = percentages.Select((p, i) => p - allocated[i]).ToArray();

        int sum = allocated.Sum();
        while (sum < columns)
        {
            int maxIdx = 0;
            double maxVal = remainders[0];
            for (int i = 1; i < remainders.Length; i++)
            {
                if (remainders[i] > maxVal)
                {
                    maxVal = remainders[i];
                    maxIdx = i;
                }
            }
            if (maxVal < 0) break;
            allocated[maxIdx]++;
            remainders[maxIdx] = -1;
            sum++;
        }

        int cursor = 0;
        for (int k = 0; k < keys.Length; k++)
        {
            for (int j = 0; j < allocated[k] && cursor < columns; j++)
            {
                result[cursor++] = keys[k];
            }
        }
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        _ageTimer.Stop();
        _scanner.SessionUpdated -= OnSessionUpdated;
        _scanner.SessionRemoved -= OnSessionRemoved;
        Sessions.CollectionChanged -= OnSessionsChanged;
        await _scanner.DisposeAsync();
    }
}
