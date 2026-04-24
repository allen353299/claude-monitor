namespace ClaudeMonitor.Core;

public readonly record struct JsonlPath(string Value)
{
    public override string ToString() => Value ?? string.Empty;

    public static JsonlPath From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("JsonlPath cannot be null, empty, or whitespace.", nameof(value));
        if (!Path.IsPathFullyQualified(value))
            throw new ArgumentException($"JsonlPath requires an absolute path, got '{value}'.", nameof(value));

        return new JsonlPath(value);
    }

    public bool TryGetLastWrite(out DateTimeOffset lastWriteUtc)
    {
        lastWriteUtc = default;
        try
        {
            if (!File.Exists(Value)) return false;
            lastWriteUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(Value), TimeSpan.Zero);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
