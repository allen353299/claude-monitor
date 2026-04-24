namespace ClaudeMonitor.Core.Platform;

public interface IPlatformServices
{
    string ClaudeHomeDirectory { get; }
}

public sealed class DefaultPlatformServices : IPlatformServices
{
    public string ClaudeHomeDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
}
