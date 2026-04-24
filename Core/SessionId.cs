namespace ClaudeMonitor.Core;

public readonly record struct SessionId(string Value)
{
    public override string ToString() => Value ?? string.Empty;

    public static SessionId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SessionId cannot be null, empty, or whitespace.", nameof(value));
        return new SessionId(value);
    }
}
