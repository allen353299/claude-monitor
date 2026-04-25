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
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (already satisfied if you have the SDK)
- Claude Code installed and running in any terminal

## Build & run

```bash
git clone https://github.com/allen353299/claude-monitor.git
cd claude-monitor
dotnet build
dotnet run --project App
```

Or double-click `cm.bat` on Windows.

## Project layout

```
Core/              cross-platform: JSONL parser, status analyzer, session scanner
Windows/           Win32 P/Invoke: find & focus terminal windows
Mac/               (stub, reserved for future)
App/               Avalonia UI (XAML, ViewModels)
Core.UnitTests/    xUnit unit tests (138 tests)
```

## Tech stack

- **.NET 10** + **Avalonia 12** — cross-platform desktop UI
- **CommunityToolkit.Mvvm** — lightweight MVVM
- **Win32 P/Invoke** — `EnumWindows`, `Toolhelp32`, `SetForegroundWindow` for window activation

## Credit

Based on [minchenlee/c9watch](https://github.com/minchenlee/c9watch) — a macOS-first menu-bar app in Tauri. The status-detection algorithm and popover design are ported from the original.

Icon is from the original repo (MIT).

## License

MIT — see [LICENSE](LICENSE).
