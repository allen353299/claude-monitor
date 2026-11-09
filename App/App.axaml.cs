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
    private NativeMenuItem? _trayPopoverItem;
    private NativeMenuItem? _trayDashboardItem;

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
            _popover.DashboardRequested += (_, _) => ShowDashboard();
            _popover.DataContext = _popoverVm;
            _popover.PropertyChanged += (_, e) =>
            {
                if (e.Property == Visual.IsVisibleProperty) SyncTrayPopoverCheck();
            };
            _trayPopoverItem = ResolveTrayItem("Popover");
            _trayDashboardItem = ResolveTrayItem("Dashboard");
            desktop.MainWindow = _popover;
            _popover.Show();
            SyncTrayPopoverCheck();
            _ = _popoverVm.StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnTrayClicked(object? sender, EventArgs e) => TogglePopover();

    private void OnTrayPopoverClick(object? sender, EventArgs e) => TogglePopover();

    private void OnTrayDashboardClick(object? sender, EventArgs e)
    {
        if (_dashboard is { IsVisible: true }) _dashboard.Hide();
        else ShowDashboard();
    }

    private void SyncTrayPopoverCheck()
    {
        if (_trayPopoverItem is not null && _popover is not null)
            _trayPopoverItem.IsChecked = _popover.IsVisible;
    }

    private void SyncTrayDashboardCheck()
    {
        if (_trayDashboardItem is not null)
            _trayDashboardItem.IsChecked = _dashboard is { IsVisible: true };
    }

    private NativeMenuItem? ResolveTrayItem(string header)
    {
        var icons = TrayIcon.GetIcons(this);
        if (icons is null || icons.Count == 0) return null;
        var menu = icons[0].Menu;
        if (menu is null) return null;
        foreach (var item in menu.Items)
        {
            if (item is NativeMenuItem nmi && nmi.Header == header) return nmi;
        }
        return null;
    }

    private void ShowDashboard()
    {
        if (_popoverVm is null) return;
        if (_dashboard is null)
        {
            _dashboard = new DashboardWindow { DataContext = AppComposition.CreateDashboardViewModel(_popoverVm) };
            _dashboard.Closed += (_, _) => { _dashboard = null; SyncTrayDashboardCheck(); };
            _dashboard.PropertyChanged += (_, e) =>
            {
                if (e.Property == Visual.IsVisibleProperty) SyncTrayDashboardCheck();
            };
            _dashboard.PopoverRequested += (_, _) => ShowPopover();
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
        ShowDashboard();
        if (_dashboard?.DataContext is DashboardViewModel vm)
            vm.OpenConversation(e.Path, e.Title);
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
        _dashboard?.Hide();
        _popover.Show();
        _popover.Activate();
    }
}
