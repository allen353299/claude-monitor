# claude-monitor

A Windows desktop monitor for Claude Code sessions — see at a glance which session is working, which needs you, which is idle.

## Highlights

- **Live status detection** — parses `~/.claude/projects/**/*.jsonl` and classifies each session as Processing / Needs attention / Done / Idle. Reads pending tool-use entries, applies auto-approved permission rules, and uses recency thresholds to distinguish "just stopped" from "actually idle".
- **Animated pixel status bar** — 28-column visual breakdown across all sessions, with a sweeping wave through the Processing blocks. Same widget renders in the popover header and the dashboard monitor tab.
- **Floating popover** — always-on-top, click any session card to focus its terminal (Windows Terminal, Rider, VS Code, Cursor, PowerShell, …).
- **Taskbar flash + beep** — when a session transitions to Needs attention or just finished its turn, the popover flashes in the taskbar and the system asterisk sound plays once.
- **Embedded dashboard** with five tabs:
  - **Monitor** — same live cards as the popover
  - **History** — every session ever, with USD spend per session and grand total, searchable, click to open the conversation
  - **Memory** — browse the `~/.claude` memory hierarchy (CLAUDE.md, agents, commands)
  - **Conversation** — embedded viewer with thinking and tool-use toggles, virtualized list for long sessions
  - **Settings** — checks GitHub releases for new versions
- **Popover ↔ Dashboard mutex** — only one window is on screen at a time. Switch with the link icon in the popover header or the POPOVER button at the top-right of the dashboard.
- **Persistent window state** — both windows remember location and size between launches.
- **Tray icon** — show / hide popover, open dashboard, quit.

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
Core/              JSONL parser, status analyzer, session scanner, cost reader, memory reader
Windows/           Win32 P/Invoke: focus terminal, FlashWindowEx, MessageBeep
Mac/               (stub, reserved for future)
App/               Avalonia UI (XAML, ViewModels, composition root)
Core.UnitTests/    xUnit (138 tests)
```

## Tech stack

- **.NET 10** + **Avalonia 12** — cross-platform desktop UI
- **CommunityToolkit.Mvvm** — lightweight MVVM with source generators
- **Win32 P/Invoke** — `EnumWindows`, `Toolhelp32`, `SetForegroundWindow`, `FlashWindowEx`, `MessageBeep`

## Credit

Based on [minchenlee/c9watch](https://github.com/minchenlee/c9watch) — a macOS-first menu-bar app in Tauri. The status-detection algorithm and popover design are ported from the original.

Icon is from the original repo (MIT).

## License

MIT — see [LICENSE](LICENSE).
