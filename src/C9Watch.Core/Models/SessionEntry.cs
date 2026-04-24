using System.Text.Json;
using System.Text.Json.Serialization;

namespace C9Watch.Core.Models;

[JsonConverter(typeof(SessionEntryConverter))]
public abstract class SessionEntry
{
}

public sealed class UserEntry : SessionEntry
{
    public SessionEntryBase Base { get; set; } = new();
    public UserMessage Message { get; set; } = new();
}

public sealed class AssistantEntry : SessionEntry
{
    public SessionEntryBase Base { get; set; } = new();
    public AssistantMessage Message { get; set; } = new();
}

public sealed class FileHistorySnapshotEntry : SessionEntry
{
    public string MessageId { get; set; } = "";
    public JsonElement Snapshot { get; set; }
    public bool IsSnapshotUpdate { get; set; }
}

public sealed class SummaryEntry : SessionEntry
{
    public string Summary { get; set; } = "";
    public string LeafUuid { get; set; } = "";
}

public sealed class CustomTitleEntry : SessionEntry
{
    public string CustomTitle { get; set; } = "";
    public string SessionId { get; set; } = "";
}

public sealed class SystemEntry : SessionEntry
{
    public string Subtype { get; set; } = "";
    public string Content { get; set; } = "";
    public string Timestamp { get; set; } = "";
}

public sealed class UnknownEntry : SessionEntry
{
}

internal sealed class SessionEntryConverter : JsonConverter<SessionEntry>
{
    public override SessionEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return new UnknownEntry();
        }

        if (!root.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
        {
            return new UnknownEntry();
        }

        var type = typeEl.GetString();
        var raw = root.GetRawText();

        return type switch
        {
            "user" => ReadUser(root, raw, options),
            "assistant" => ReadAssistant(root, raw, options),
            "file-history-snapshot" => ReadFileHistorySnapshot(root),
            "summary" => ReadSummary(root),
            "custom-title" => ReadCustomTitle(root),
            "system" => ReadSystem(root),
            _ => new UnknownEntry(),
        };
    }

    private static SystemEntry ReadSystem(JsonElement root)
    {
        return new SystemEntry
        {
            Subtype = root.TryGetProperty("subtype", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString() ?? "" : "",
            Content = root.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString() ?? "" : "",
            Timestamp = root.TryGetProperty("timestamp", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() ?? "" : "",
        };
    }

    private static UserEntry ReadUser(JsonElement root, string raw, JsonSerializerOptions options)
    {
        var entry = new UserEntry
        {
            Base = JsonSerializer.Deserialize<SessionEntryBase>(raw, options) ?? new SessionEntryBase(),
        };
        if (root.TryGetProperty("message", out var msg))
        {
            entry.Message = JsonSerializer.Deserialize<UserMessage>(msg.GetRawText(), options) ?? new UserMessage();
        }
        return entry;
    }

    private static AssistantEntry ReadAssistant(JsonElement root, string raw, JsonSerializerOptions options)
    {
        var entry = new AssistantEntry
        {
            Base = JsonSerializer.Deserialize<SessionEntryBase>(raw, options) ?? new SessionEntryBase(),
        };
        if (root.TryGetProperty("message", out var msg))
        {
            entry.Message = JsonSerializer.Deserialize<AssistantMessage>(msg.GetRawText(), options) ?? new AssistantMessage();
        }
        return entry;
    }

    private static FileHistorySnapshotEntry ReadFileHistorySnapshot(JsonElement root)
    {
        return new FileHistorySnapshotEntry
        {
            MessageId = root.TryGetProperty("messageId", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString() ?? "" : "",
            Snapshot = root.TryGetProperty("snapshot", out var s) ? s.Clone() : default,
            IsSnapshotUpdate = root.TryGetProperty("isSnapshotUpdate", out var u)
                && u.ValueKind is JsonValueKind.True or JsonValueKind.False && u.GetBoolean(),
        };
    }

    private static SummaryEntry ReadSummary(JsonElement root)
    {
        return new SummaryEntry
        {
            Summary = root.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString() ?? "" : "",
            LeafUuid = root.TryGetProperty("leafUuid", out var l) && l.ValueKind == JsonValueKind.String
                ? l.GetString() ?? "" : "",
        };
    }

    private static CustomTitleEntry ReadCustomTitle(JsonElement root)
    {
        return new CustomTitleEntry
        {
            CustomTitle = root.TryGetProperty("customTitle", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() ?? "" : "",
            SessionId = root.TryGetProperty("sessionId", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString() ?? "" : "",
        };
    }

    public override void Write(Utf8JsonWriter writer, SessionEntry value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("SessionEntry write is not supported.");
    }
}
