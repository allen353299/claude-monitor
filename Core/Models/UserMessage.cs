using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeMonitor.Core.Models;

[JsonConverter(typeof(UserMessageConverter))]
public sealed class UserMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public bool IsToolResult { get; set; }
    public List<string> Images { get; set; } = new();
}

internal sealed class UserMessageConverter : JsonConverter<UserMessage>
{
    public override UserMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var msg = new UserMessage();

        if (root.TryGetProperty("role", out var roleEl) && roleEl.ValueKind == JsonValueKind.String)
        {
            msg.Role = roleEl.GetString() ?? "user";
        }

        if (!root.TryGetProperty("content", out var contentEl))
        {
            return msg;
        }

        if (contentEl.ValueKind == JsonValueKind.String)
        {
            msg.Content = contentEl.GetString() ?? "";
            return msg;
        }

        if (contentEl.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            var hasToolResult = false;

            foreach (var block in contentEl.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object) continue;
                if (!block.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) continue;

                var type = typeEl.GetString();
                switch (type)
                {
                    case "tool_result":
                        hasToolResult = true;
                        if (block.TryGetProperty("content", out var trContent))
                        {
                            FlattenToolResultContent(trContent, parts);
                        }
                        break;
                    case "text":
                        if (block.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                        {
                            var t = textEl.GetString();
                            if (t != null) parts.Add(t);
                        }
                        break;
                    case "image":
                        if (block.TryGetProperty("source", out var sourceEl) && sourceEl.ValueKind == JsonValueKind.Object)
                        {
                            if (sourceEl.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.String)
                            {
                                var data = dataEl.GetString();
                                if (data != null) msg.Images.Add(data);
                            }
                        }
                        break;
                }
            }

            msg.IsToolResult = hasToolResult;
            if (parts.Count == 0 && !hasToolResult)
            {
                msg.Content = "";
            }
            else if (parts.Count == 0)
            {
                msg.Content = "[tool result]";
            }
            else
            {
                msg.Content = string.Join("\n", parts);
            }
        }

        return msg;
    }

    private static void FlattenToolResultContent(JsonElement content, List<string> parts)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            var s = content.GetString();
            if (s != null) parts.Add(s);
            return;
        }

        if (content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object) continue;
                if (block.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                {
                    var t = textEl.GetString();
                    if (t != null) parts.Add(t);
                }
            }
        }
    }

    public override void Write(Utf8JsonWriter writer, UserMessage value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("role", value.Role);
        writer.WriteString("content", value.Content);
        writer.WriteEndObject();
    }
}
