namespace ClaudeMonitor.Core.Persistence;

public readonly record struct ScreenInfo(
    int X,
    int Y,
    int Width,
    int Height,
    string? DisplayName,
    bool IsPrimary);
