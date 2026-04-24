using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace C9Watch.App.ViewModels;

public sealed partial class PixelBlock : ObservableObject
{
    [ObservableProperty] private IBrush _color = Brushes.Transparent;
}
