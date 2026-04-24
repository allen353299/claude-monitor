using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using C9Watch.App.ViewModels;
using C9Watch.App.Views;
using C9Watch.Core.Platform;
using C9Watch.Core.Sessions;
using C9Watch.Platform.Windows;

namespace C9Watch.App;

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
