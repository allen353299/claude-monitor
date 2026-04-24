using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ClaudeMonitor.App.Composition;
using ClaudeMonitor.App.ViewModels;
using ClaudeMonitor.App.Views;
using ClaudeMonitor.Core;

namespace ClaudeMonitor.App;

public partial class App : Application
{
    private PopoverWindow? _popover;
    private DashboardWindow? _dashboard;
    private PopoverViewModel? _popoverVm;
    private readonly Dictionary<JsonlPath, ConversationWindow> _conversationWindows = new();

    public static bool IsQuitting { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _popover = new PopoverWindow();
            _popoverVm = AppComposition.CreatePopoverViewModel(() => _popover?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
            _popoverVm.ConversationRequested += OnConversationRequested;
            _popover.DataContext = _popoverVm;
            desktop.MainWindow = _popover;
            _popover.Show();
            _ = _popoverVm.StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnTrayClicked(object? sender, EventArgs e) => TogglePopover();

    private void OnTrayShowClick(object? sender, EventArgs e) => ShowPopover();

    private void OnTrayHideClick(object? sender, EventArgs e) => _popover?.Hide();

    private void OnTrayDashboardClick(object? sender, EventArgs e) => ShowDashboard();

    private void ShowDashboard()
    {
        if (_popoverVm is null) return;
        if (_dashboard is null)
        {
            _dashboard = new DashboardWindow { DataContext = AppComposition.CreateDashboardViewModel(_popoverVm) };
            _dashboard.Closed += (_, _) => _dashboard = null;
        }
        _dashboard.Show();
        _dashboard.Activate();
    }

    private async void OnTrayQuitClick(object? sender, EventArgs e)
    {
        IsQuitting = true;
        if (_popoverVm is not null)
            await _popoverVm.DisposeAsync();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void OnConversationRequested(object? sender, ConversationRequestedEventArgs e)
    {
        if (_conversationWindows.TryGetValue(e.Path, out var existing))
        {
            existing.Show();
            existing.Activate();
            return;
        }
        var window = new ConversationWindow
        {
            DataContext = new ConversationViewModel(e.Path, e.Title),
        };
        window.Closed += (_, _) => _conversationWindows.Remove(e.Path);
        _conversationWindows[e.Path] = window;
        window.Show();
    }

    private void TogglePopover()
    {
        if (_popover is null) return;
        if (_popover.IsVisible) _popover.Hide();
        else ShowPopover();
    }

    private void ShowPopover()
    {
        if (_popover is null) return;
        _popover.Show();
        _popover.Activate();
    }
}
