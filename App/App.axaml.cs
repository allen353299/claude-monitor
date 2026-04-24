using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ClaudeMonitor.App.Composition;
using ClaudeMonitor.App.ViewModels;
using ClaudeMonitor.App.Views;

namespace ClaudeMonitor.App;

public partial class App : Application
{
    private PopoverWindow? _popover;
    private DashboardWindow? _dashboard;
    private PopoverViewModel? _popoverVm;

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
            _dashboard = new DashboardWindow { DataContext = new DashboardViewModel(_popoverVm) };
            _dashboard.Closed += (_, _) => _dashboard = null;
        }
        _dashboard.Show();
        _dashboard.Activate();
    }

    private void OnTrayQuitClick(object? sender, EventArgs e)
    {
        IsQuitting = true;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
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
