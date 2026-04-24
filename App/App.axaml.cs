using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ClaudeMonitor.App.Composition;
using ClaudeMonitor.App.Views;

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
            var vm = AppComposition.CreatePopoverViewModel();
            desktop.MainWindow = new PopoverWindow { DataContext = vm };
            _ = vm.StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
