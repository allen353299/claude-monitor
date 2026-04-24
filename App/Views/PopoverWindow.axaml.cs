using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ClaudeMonitor.App.ViewModels;
using ClaudeMonitor.Core.Persistence;

namespace ClaudeMonitor.App.Views;

public partial class PopoverWindow : Window
{
    private static readonly TimeSpan TopmostReapplyInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PersistDebounce = TimeSpan.FromMilliseconds(400);
    private const double CompactHeightThreshold = 180;

    private DispatcherTimer? _topmostTimer;
    private DispatcherTimer? _persistDebounceTimer;

    private bool _stateRestored;

    public PopoverWindow()
    {
        InitializeComponent();
        RestoreWindowState();
        UpdatePinVisual();
        UpdateTaskbarVisibility();
        Opened += (_, _) =>
        {
            ApplyTopmost();
            StartTopmostTimer();
            SyncCompactMode();
            _stateRestored = true;
        };
        Deactivated += (_, _) => ApplyTopmost();
        SizeChanged += (_, _) =>
        {
            SyncCompactMode();
            SchedulePersist();
        };
        PositionChanged += (_, _) => SchedulePersist();
        Closing += OnWindowClosing;
    }

    private void SchedulePersist()
    {
        if (!_stateRestored) return;
        if (_persistDebounceTimer is null)
        {
            _persistDebounceTimer = new DispatcherTimer(PersistDebounce, DispatcherPriority.Background, OnDebouncedPersist);
        }
        _persistDebounceTimer.Stop();
        _persistDebounceTimer.Start();
    }

    private void OnDebouncedPersist(object? sender, EventArgs e)
    {
        _persistDebounceTimer?.Stop();
        PersistWindowState();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        _persistDebounceTimer?.Stop();
        PersistWindowState();
        if (!App.IsQuitting)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void RestoreWindowState()
    {
        if (!PersistedWindowStateStore.TryLoad(out var state)) return;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint(state.X, state.Y);
        Width = state.Width;
        Height = state.Height;
        Topmost = state.Topmost;
    }

    private void PersistWindowState()
    {
        if (!_stateRestored) return;
        PersistedWindowStateStore.Save(new PersistedWindowState(
            Position.X, Position.Y, Width, Height, Topmost));
    }

    private void SyncCompactMode()
    {
        if (DataContext is PopoverViewModel vm)
            vm.IsCompact = Bounds.Height < CompactHeightThreshold;
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
        PersistWindowState();
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
        ToolTip.SetTip(pin, Topmost ? "Unpin from top" : "Pin to top");
    }

    private void StartTopmostTimer()
    {
        if (_topmostTimer != null) return;
        _topmostTimer = new DispatcherTimer(TopmostReapplyInterval, DispatcherPriority.Background, (_, _) => ApplyTopmost());
        _topmostTimer.Start();
    }

    private void ApplyTopmost()
    {
        if (!OperatingSystem.IsWindows()) return;

        var hwnd = TryGetNativeHandle();
        if (hwnd == IntPtr.Zero) return;

        var insertAfter = Topmost ? HWND_TOPMOST : HWND_NOTOPMOST;
        SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
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
