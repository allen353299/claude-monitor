using C9Watch.Core.Models;

namespace C9Watch.Core;

public sealed record Session(
    string Id,
    string SessionName,
    string? CustomTitle,
    string ProjectPath,
    string JsonlPath,
    SessionStatus Status,
    string? PendingTool,
    string? FirstPrompt,
    string? LatestMessage,
    string? Recap,
    int MessageCount,
    bool IsLive,
    DateTimeOffset LastActivity);
