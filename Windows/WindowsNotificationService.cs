using System.Runtime.InteropServices;
using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Platform;

namespace ClaudeMonitor.Platform.Windows;

public sealed class WindowsNotificationService : INotificationService
{
    private readonly Func<IntPtr> _getWindowHandle;

    public WindowsNotificationService(Func<IntPtr> getWindowHandle)
    {
        _getWindowHandle = getWindowHandle;
    }

    public void NotifyAttention(SessionId sessionId, string title, string body)
    {
        var hwnd = _getWindowHandle();
        if (hwnd == IntPtr.Zero) return;

        var info = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = hwnd,
            dwFlags = FLASHW_TRAY | FLASHW_TIMERNOFG,
            uCount = uint.MaxValue,
            dwTimeout = 0,
        };
        FlashWindowEx(ref info);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_TRAY = 0x00000002;
    private const uint FLASHW_TIMERNOFG = 0x0000000C;

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
}
