using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeMonitor.Core.Models;

[JsonConverter(typeof(MessageContentConverter))]
public abstract class MessageContent
{
}

public sealed class TextContent : MessageContent
{
    public string Text { get; set; } = "";
}

public sealed class ThinkingContent : MessageContent
{
    public string Thinking { get; set; } = "";
    public string? Signature { get; set; }
}

public sealed class ToolUseContent : MessageContent
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public JsonElement Input { get; set; }
}

public sealed class ToolResultContent : MessageContent
{
    public string ToolUseId { get; set; } = "";
    public string Content { get; set; } = "";
    public bool? IsError { get; set; }
}

public sealed class UnknownContent : MessageContent
{
}

internal sealed class MessageContentConverter : JsonConverter<MessageContent>
{
    public override MessageContent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return new UnknownContent();
        }

        if (!root.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
        {
            return new UnknownContent();
        }

        return typeEl.GetString() switch
        {
            "text" => new TextContent
            {
                Text = root.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString() ?? "" : "",
            },
            "thinking" => new ThinkingContent
            {
                Thinking = root.TryGetProperty("thinking", out var th) && th.ValueKind == JsonValueKind.String
                    ? th.GetString() ?? "" : "",
                Signature = root.TryGetProperty("signature", out var sig) && sig.ValueKind == JsonValueKind.String
                    ? sig.GetString() : null,
            },
            "tool_use" => new ToolUseContent
            {
                Id = root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                    ? id.GetString() ?? "" : "",
                Name = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                    ? n.GetString() ?? "" : "",
                Input = root.TryGetProperty("input", out var inp) ? inp.Clone() : default,
            },
            "tool_result" => new ToolResultContent
            {
                ToolUseId = root.TryGetProperty("tool_use_id", out var tu) && tu.ValueKind == JsonValueKind.String
                    ? tu.GetString() ?? "" : "",
                Content = root.TryGetProperty("content", out var c) ? FlattenContent(c) : "",
                IsError = root.TryGetProperty("is_error", out var ie) && ie.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? ie.GetBoolean() : null,
            },
            _ => new UnknownContent(),
        };
    }

    private static string FlattenContent(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString() ?? "";
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var block in element.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object) continue;
                if (block.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                {
                    var t = textEl.GetString();
                    if (t != null) parts.Add(t);
                }
            }
            return string.Join("\n", parts);
        }

        return "";
    }

    public override void Write(Utf8JsonWriter writer, MessageContent value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case TextContent t:
                writer.WriteString("type", "text");
                writer.WriteString("text", t.Text);
                break;
            case ThinkingContent th:
                writer.WriteString("type", "thinking");
                writer.WriteString("thinking", th.Thinking);
                if (th.Signature != null) writer.WriteString("signature", th.Signature);
                break;
            case ToolUseContent tu:
                writer.WriteString("type", "tool_use");
                writer.WriteString("id", tu.Id);
                writer.WriteString("name", tu.Name);
                writer.WritePropertyName("input");
                tu.Input.WriteTo(writer);
                break;
            case ToolResultContent tr:
                writer.WriteString("type", "tool_result");
                writer.WriteString("tool_use_id", tr.ToolUseId);
                writer.WriteString("content", tr.Content);
                if (tr.IsError.HasValue) writer.WriteBoolean("is_error", tr.IsError.Value);
                break;
            default:
                writer.WriteString("type", "unknown");
                break;
        }
        writer.WriteEndObject();
    }
}
