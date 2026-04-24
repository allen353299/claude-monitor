using System.Text.Json.Serialization;

namespace ClaudeMonitor.Core.Models;

public sealed class SessionEntryBase
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("gitBranch")]
    public string? GitBranch { get; set; }

    [JsonPropertyName("parentUuid")]
    public string? ParentUuid { get; set; }

    [JsonPropertyName("isSidechain")]
    public bool? IsSidechain { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }
}
