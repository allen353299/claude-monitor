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

    public Task<bool> ActivateAsync(SessionId sessionId, CancellationToken ct = default)
    {
        if (!_liveSessions.TryGetPid(sessionId, out var pid))
            return Task.FromResult(false);

        var hwnd = FindWindowForProcess(pid);
        if (hwnd == IntPtr.Zero) return Task.FromResult(false);

        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        ForceForeground(hwnd);
        return Task.FromResult(true);
    }

    private static readonly HashSet<string> ExcludedAncestorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe",
        "svchost.exe",
        "services.exe",
        "lsass.exe",
        "winlogon.exe",
        "csrss.exe",
        "wininit.exe",
    };

    private static IntPtr FindWindowForProcess(int startPid)
    {
        var (parentMap, nameMap) = BuildProcessMaps();
        var ancestors = WalkAncestors(startPid, parentMap)
            .Where(pid => !(nameMap.TryGetValue(pid, out var n) && ExcludedAncestorNames.Contains(n)))
            .ToHashSet();

        IntPtr foundByAncestor = IntPtr.Zero;
        if (ancestors.Count > 0)
        {
            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd)) return true;
                GetWindowThreadProcessId(hwnd, out var wpid);
                if (wpid == 0) return true;
                if (!ancestors.Contains((int)wpid)) return true;
                foundByAncestor = hwnd;
                return false;
            }, IntPtr.Zero);
        }

        if (foundByAncestor != IntPtr.Zero) return foundByAncestor;

        return FindWindowByTitle(new[] { "Claude Code" });
    }

    private const int WindowTitleBufferSize = 256;

    private static IntPtr FindWindowByTitle(string[] keywords)
    {
        IntPtr found = IntPtr.Zero;
        var buffer = new System.Text.StringBuilder(WindowTitleBufferSize);
        var selfPid = Environment.ProcessId;

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            GetWindowThreadProcessId(hwnd, out var wpid);
            if ((int)wpid == selfPid) return true;
            buffer.Clear();
            GetWindowTextW(hwnd, buffer, buffer.Capacity);
            var title = buffer.ToString();
            if (title.Length == 0) return true;

            foreach (var kw in keywords)
            {
                if (title.Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    found = hwnd;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }

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

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

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
