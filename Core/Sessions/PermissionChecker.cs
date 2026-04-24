using System.Text.Json;

namespace ClaudeMonitor.Core.Sessions;

public sealed class PermissionChecker
{
    private static readonly IReadOnlyList<string> DefaultAllowList = new[]
    {
        "Read", "Glob", "Grep", "WebFetch", "WebSearch",
        "Task", "TaskList", "TaskGet", "TaskCreate", "TaskUpdate",
        "AskUserQuestion",
    };

    private static readonly HashSet<string> AlwaysAutoApproved = new(StringComparer.Ordinal)
    {
        "Read", "Glob", "Grep", "WebFetch", "WebSearch",
        "Task", "TaskList", "TaskGet", "TaskCreate", "TaskUpdate",
        "AskUserQuestion",
    };

    private static readonly HashSet<string> EditTools = new(StringComparer.Ordinal)
    {
        "Write", "Edit", "MultiEdit", "NotebookEdit",
    };

    public static PermissionChecker Default { get; } = new(DefaultAllowList);

    private readonly List<BashPattern> _bashPatterns = new();
    private readonly HashSet<string> _allowedTools = new(StringComparer.Ordinal);
    private readonly HashSet<string> _allowedMcp = new(StringComparer.Ordinal);

    public PermissionChecker(IReadOnlyList<string> allowList)
    {
        ArgumentNullException.ThrowIfNull(allowList);

        foreach (var entry in allowList)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            Parse(entry);
        }
    }

    private void Parse(string pattern)
    {
        if (pattern.StartsWith("Bash(", StringComparison.Ordinal) && pattern.EndsWith(")", StringComparison.Ordinal))
        {
            var inner = pattern.Substring(5, pattern.Length - 6);
            if (inner.EndsWith(":*", StringComparison.Ordinal))
            {
                _bashPatterns.Add(new BashPattern(inner.Substring(0, inner.Length - 2), true));
            }
            else
            {
                _bashPatterns.Add(new BashPattern(inner, false));
            }
            return;
        }

        if (pattern.StartsWith("mcp__", StringComparison.Ordinal))
        {
            _allowedMcp.Add(pattern);
            return;
        }

        if (pattern.StartsWith("Skill(", StringComparison.Ordinal) && pattern.EndsWith(")", StringComparison.Ordinal))
        {
            return;
        }

        if (!pattern.Contains('(') && !pattern.Contains("__", StringComparison.Ordinal))
        {
            _allowedTools.Add(pattern);
        }
    }

    public bool IsAutoApproved(string toolName, JsonElement input)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolName);

        if (AlwaysAutoApproved.Contains(toolName)) return true;

        if (toolName == "Bash")
        {
            var command = "";
            if (input.ValueKind == JsonValueKind.Object
                && input.TryGetProperty("command", out var cmdEl)
                && cmdEl.ValueKind == JsonValueKind.String)
            {
                command = cmdEl.GetString() ?? "";
            }
            return IsBashAllowed(command);
        }

        if (toolName.StartsWith("mcp__", StringComparison.Ordinal))
        {
            return _allowedMcp.Contains(toolName);
        }

        if (EditTools.Contains(toolName))
        {
            return _allowedTools.Contains(toolName);
        }

        return false;
    }

    private bool IsBashAllowed(string command)
    {
        var trimmed = command.Trim();
        foreach (var p in _bashPatterns)
        {
            if (p.Wildcard)
            {
                if (trimmed.StartsWith(p.Prefix, StringComparison.Ordinal)) return true;
            }
            else
            {
                if (trimmed == p.Prefix) return true;
            }
        }
        return false;
    }

    private sealed record BashPattern(string Prefix, bool Wildcard);
}
