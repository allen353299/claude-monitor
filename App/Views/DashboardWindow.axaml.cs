using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClaudeMonitor.App.ViewModels;

namespace ClaudeMonitor.App.Views;

public partial class DashboardWindow : Window
{
    public DashboardWindow()
    {
        InitializeComponent();
    }

    private void OnMemoryFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not MemoryFileVm file) return;
        if (DataContext is not DashboardViewModel vm) return;
        vm.Memory.SelectedFile = file;
    }

    private void OnOpenReleaseClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not string url) return;
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
}
