# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build, run, test

This is a .NET 10 solution. The solution file is `ClaudeMonitor.slnx` (the new XML solution format — not `.sln`).

```bash
dotnet build                                  # build everything
dotnet run --project App                      # run the app (Windows desktop)
dotnet test Core.UnitTests                    # run all unit tests
dotnet test Core.UnitTests --filter "FullyQualifiedName~StatusAnalyzer"  # single test class
dotnet test Core.UnitTests --filter "DisplayName~specific test name"      # single test
```

`cm.bat` at the repo root is a convenience launcher: it does `dotnet build App\App.csproj` then starts `App\bin\Debug\net10.0\ClaudeMonitor.App.exe`.

`nuget.config` pins package sources to nuget.org only — do not add other feeds.

## Project layout & dependency direction

Five projects in `ClaudeMonitor.slnx`:

- **`Core/`** — pure .NET, no UI, no platform code. Owns the domain: JSONL parsing (`SessionParser`), status detection (`StatusAnalyzer`), file watching (`SessionScanner`), live session detection (`LiveSessionIndex`), cost reading, history, memory reader, project-path encoding/decoding, persisted window state.
- **`Windows/`** — Win32 P/Invoke implementations of `Core.Platform` interfaces (`ISessionActivator` → `WindowsSessionActivator`, `INotificationService` → `WindowsNotificationService`). Uses `EnumWindows`, `Toolhelp32`, `SetForegroundWindow`, `FlashWindowEx`, `MessageBeep`.
- **`Mac/`** — stub, reserved for future macOS port.
- **`App/`** — Avalonia 12 UI. XAML views in `Views/`, view-models in `ViewModels/`, theme/assets, and the composition root `Composition/AppComposition.cs`. References both `Core` and `Windows`.
- **`Core.UnitTests/`** — xUnit, references `Core` only.

Dependency rule: `Core` depends on nothing; `Windows`/`Mac` depend on `Core`; `App` depends on `Core` + `Windows`. Keep platform-specific code (Win32, Cocoa, file dialogs that vary by OS) out of `Core`. When adding a platform capability, define an interface under `Core/Platform/`, implement it in `Windows/`, and wire it in `AppComposition` with an `OperatingSystem.IsWindows()` branch and a `Noop*` fallback in `Core/Platform/`.

## Composition

`App/Composition/AppComposition.cs` is the single composition root — there is no DI container. View-models are constructed there with their concrete dependencies. `App.axaml.cs` owns the `PopoverWindow` / `DashboardWindow` lifecycle. Visibility is asymmetric: opening the dashboard from the popover (or via tray) keeps the popover visible, but showing the popover from the dashboard or via the tray "Popover" toggle hides the dashboard. The popover view-model is created first; the dashboard view-model is lazily created on first show and receives the popover VM so it shares the same live `SessionScanner` state.

## Live session detection

The app only shows sessions whose Claude CLI process is currently running. `LiveSessionIndex` reads `~/.claude/sessions/<pid>.json`, checks the PID is alive and named `claude` or `node`, and maps `sessionId → pid`. `SessionScanner` filters all `~/.claude/projects/**/*.jsonl` files through this index — a JSONL file with no live PID is ignored (and emits `SessionRemoved` if it was previously tracked). When changing scanner behavior, remember that a missing `~/.claude/sessions/` directory or a stale PID file is a normal state, not an error.

## Window state persistence

Popover and dashboard positions are persisted to `%APPDATA%\ClaudeMonitor\window.json` and `dashboard.json`. The file holds a `Configurations` dictionary keyed by **monitor-layout fingerprint** (`Core.Persistence.WindowPositionPolicy.ComputeLayoutFingerprint` — uses `Screen.DisplayName` when available, falls back to bounds) plus a `Last` fallback used when the current layout has no entry yet. Both windows compute the fingerprint in `Opened` and on `Screens.Changed`, save the previous layout's state before applying the new one, and re-clamp to a visible screen via `WindowPositionPolicy.ClampToVisibleScreens`. Legacy single-state JSON files (pre-fingerprint schema) are auto-imported into `Last` on first load.

## Status detection

`StatusAnalyzer.DetermineStatus` walks the *tail* of session entries (parser only reads the last 20 lines via `ParseTailAsync`) and applies these rules in order:

- Empty / no user-or-assistant entry → `Connecting`
- Last entry is a `UserEntry` tool-result → `Working` if recent (<30s), else `WaitingForInput`
- Last entry is a `UserEntry` and file mtime is older than 15s → `NeedsAttention` (permission prompt has gone stale)
- Last entry is an `AssistantEntry` with a pending `AskUserQuestion` tool-use → `NeedsAttention`
- Last assistant message ends with `?` and has been idle (>20s) → `NeedsAttention`
- Pending tool-use without matching tool-result → `Working`
- Otherwise read `stop_reason` (`end_turn`/`max_tokens`/`stop_sequence` → `WaitingForInput`, `tool_use` → `Working`)

The recency thresholds (`PermissionStaleSeconds = 15`, `UserRecencySeconds = 30`, `AssistantRecencySeconds = 20`) are tuned and unit-tested. Changing them will break tests in `StatusAnalyzerTests` / `StatusTransitionTests`.

## File watching

`SessionScanner` runs both a `FileSystemWatcher` (debounced 200ms per path) and a 5-second fallback full-scan loop — keep both. The fallback exists because `FileSystemWatcher` misses events under load and on some network/virtualized filesystems. JSONL writes are coalesced via per-path debounce; do not remove the debounce or you will reparse on every byte appended.

## Coding conventions to follow

- `<Nullable>enable</Nullable>` everywhere — handle nulls explicitly, do not suppress with `!` casually.
- `LangVersion=latest`. File-scoped namespaces, primary constructors, records (`Session` is a record), and target-typed `new()` are used throughout.
- Strong types over primitives for identifiers and paths: `SessionId`, `JsonlPath`, `ProjectPath`. Construct via `From(...)` factories, not raw strings, to keep the type system honest.
- `Core` uses `ImplicitUsings`; `App` does not (it lists `using` per file).
- Avalonia uses compiled bindings by default (`AvaloniaUseCompiledBindingsByDefault=true`) — XAML must declare `x:DataType` for `{Binding}` to resolve.

## Tests

xUnit, no mocking framework — tests use real types and a `TempDir` helper for filesystem fixtures. When adding a test that needs a JSONL session, write fixture JSONL into a `TempDir` rather than mocking `SessionParser`. Tests cover parser, status analyzer, status transitions, enricher, history reader, cost calculator/reader, memory reader, project-path decoder, pixel-bar distribution, persisted window state, and the live session index.
