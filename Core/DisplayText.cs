using System.Text;
using System.Text.RegularExpressions;

namespace ClaudeMonitor.Core;

public readonly record struct DisplayText(string Value)
{
    private static readonly Regex CommandTag = new(
        "<command-(?:name|message|args)>.*?</command-(?:name|message|args)>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public override string ToString() => Value ?? string.Empty;

    public static bool TryFrom(string? rawText, int maxLength, out DisplayText displayText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            displayText = default;
            return false;
        }

        var stripped = CommandTag.Replace(rawText, string.Empty);
        var collapsed = CollapseWhitespace(stripped);
        if (collapsed.Length == 0)
        {
            displayText = default;
            return false;
        }

        var truncated = collapsed.Length <= maxLength
            ? collapsed
            : collapsed.AsSpan(0, maxLength).ToString() + "…";

        displayText = new DisplayText(truncated);
        return true;
    }

    public bool HasContent => !string.IsNullOrWhiteSpace(Value);

    private static string CollapseWhitespace(string source)
    {
        if (string.IsNullOrEmpty(source)) return source;

        var builder = new StringBuilder(source.Length);
        var lastWasSpace = false;
        foreach (var ch in source)
        {
            if (ch is '\n' or '\r' or '\t' or ' ')
            {
                if (!lastWasSpace) builder.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                builder.Append(ch);
                lastWasSpace = false;
            }
        }
        return builder.ToString().Trim();
    }
}
