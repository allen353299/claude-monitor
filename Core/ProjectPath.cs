namespace ClaudeMonitor.Core;

/// <summary>
/// Decoded project directory path (e.g. "D:\git"), derived from the
/// encoded directory name beneath ~/.claude/projects.
/// </summary>
public readonly record struct ProjectPath(string Value)
{
    public override string ToString() => Value ?? string.Empty;

    public static ProjectPath From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ProjectPath cannot be null, empty, or whitespace.", nameof(value));

        return new ProjectPath(value);
    }
}
