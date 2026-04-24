using Avalonia.Controls;
using ClaudeMonitor.App.ViewModels;

namespace ClaudeMonitor.App.Views;

public partial class ConversationWindow : Window
{
    public ConversationWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is ConversationViewModel vm) await vm.LoadAsync();
        };
    }
}
