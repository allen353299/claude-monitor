using System;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Platform;
using ClaudeMonitor.Core.Sessions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeMonitor.App.ViewModels;

public sealed class PopoverViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly TimeSpan TimeBasedPropertyRefreshInterval = TimeSpan.FromMinutes(1);

    private readonly ISessionScanner _scanner;
    private readonly ISessionActivator _activator;
    private readonly DispatcherTimer _ageTimer;

    public SessionVmCollection Sessions { get; } = new();

    public PixelBar Bar { get; } = new();

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

        foreach (var s in _scanner.CurrentSessions)
        {
            var vm = new SessionVm { Id = s.Id };
            vm.UpdateFrom(s);
            Sessions.Add(vm);
        }

        Sessions.Sort(SessionVmRankComparer.Default);
        Bar.Refresh(Sessions);

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
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        var id = SessionId.From(sessionId);
        var vm = Sessions.FindById(id);
        if (vm is not null)
        {
            vm.Acknowledge();
            Sessions.Sort(SessionVmRankComparer.Default);
        }
        try
        {
            await _activator.ActivateAsync(id).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SessionCountText));
        OnPropertyChanged(nameof(IsEmpty));
        Bar.Refresh(Sessions);
    }

    private void OnSessionUpdated(object? sender, SessionUpdatedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var session = e.Session;
            var existing = Sessions.FindById(session.Id);
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
            Sessions.Sort(SessionVmRankComparer.Default);
            Bar.Refresh(Sessions);
        });
    }

    private void OnSessionRemoved(object? sender, SessionRemovedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => Sessions.RemoveById(e.SessionId));
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
