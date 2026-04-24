# claude-monitor

A lightweight Windows popover for monitoring all your Claude Code sessions at a glance.

Always-on-top floating window showing live session status, with one-click focus to the matching terminal.

## Features

- **Live status per session**
  - `Processing` (green) — Claude is actively running a tool or generating output
  - `Needs attention` (orange, blinking) — a tool call awaits your approval
  - `Done` (cyan, blinking for 1 hour) — the turn just ended, ready for next prompt
  - `Idle` (gray) — ended turn more than an hour ago
- **Live-only list** — only shows sessions whose `claude.exe` process is actually alive
- **Click to focus** — click a card to bring its terminal window to the front (Windows Terminal, Rider, VS Code, PowerShell, etc.)
- **Recap line** — when a session disconnects, shows the `away_summary` Claude wrote before detaching
- **Pin toggle** — always-on-top when pinned (📌 white); hides to taskbar when unpinned (📌 gray)
- **Resizable** — drag any edge or corner
- **Draggable** — drag the top bar

## Requirements

- Windows 10 / 11
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (already satisfied if you have the SDK)
- Claude Code installed and running in any terminal

## Build & run

```bash
git clone https://github.com/allen353299/claude-monitor.git
cd claude-monitor
dotnet build
dotnet run --project src/ClaudeMonitor.App
```

Or double-click `run.bat` on Windows.

## Project layout

```
src/
  ClaudeMonitor.Core/                 cross-platform: JSONL parser, status analyzer, session scanner
  ClaudeMonitor.Platform.Windows/     Win32 P/Invoke: find & focus terminal windows
  ClaudeMonitor.Platform.Mac/         (stub, reserved for future)
  ClaudeMonitor.App/                  Avalonia UI (XAML, ViewModels)
tests/
  ClaudeMonitor.Core.Tests/           xUnit unit tests for StatusAnalyzer (31 tests)
```

## Tech stack

- **.NET 8** + **Avalonia 11** — cross-platform desktop UI
- **CommunityToolkit.Mvvm** — lightweight MVVM
- **Win32 P/Invoke** — `EnumWindows`, `Toolhelp32`, `SetForegroundWindow` for window activation

## Credit

Based on [minchenlee/c9watch](https://github.com/minchenlee/c9watch) — a macOS-first menu-bar app in Tauri. The status-detection algorithm and popover design are ported from the original.

Icon is from the original repo (MIT).

## License

MIT — see [LICENSE](LICENSE).
