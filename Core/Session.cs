using ClaudeMonitor.Core.Models;

namespace ClaudeMonitor.Core;

public sealed record Session(
    SessionId Id,
    string SessionName,
    string? CustomTitle,
    ProjectPath ProjectPath,
    JsonlPath JsonlPath,
    SessionStatus Status,
    string? PendingTool,
    string? FirstPrompt,
    string? LatestMessage,
    string? Recap,
    int MessageCount,
    bool IsLive,
    DateTimeOffset LastActivity);
