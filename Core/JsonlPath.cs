using System.Runtime.InteropServices;

namespace ClaudeMonitor.Core;

/// <summary>
/// Absolute path to a .jsonl session transcript file.
/// On Windows the underlying value is normalized to lower-invariant so that
/// record-struct equality (used by ConcurrentDictionary&lt;JsonlPath, ...&gt;)
/// behaves case-insensitively, matching NTFS semantics.
/// </summary>
public readonly record struct JsonlPath(string Value)
{
    public override string ToString() => Value ?? string.Empty;

    public static JsonlPath From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("JsonlPath cannot be null, empty, or whitespace.", nameof(value));
        if (!Path.IsPathFullyQualified(value))
            throw new ArgumentException($"JsonlPath requires an absolute path, got '{value}'.", nameof(value));

        var normalized = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? value.ToLowerInvariant()
            : value;

        return new JsonlPath(normalized);
    }
}
