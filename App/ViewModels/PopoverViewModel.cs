using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Models;
using ClaudeMonitor.Core.Platform;
using ClaudeMonitor.Core.Sessions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeMonitor.App.ViewModels;

public sealed class ConversationRequestedEventArgs : EventArgs
{
    public JsonlPath Path { get; }
    public string Title { get; }
    public ConversationRequestedEventArgs(JsonlPath path, string title)
    {
        Path = path;
        Title = title;
    }
}

public sealed partial class PopoverViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly TimeSpan TimeBasedPropertyRefreshInterval = TimeSpan.FromMinutes(1);

    private readonly ISessionScanner _scanner;
    private readonly ISessionActivator _activator;
    private readonly INotificationService _notifications;
    private readonly DispatcherTimer _ageTimer;
    private readonly Dictionary<SessionId, SessionStatus> _lastNotifiedStatus = new();

    public SessionVmCollection Sessions { get; } = new();

    public PixelBar Bar { get; } = new();

    public string SessionCountText => $"{Sessions.Count} SESSION{(Sessions.Count == 1 ? "" : "S")}";

    public bool IsEmpty => Sessions.Count == 0;

    [ObservableProperty] private bool _isCompact;

    public IAsyncRelayCommand<string> ActivateSessionCommand { get; }

    public IRelayCommand<string> ViewConversationCommand { get; }

    public event EventHandler<ConversationRequestedEventArgs>? ConversationRequested;

    partial void OnIsCompactChanged(bool value)
    {
        foreach (var vm in Sessions) vm.IsCompact = value;
    }

    public PopoverViewModel(ISessionScanner scanner, ISessionActivator activator, INotificationService notifications)
    {
        _scanner = scanner;
        _activator = activator;
        _notifications = notifications;
        ActivateSessionCommand = new AsyncRelayCommand<string>(ActivateSessionAsync);
        ViewConversationCommand = new RelayCommand<string>(RequestConversation);

        _scanner.SessionUpdated += OnSessionUpdated;
        _scanner.SessionRemoved += OnSessionRemoved;
        Sessions.CollectionChanged += OnSessionsChanged;

        foreach (var s in _scanner.CurrentSessions)
        {
            var vm = new SessionVm { Id = s.Id, IsCompact = IsCompact };
            vm.UpdateFrom(s);
            Sessions.Add(vm);
            _lastNotifiedStatus[s.Id] = s.Status;
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

    private void RequestConversation(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        var id = SessionId.From(sessionId);
        var vm = Sessions.FindById(id);
        if (vm is null) return;
        ConversationRequested?.Invoke(this, new ConversationRequestedEventArgs(vm.JsonlPath, vm.PrimaryLabel));
    }

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
                var vm = new SessionVm { Id = session.Id, IsCompact = IsCompact };
                vm.UpdateFrom(session);
                Sessions.Add(vm);
            }
            Sessions.Sort(SessionVmRankComparer.Default);
            Bar.Refresh(Sessions);
            RaiseAttentionIfTransitioned(session);
        });
    }

    private void RaiseAttentionIfTransitioned(Session session)
    {
        var hadPrevious = _lastNotifiedStatus.TryGetValue(session.Id, out var previous);
        _lastNotifiedStatus[session.Id] = session.Status;

        if (!hadPrevious) return;
        if (previous == session.Status) return;

        var title = session.SessionName;
        if (session.Status == SessionStatus.NeedsAttention)
            _notifications.NotifyAttention(session.Id, title, "needs your attention");
        else if (session.Status == SessionStatus.WaitingForInput && previous == SessionStatus.Working)
            _notifications.NotifyAttention(session.Id, title, "task complete");
    }

    private void OnSessionRemoved(object? sender, SessionRemovedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Sessions.RemoveById(e.SessionId);
            _lastNotifiedStatus.Remove(e.SessionId);
        });
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
