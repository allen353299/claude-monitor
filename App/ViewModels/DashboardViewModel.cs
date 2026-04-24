using ClaudeMonitor.Core.Sessions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeMonitor.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty] private int _selectedTabIndex;

    public MonitorTabViewModel Monitor { get; }
    public HistoryTabViewModel History { get; }
    public CostTabViewModel Cost { get; } = new();
    public MemoryTabViewModel Memory { get; } = new();
    public SettingsTabViewModel Settings { get; } = new();

    public DashboardViewModel(PopoverViewModel sharedPopover, IHistoryReader historyReader)
    {
        Monitor = new MonitorTabViewModel(sharedPopover);
        History = new HistoryTabViewModel(historyReader);
        _ = History.RefreshAsync();
    }
}

public sealed class MonitorTabViewModel
{
    public PopoverViewModel Popover { get; }
    public MonitorTabViewModel(PopoverViewModel popover) => Popover = popover;
}

public sealed partial class CostTabViewModel : ObservableObject
{
    [ObservableProperty] private string _placeholder = "Cost coming soon.";
}

public sealed partial class MemoryTabViewModel : ObservableObject
{
    [ObservableProperty] private string _placeholder = "Memory coming soon.";
}

public sealed partial class SettingsTabViewModel : ObservableObject
{
    [ObservableProperty] private string _placeholder = "Settings coming soon.";
}
