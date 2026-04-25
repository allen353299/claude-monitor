using System;
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

    public DashboardWindow()
    {
        InitializeComponent();
        RestoreWindowState();
        Opened += (_, _) =>
        {
            EnsurePositionOnVisibleScreen();
            _stateRestored = true;
        };
        SizeChanged += (_, _) => SchedulePersist();
        PositionChanged += (_, _) => SchedulePersist();
        Closing += (_, _) =>
        {
            _persistDebounceTimer?.Stop();
            PersistWindowState();
        };
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
        var screens = Screens;
        if (screens is null || screens.All.Count == 0) return;

        var width = (int)Bounds.Width;
        var height = (int)Bounds.Height;
        foreach (var s in screens.All)
        {
            var area = s.WorkingArea;
            if (Position.X + width > area.X && Position.X < area.X + area.Width
                && Position.Y + height > area.Y && Position.Y < area.Y + area.Height)
            {
                return;
            }
        }
        var primary = screens.Primary ?? screens.All[0];
        var pa = primary.WorkingArea;
        Position = new PixelPoint(pa.X + (pa.Width - width) / 2, pa.Y + (pa.Height - height) / 2);
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
        PersistedWindowStateStore.Save(StatePath, new PersistedWindowState(
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

    private void OnOpenReleaseClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not string url) return;
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
}
