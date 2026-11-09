using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeMonitor.App.ViewModels;

public sealed partial class PixelBlock : ObservableObject
{
    [ObservableProperty] private IBrush _color = Brushes.Transparent;
    [ObservableProperty] private bool _isWorking;
    [ObservableProperty] private bool _isLit;
    [ObservableProperty] private bool _isLit2;
    [ObservableProperty] private bool _isLit3;
}
