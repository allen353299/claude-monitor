using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Sessions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeMonitor.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    public const int ConversationTabIndex = 5;

    [ObservableProperty] private int _selectedTabIndex;

    [ObservableProperty] private ConversationViewModel? _currentConversation;

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
    }

    public void OpenConversation(JsonlPath path, string title)
    {
        var vm = new ConversationViewModel(path, title);
        CurrentConversation = vm;
        SelectedTabIndex = ConversationTabIndex;
        _ = vm.LoadAsync();
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        switch (value)
        {
            case 1: _ = History.RefreshAsync(); break;
            case 2: _ = Cost.RefreshAsync(); break;
            case 3: _ = Memory.RefreshAsync(); break;
        }
    }
}

public sealed class MonitorTabViewModel
{
    public PopoverViewModel Popover { get; }
    public MonitorTabViewModel(PopoverViewModel popover) => Popover = popover;
}

