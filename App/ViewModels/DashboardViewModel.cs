using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeMonitor.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty] private int _selectedTabIndex;

    public MonitorTabViewModel Monitor { get; }
    public HistoryTabViewModel History { get; } = new();
    public CostTabViewModel Cost { get; } = new();
    public MemoryTabViewModel Memory { get; } = new();
    public SettingsTabViewModel Settings { get; } = new();

    public DashboardViewModel(PopoverViewModel sharedPopover)
    {
        Monitor = new MonitorTabViewModel(sharedPopover);
    }
}

public sealed class MonitorTabViewModel
{
    public PopoverViewModel Popover { get; }
    public MonitorTabViewModel(PopoverViewModel popover) => Popover = popover;
}

public sealed partial class HistoryTabViewModel : ObservableObject
{
    [ObservableProperty] private string _placeholder = "History coming soon.";
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
