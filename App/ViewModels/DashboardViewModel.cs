using ClaudeMonitor.Core.Sessions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeMonitor.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty] private int _selectedTabIndex;

    public MonitorTabViewModel Monitor { get; }
    public HistoryTabViewModel History { get; }
    public CostTabViewModel Cost { get; }
    public MemoryTabViewModel Memory { get; }
    public SettingsTabViewModel Settings { get; } = new();

    public DashboardViewModel(
        PopoverViewModel sharedPopover,
        IHistoryReader historyReader,
        IMemoryReader memoryReader,
        ICostReader costReader)
    {
        Monitor = new MonitorTabViewModel(sharedPopover);
        History = new HistoryTabViewModel(historyReader);
        Memory = new MemoryTabViewModel(memoryReader);
        Cost = new CostTabViewModel(costReader);
        _ = History.RefreshAsync();
        _ = Memory.RefreshAsync();
        _ = Cost.RefreshAsync();
    }
}

public sealed class MonitorTabViewModel
{
    public PopoverViewModel Popover { get; }
    public MonitorTabViewModel(PopoverViewModel popover) => Popover = popover;
}

