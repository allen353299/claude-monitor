using Avalonia.Media;

namespace ClaudeMonitor.App.Theme;

public static class StatusPalette
{
    public static IBrush Working { get; } = new SolidColorBrush(Color.Parse("#22c55e"));
    public static IBrush Attention { get; } = new SolidColorBrush(Color.Parse("#ff6600"));
    public static IBrush Done { get; } = new SolidColorBrush(Color.Parse("#06b6d4"));
    public static IBrush Idle { get; } = new SolidColorBrush(Color.Parse("#666666"));
    public static IBrush PixelEmpty { get; } = new SolidColorBrush(Color.FromArgb(0x0f, 0xff, 0xff, 0xff));
}
