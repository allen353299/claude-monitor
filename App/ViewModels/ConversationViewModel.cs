using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Models;
using ClaudeMonitor.Core.Sessions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeMonitor.App.ViewModels;

public sealed partial class ConversationViewModel : ObservableObject
{
    private readonly JsonlPath _path;

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _showThinking = true;
    [ObservableProperty] private bool _showToolCalls = true;

    public ObservableCollection<MessageVm> AllMessages { get; } = new();
    public ObservableCollection<MessageVm> VisibleMessages { get; } = new();

    public ConversationViewModel(JsonlPath path, string title)
    {
        _path = path;
        _title = title;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var entries = await SessionParser.ParseAllAsync(_path);
            AllMessages.Clear();
            foreach (var entry in entries)
            {
                foreach (var vm in Flatten(entry)) AllMessages.Add(vm);
            }
            ApplyFilter();
        }
        catch (IOException ex)
        {
            Error = ex.Message;
        }
        catch (JsonException ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnShowThinkingChanged(bool value) => ApplyFilter();
    partial void OnShowToolCallsChanged(bool value) => ApplyFilter();

    private void ApplyFilter()
    {
        VisibleMessages.Clear();
        foreach (var m in AllMessages)
        {
            if (m.Kind == MessageKind.Thinking && !ShowThinking) continue;
            if ((m.Kind == MessageKind.ToolUse || m.Kind == MessageKind.ToolResult) && !ShowToolCalls) continue;
            VisibleMessages.Add(m);
        }
    }

    private static IEnumerable<MessageVm> Flatten(SessionEntry entry)
    {
        switch (entry)
        {
            case UserEntry u:
                foreach (var vm in FlattenUser(u)) yield return vm;
                break;
            case AssistantEntry a:
                foreach (var vm in FlattenAssistant(a)) yield return vm;
                break;
            case SystemEntry s:
                yield return new MessageVm(MessageKind.System, s.Subtype, s.Content);
                break;
        }
    }

    private static IEnumerable<MessageVm> FlattenUser(UserEntry u)
    {
        if (string.IsNullOrEmpty(u.Message.Content)) yield break;
        var kind = u.Message.IsToolResult ? MessageKind.ToolResult : MessageKind.User;
        var header = u.Message.IsToolResult ? "tool_result" : "user";
        yield return new MessageVm(kind, header, u.Message.Content);
    }

    private static IEnumerable<MessageVm> FlattenAssistant(AssistantEntry a)
    {
        foreach (var c in a.Message.Content)
        {
            switch (c)
            {
                case TextContent t:
                    yield return new MessageVm(MessageKind.Assistant, "assistant", t.Text);
                    break;
                case ThinkingContent th:
                    yield return new MessageVm(MessageKind.Thinking, "thinking", th.Thinking);
                    break;
                case ToolUseContent tu:
                    var input = tu.Input.ValueKind == JsonValueKind.Undefined
                        ? ""
                        : JsonSerializer.Serialize(tu.Input, new JsonSerializerOptions { WriteIndented = true });
                    yield return new MessageVm(MessageKind.ToolUse, $"tool_use · {tu.Name}", input);
                    break;
                case ToolResultContent tr:
                    yield return new MessageVm(MessageKind.ToolResult,
                        tr.IsError == true ? "tool_result (error)" : "tool_result", tr.Content);
                    break;
                default:
                    yield return new MessageVm(MessageKind.System, c.GetType().Name, "");
                    break;
            }
        }
    }
}

public enum MessageKind { User, Assistant, Thinking, ToolUse, ToolResult, System }

public sealed class MessageVm
{
    public MessageKind Kind { get; }
    public string Header { get; }
    public string Body { get; }

    public MessageVm(MessageKind kind, string header, string body)
    {
        Kind = kind;
        Header = header;
        Body = body ?? "";
    }

    public string AccentColor => Kind switch
    {
        MessageKind.User => "#3b82f6",
        MessageKind.Assistant => "#22c55e",
        MessageKind.Thinking => "#a78bfa",
        MessageKind.ToolUse => "#f59e0b",
        MessageKind.ToolResult => "#6b7280",
        MessageKind.System => "#94a3b8",
        _ => "#888888",
    };
}
