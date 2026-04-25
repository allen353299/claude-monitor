using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Sessions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeMonitor.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    public const int ConversationTabIndex = 4;

    [ObservableProperty] private int _selectedTabIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConversation))]
    private ConversationViewModel? _currentConversation;

    public bool HasConversation => CurrentConversation is not null;

    public MonitorTabViewModel Monitor { get; }
    public HistoryTabViewModel History { get; }
    public MemoryTabViewModel Memory { get; }
    public SettingsTabViewModel Settings { get; } = new();

    public DashboardViewModel(
        PopoverViewModel sharedPopover,
        IHistoryReader historyReader,
        IMemoryReader memoryReader,
        ICostReader costReader)
    {
        Monitor = new MonitorTabViewModel(sharedPopover);
        History = new HistoryTabViewModel(historyReader, costReader);
        Memory = new MemoryTabViewModel(memoryReader);
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
            case 2: _ = Memory.RefreshAsync(); break;
        }
    }
}

public sealed class MonitorTabViewModel
{
    public PopoverViewModel Popover { get; }
    public MonitorTabViewModel(PopoverViewModel popover) => Popover = popover;
}

