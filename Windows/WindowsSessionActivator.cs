using System.Runtime.InteropServices;
using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Platform;

namespace ClaudeMonitor.Platform.Windows;

public sealed class WindowsSessionActivator : ISessionActivator
{
    private readonly ILiveSessionIndex _liveSessions;

    public WindowsSessionActivator(ILiveSessionIndex liveSessions)
    {
        _liveSessions = liveSessions;
    }

    public Task<bool> ActivateAsync(SessionId sessionId, string? titleHint = null, CancellationToken ct = default)
    {
        ActivateLog.Append($"--- Activate {sessionId.Value} hint='{titleHint}' ---");
        if (!_liveSessions.TryGetPid(sessionId, out var pid))
        {
            ActivateLog.Append("  no live PID for session");
            return Task.FromResult(false);
        }
        ActivateLog.Append($"  claude pid={pid}");

        var hwnd = FindWindowForProcess(pid, titleHint);
        if (hwnd == IntPtr.Zero)
        {
            ActivateLog.Append("  no matching window found");
            return Task.FromResult(false);
        }

        var picked = DescribeWindow(hwnd);
        ActivateLog.Append($"  PICK -> {picked}");

        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        ForceForeground(hwnd);
        return Task.FromResult(true);
    }

    private static readonly HashSet<string> ExcludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe",
        "svchost.exe",
        "services.exe",
        "lsass.exe",
        "winlogon.exe",
        "csrss.exe",
        "wininit.exe",
        "dwm.exe",
        "smss.exe",
    };

    /// <summary>
    /// Score-based window finder. Builds a "related-process" map covering:
    ///   - claude's ancestors (up the tree): catches Windows Terminal, Rider, IDEs
    ///     where the GUI host is a parent of the shell that runs claude.
    ///   - transitive descendants of those ancestors: catches conhost.exe,
    ///     which on modern Windows is reparented to the host shell. Walking
    ///     DOWN from the shell finds conhost regardless of reparenting depth.
    /// Then enumerates all visible windows once and scores each candidate by
    /// (relationKind, titleMatch, depth, area).
    /// </summary>
    internal static IntPtr FindWindowForProcess(int startPid, string? titleHint)
    {
        var (parentMap, nameMap) = BuildProcessMaps();
        var related = BuildRelatedProcessMap(startPid, parentMap, nameMap);

        ActivateLog.Append($"  ancestor chain (raw): {string.Join(" <- ", WalkAncestors(startPid, parentMap).Take(8).Select(p => $"{p}({(nameMap.TryGetValue(p, out var n) ? n : "?")})"))}");
        ActivateLog.Append($"  related-process map ({related.Count} entries):");
        foreach (var kv in related.OrderBy(k => k.Value.Kind).ThenBy(k => k.Value.Depth).Take(20))
        {
            var name = nameMap.TryGetValue(kv.Key, out var nm) ? nm : "?";
            ActivateLog.Append($"    pid={kv.Key} ({name}) kind={kv.Value.Kind} depth={kv.Value.Depth}");
        }

        var hint = string.IsNullOrWhiteSpace(titleHint) ? null : titleHint.Trim();
        var selfPid = Environment.ProcessId;
        var buffer = new System.Text.StringBuilder(WindowTitleBufferSize);

        WindowCandidate? best = null;
        var top = new List<(WindowCandidate cand, string title, string procName)>();

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            GetWindowThreadProcessId(hwnd, out var wpid);
            int pid = (int)wpid;
            if (pid == 0 || pid == selfPid) return true;
            if (!GetWindowRect(hwnd, out var rect)) return true;

            var width = Math.Max(0, rect.Right - rect.Left);
            var height = Math.Max(0, rect.Bottom - rect.Top);
            long area = (long)width * height;

            buffer.Clear();
            GetWindowTextW(hwnd, buffer, buffer.Capacity);
            var title = buffer.ToString();
            bool titleHit = hint != null && title.Length > 0
                && title.Contains(hint, StringComparison.OrdinalIgnoreCase);
            bool claudeTitle = title.Contains("Claude Code", StringComparison.OrdinalIgnoreCase);

            RelationKind kind;
            int depth;
            bool isProcessTreeRelated = related.TryGetValue(pid, out var info);
            if (isProcessTreeRelated)
            {
                kind = info.Kind;
                depth = info.Depth;
            }
            else if (titleHit)
            {
                kind = RelationKind.TitleOnly;
                depth = int.MaxValue;
            }
            else if (claudeTitle)
            {
                kind = RelationKind.ClaudeTitleOnly;
                depth = int.MaxValue;
            }
            else
            {
                return true;
            }

            // Area filter only kicks out tiny popups/tooltips for processes we
            // can't otherwise vouch for. Process-tree-related windows skip this
            // check — a minimized PowerShell/conhost has a taskbar-icon-sized
            // GetWindowRect that would otherwise be filtered out, and the
            // IsIconic+SW_RESTORE path will restore it once picked.
            if (!isProcessTreeRelated && area < 200 * 100) return true;

            var candidate = new WindowCandidate(hwnd, kind, depth, titleHit, claudeTitle, area);
            top.Add((candidate, title, nameMap.TryGetValue(pid, out var pn) ? pn : "?"));
            if (best is null || candidate.IsBetterThan(best.Value)) best = candidate;
            return true;
        }, IntPtr.Zero);

        ActivateLog.Append($"  candidates ({top.Count}):");
        foreach (var (c, t, pn) in top.OrderByDescending(x => x.cand.IsBetterThan(default)).Take(8))
        {
            ActivateLog.Append($"    hwnd=0x{c.Hwnd.ToInt64():x} proc={pn} kind={c.Kind} depth={c.Depth} titleHit={c.TitleHit} area={c.Area} title='{Truncate(t, 60)}'");
        }

        return best?.Hwnd ?? IntPtr.Zero;
    }

    internal enum RelationKind
    {
        Ancestor = 0,        // up the tree from claude
        AncestorChild = 1,   // descendant of an ancestor (e.g. conhost reparented to pwsh)
        TitleOnly = 2,       // not in process tree, but title contains the project hint
        ClaudeTitleOnly = 3, // not in process tree, title is "Claude Code"
    }

    internal readonly record struct RelatedInfo(RelationKind Kind, int Depth);

    internal readonly record struct WindowCandidate(
        IntPtr Hwnd,
        RelationKind Kind,
        int Depth,
        bool TitleHit,
        bool ClaudeTitle,
        long Area)
    {
        // Ancestor and AncestorChild collapse to one process-tree tier; within
        // the tier, the candidate closer to claude (smaller Depth) wins. This
        // is what lets a depth-1 conhost (child of pwsh) beat a depth-2 IDE
        // ancestor (e.g. Rider grand-parent) that happens to be in the chain.
        private int Tier => Kind switch
        {
            RelationKind.Ancestor or RelationKind.AncestorChild => 0,
            RelationKind.TitleOnly => 1,
            RelationKind.ClaudeTitleOnly => 2,
            _ => 3,
        };

        public bool IsBetterThan(WindowCandidate other)
        {
            // Process tree wins over coincidental title overlap (e.g. another
            // project window of the same name exists, but claude is in a
            // separate shell — we want the shell, not the unrelated window).
            if (Tier != other.Tier) return Tier < other.Tier;
            // Same tier: prefer title-hint match (multi-window IDE).
            if (TitleHit != other.TitleHit) return TitleHit;
            // Closer process-tree distance.
            if (Depth != other.Depth) return Depth < other.Depth;
            // "Claude Code" terminal title is a useful tiebreaker.
            if (ClaudeTitle != other.ClaudeTitle) return ClaudeTitle;
            // Largest visible area wins — kicks out popups / tooltips.
            return Area > other.Area;
        }
    }

    internal static Dictionary<int, RelatedInfo> BuildRelatedProcessMap(
        int startPid,
        Dictionary<int, int> parentMap,
        Dictionary<int, string> nameMap)
    {
        var related = new Dictionary<int, RelatedInfo>();

        int depth = 0;
        foreach (var pid in WalkAncestors(startPid, parentMap))
        {
            if (!IsExcluded(pid, nameMap))
                related[pid] = new RelatedInfo(RelationKind.Ancestor, depth);
            depth++;
        }

        // Build child index from parent map.
        var children = new Dictionary<int, List<int>>();
        foreach (var (childPid, parentPid) in parentMap)
        {
            if (!children.TryGetValue(parentPid, out var list))
            {
                list = new List<int>();
                children[parentPid] = list;
            }
            list.Add(childPid);
        }

        // BFS down from each ancestor: catches conhost.exe (reparented to host
        // shell), OpenConsole.exe, Code helper procs, etc.
        var queue = new Queue<int>(related.Keys.Where(k => related[k].Kind == RelationKind.Ancestor));
        var seen = new HashSet<int>(queue);
        while (queue.Count > 0)
        {
            var pid = queue.Dequeue();
            if (!children.TryGetValue(pid, out var kids)) continue;
            var parentDepth = related[pid].Depth;
            foreach (var kid in kids)
            {
                if (!seen.Add(kid)) continue;
                if (IsExcluded(kid, nameMap)) continue;
                if (!related.ContainsKey(kid))
                    related[kid] = new RelatedInfo(RelationKind.AncestorChild, parentDepth);
                queue.Enqueue(kid);
            }
        }

        return related;
    }

    private static bool IsExcluded(int pid, Dictionary<int, string> nameMap)
        => nameMap.TryGetValue(pid, out var n) && ExcludedNames.Contains(n);

    private static (Dictionary<int, int> parent, Dictionary<int, string> name) BuildProcessMaps()
    {
        var parent = new Dictionary<int, int>();
        var name = new Dictionary<int, string>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE) return (parent, name);

        try
        {
            var entry = new ProcessEntry32W
            {
                dwSize = (uint)Marshal.SizeOf<ProcessEntry32W>()
            };

            if (!Process32FirstW(snapshot, ref entry)) return (parent, name);
            do
            {
                var pid = (int)entry.th32ProcessID;
                parent[pid] = (int)entry.th32ParentProcessID;
                name[pid] = entry.szExeFile ?? "";
            }
            while (Process32NextW(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return (parent, name);
    }

    private static IEnumerable<int> WalkAncestors(int pid, Dictionary<int, int> map)
    {
        var seen = new HashSet<int>();
        var current = pid;
        while (current > 0 && seen.Add(current))
        {
            yield return current;
            if (!map.TryGetValue(current, out var parent) || parent == 0) yield break;
            current = parent;
        }
    }

    private static string DescribeWindow(IntPtr hwnd)
    {
        var buffer = new System.Text.StringBuilder(WindowTitleBufferSize);
        GetWindowTextW(hwnd, buffer, buffer.Capacity);
        GetWindowThreadProcessId(hwnd, out var wpid);
        return $"hwnd=0x{hwnd.ToInt64():x} pid={wpid} title='{Truncate(buffer.ToString(), 80)}'";
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";

    private static void ForceForeground(IntPtr hwnd)
    {
        var fgHwnd = GetForegroundWindow();
        var fgThread = GetWindowThreadProcessId(fgHwnd, out _);
        var thisThread = GetCurrentThreadId();
        var targetThread = GetWindowThreadProcessId(hwnd, out _);

        var attachedFg = false;
        var attachedTarget = false;
        try
        {
            if (fgThread != thisThread)
                attachedFg = AttachThreadInput(thisThread, fgThread, true);
            if (targetThread != thisThread && targetThread != fgThread)
                attachedTarget = AttachThreadInput(thisThread, targetThread, true);

            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            ShowWindow(hwnd, SW_SHOW);
        }
        finally
        {
            if (attachedFg) AttachThreadInput(thisThread, fgThread, false);
            if (attachedTarget) AttachThreadInput(thisThread, targetThread, false);
        }
    }

    // ---- Win32 P/Invoke ----

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;
    private const int WindowTitleBufferSize = 256;
    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32FirstW(IntPtr hSnapshot, ref ProcessEntry32W lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32NextW(IntPtr hSnapshot, ref ProcessEntry32W lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
}

internal static class ActivateLog
{
    private static readonly string LogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "claude-monitor-activate.log");
    private static readonly object Sync = new();
    private static int _lineCount;
    private const int MaxLines = 2000;

    public static string FilePath => LogPath;

    public static void Append(string message)
    {
        try
        {
            lock (Sync)
            {
                if (_lineCount > MaxLines)
                {
                    try { System.IO.File.Delete(LogPath); } catch { }
                    _lineCount = 0;
                }
                System.IO.File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
                _lineCount++;
            }
        }
        catch { }
    }
}
