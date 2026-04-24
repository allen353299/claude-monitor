using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ClaudeMonitor.App.ViewModels;
using ClaudeMonitor.App.Views;
using ClaudeMonitor.Core.Platform;
using ClaudeMonitor.Core.Sessions;
using ClaudeMonitor.Platform.Windows;

namespace ClaudeMonitor.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var scanner = new SessionScanner();
            ISessionActivator activator = OperatingSystem.IsWindows()
                ? new WindowsSessionActivator()
                : new NoopSessionActivator();
            var vm = new PopoverViewModel(scanner, activator);
            var popover = new PopoverWindow { DataContext = vm };
            desktop.MainWindow = popover;
            _ = vm.StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
