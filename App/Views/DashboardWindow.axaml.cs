using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClaudeMonitor.App.ViewModels;
using ClaudeMonitor.Core.Persistence;

namespace ClaudeMonitor.App.Views;

public partial class DashboardWindow : Window
{
    private static readonly string StatePath = PersistedWindowStateStore.PathFor("dashboard");
    private static readonly TimeSpan PersistDebounce = TimeSpan.FromMilliseconds(400);

    private DispatcherTimer? _persistDebounceTimer;
    private bool _stateRestored;
    private string? _currentFingerprint;

    public DashboardWindow()
    {
        InitializeComponent();
        RestoreWindowState();
        Opened += (_, _) =>
        {
            _currentFingerprint = ComputeFingerprint();
            TryApplyFingerprintState();
            EnsurePositionOnVisibleScreen();
            if (Screens is { } screens) screens.Changed += OnScreensChanged;
            _stateRestored = true;
        };
        SizeChanged += (_, _) => SchedulePersist();
        PositionChanged += (_, _) => SchedulePersist();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible && _stateRestored)
                EnsurePositionOnVisibleScreen();
        };
        Closing += (_, _) =>
        {
            _persistDebounceTimer?.Stop();
            PersistWindowState();
        };
    }

    private void OnScreensChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(HandleLayoutChange);

    private void HandleLayoutChange()
    {
        var newFp = ComputeFingerprint();
        if (newFp == _currentFingerprint)
        {
            EnsurePositionOnVisibleScreen();
            return;
        }
        if (_currentFingerprint is not null && _stateRestored)
        {
            PersistedWindowStateStore.Save(StatePath, _currentFingerprint,
                new PersistedWindowState(Position.X, Position.Y, Width, Height, false));
        }
        _currentFingerprint = newFp;
        if (newFp is not null && PersistedWindowStateStore.TryLoad(StatePath, newFp, out var state))
        {
            Position = new PixelPoint(state.X, state.Y);
            Width = state.Width;
            Height = state.Height;
        }
        EnsurePositionOnVisibleScreen();
    }

    private void TryApplyFingerprintState()
    {
        if (_currentFingerprint is null) return;
        if (!PersistedWindowStateStore.TryLoad(StatePath, _currentFingerprint, out var state)) return;
        if (state.X == Position.X && state.Y == Position.Y
            && Math.Abs(state.Width - Width) < 0.5 && Math.Abs(state.Height - Height) < 0.5)
            return;
        Position = new PixelPoint(state.X, state.Y);
        Width = state.Width;
        Height = state.Height;
    }

    private string? ComputeFingerprint()
    {
        if (Screens is null || Screens.All.Count == 0) return null;
        var infos = new List<ScreenInfo>(Screens.All.Count);
        foreach (var s in Screens.All)
        {
            var area = s.WorkingArea;
            infos.Add(new ScreenInfo(area.X, area.Y, area.Width, area.Height, s.DisplayName, s.IsPrimary));
        }
        return WindowPositionPolicy.ComputeLayoutFingerprint(infos);
    }

    private const double PixelBarSlotPitch = 12;

    private void OnPixelBarSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm) return;
        var width = e.NewSize.Width;
        if (width <= 0) return;
        var count = (int)Math.Floor(width / PixelBarSlotPitch);
        if (count < 1) count = 1;
        vm.Monitor.Popover.Bar.SetColumnCount(count);
    }

    private void RestoreWindowState()
    {
        if (!PersistedWindowStateStore.TryLoad(StatePath, out var state)) return;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint(state.X, state.Y);
        Width = state.Width;
        Height = state.Height;
    }

    private void EnsurePositionOnVisibleScreen()
    {
        if (Screens is null || Screens.All.Count == 0) return;
        var width = (int)Bounds.Width;
        var height = (int)Bounds.Height;
        if (width <= 0 || height <= 0) { width = (int)Width; height = (int)Height; }
        var infos = new List<ScreenInfo>(Screens.All.Count);
        foreach (var s in Screens.All)
        {
            var area = s.WorkingArea;
            infos.Add(new ScreenInfo(area.X, area.Y, area.Width, area.Height, s.DisplayName, s.IsPrimary));
        }
        var clamped = WindowPositionPolicy.ClampToVisibleScreens(Position.X, Position.Y, width, height, infos);
        if (clamped is { } pt) Position = new PixelPoint(pt.X, pt.Y);
    }

    private void SchedulePersist()
    {
        if (!_stateRestored) return;
        _persistDebounceTimer ??= new DispatcherTimer(PersistDebounce, DispatcherPriority.Background, OnDebouncedPersist);
        _persistDebounceTimer.Stop();
        _persistDebounceTimer.Start();
    }

    private void OnDebouncedPersist(object? sender, EventArgs e)
    {
        _persistDebounceTimer?.Stop();
        PersistWindowState();
    }

    private void PersistWindowState()
    {
        if (!_stateRestored) return;
        PersistedWindowStateStore.Save(StatePath, _currentFingerprint, new PersistedWindowState(
            Position.X, Position.Y, Width, Height, false));
    }

    private void OnMemoryFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not MemoryFileVm file) return;
        if (DataContext is not DashboardViewModel vm) return;
        vm.Memory.SelectedFile = file;
    }

    private void OnHistoryRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not HistoryItemVm item) return;
        if (item.JsonlPath is null) return;
        if (DataContext is not DashboardViewModel vm) return;
        vm.OpenConversation(item.JsonlPath.Value, item.PrimaryLabel);
    }


    public event EventHandler? PopoverRequested;

    private void OnShowPopoverClick(object? sender, RoutedEventArgs e)
        => PopoverRequested?.Invoke(this, EventArgs.Empty);

    private void OnOpenReleaseClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not string url) return;
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
}
