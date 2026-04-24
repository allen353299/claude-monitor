using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace ClaudeMonitor.App.Views;

public partial class PopoverWindow : Window
{
    private DispatcherTimer? _topmostTimer;

    public PopoverWindow()
    {
        InitializeComponent();
        UpdatePinVisual();
        UpdateTaskbarVisibility();
        Opened += (_, _) =>
        {
            ApplyTopmost();
            StartTopmostTimer();
        };
        Deactivated += (_, _) => ApplyTopmost();
    }

    private void OnDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnPinClick(object? sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdatePinVisual();
        UpdateTaskbarVisibility();
        ApplyTopmost();
    }

    private void UpdateTaskbarVisibility()
    {
        ShowInTaskbar = !Topmost;
    }

    private void OnResizePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not string tag) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var edge = tag switch
        {
            "N" => WindowEdge.North,
            "S" => WindowEdge.South,
            "E" => WindowEdge.East,
            "W" => WindowEdge.West,
            "NE" => WindowEdge.NorthEast,
            "NW" => WindowEdge.NorthWest,
            "SE" => WindowEdge.SouthEast,
            "SW" => WindowEdge.SouthWest,
            _ => (WindowEdge?)null,
        };
        if (edge.HasValue) BeginResizeDrag(edge.Value, e);
    }

    private void UpdatePinVisual()
    {
        var pin = this.FindControl<Button>("PinButton");
        if (pin is null) return;
        pin.Foreground = new SolidColorBrush(Topmost
            ? Color.Parse("#ffffff")
            : Color.Parse("#666666"));
    }

    private void StartTopmostTimer()
    {
        if (_topmostTimer != null) return;
        _topmostTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => ApplyTopmost());
        _topmostTimer.Start();
    }

    private void ApplyTopmost()
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var hwnd = TryGetNativeHandle();
            if (hwnd == IntPtr.Zero) return;

            var insertAfter = Topmost ? HWND_TOPMOST : HWND_NOTOPMOST;
            SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        catch
        {
        }
    }

    private IntPtr TryGetNativeHandle()
    {
        var platformHandle = TryGetPlatformHandle();
        return platformHandle?.Handle ?? IntPtr.Zero;
    }

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint flags);
}
