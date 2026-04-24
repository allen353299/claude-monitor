using ClaudeMonitor.Core.Models;

namespace ClaudeMonitor.Core;

public sealed record Session(
    SessionId Id,
    string SessionName,
    DisplayText? CustomTitle,
    ProjectPath ProjectPath,
    JsonlPath JsonlPath,
    SessionStatus Status,
    string? PendingTool,
    DisplayText? FirstPrompt,
    DisplayText? LatestMessage,
    DisplayText? Recap,
    int MessageCount,
    bool IsLive,
    DateTimeOffset LastActivity);
