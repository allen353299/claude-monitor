using System;
using System.Collections.Generic;
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
    private static readonly TimeSpan SnapDebounce = TimeSpan.FromMilliseconds(220);

    private DispatcherTimer? _topmostTimer;
    private DispatcherTimer? _persistDebounceTimer;
    private DispatcherTimer? _snapTimer;
    private double _lastCardHeight;

    private bool _stateRestored;
    private string? _currentFingerprint;

    public PopoverWindow()
    {
        InitializeComponent();
        RestoreWindowState();
        UpdatePinVisual();
        Opened += (_, _) =>
        {
            _currentFingerprint = ComputeFingerprint();
            TryApplyFingerprintState();
            EnsurePositionOnVisibleScreen();
            ApplyToolWindowStyle();
            ApplyTopmost();
            StartTopmostTimer();
            if (Screens is { } screens) screens.Changed += OnScreensChanged;
            _stateRestored = true;
        };
        Deactivated += (_, _) => ApplyTopmost();
        SizeChanged += (_, _) =>
        {
            ScheduleSnap();
            SchedulePersist();
        };
        PositionChanged += (_, _) => SchedulePersist();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible && _stateRestored)
                EnsurePositionOnVisibleScreen();
        };
        Closing += OnWindowClosing;
    }

    private void OnScreensChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(HandleLayoutChange);

    private void HandleLayoutChange()
    {
        var newFp = ComputeFingerprint();
        if (newFp == _currentFingerprint)
        {
            EnsurePositionOnVisibleScreen();
            return;
        }
        if (_currentFingerprint is not null && _stateRestored)
        {
            PersistedWindowStateStore.Save(
                PersistedWindowStateStore.DefaultFilePath,
                _currentFingerprint,
                new PersistedWindowState(Position.X, Position.Y, Width, Height, Topmost));
        }
        _currentFingerprint = newFp;
        if (newFp is not null
            && PersistedWindowStateStore.TryLoad(
                PersistedWindowStateStore.DefaultFilePath, newFp, out var state))
        {
            Position = new PixelPoint(state.X, state.Y);
            Width = state.Width;
            Height = state.Height;
            Topmost = state.Topmost;
        }
        EnsurePositionOnVisibleScreen();
    }

    private void TryApplyFingerprintState()
    {
        if (_currentFingerprint is null) return;
        if (!PersistedWindowStateStore.TryLoad(
            PersistedWindowStateStore.DefaultFilePath, _currentFingerprint, out var state))
            return;
        if (state.X == Position.X && state.Y == Position.Y
            && Math.Abs(state.Width - Width) < 0.5 && Math.Abs(state.Height - Height) < 0.5)
            return;
        Position = new PixelPoint(state.X, state.Y);
        Width = state.Width;
        Height = state.Height;
        Topmost = state.Topmost;
    }

    private string? ComputeFingerprint()
    {
        if (Screens is null || Screens.All.Count == 0) return null;
        var infos = new List<ScreenInfo>(Screens.All.Count);
        foreach (var s in Screens.All)
        {
            var area = s.WorkingArea;
            infos.Add(new ScreenInfo(area.X, area.Y, area.Width, area.Height, s.DisplayName, s.IsPrimary));
        }
        return WindowPositionPolicy.ComputeLayoutFingerprint(infos);
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

    private void EnsurePositionOnVisibleScreen()
    {
        if (Screens is null || Screens.All.Count == 0) return;
        var width = (int)Bounds.Width;
        var height = (int)Bounds.Height;
        if (width <= 0 || height <= 0) { width = (int)Width; height = (int)Height; }
        var infos = new List<ScreenInfo>(Screens.All.Count);
        foreach (var s in Screens.All)
        {
            var area = s.WorkingArea;
            infos.Add(new ScreenInfo(area.X, area.Y, area.Width, area.Height, s.DisplayName, s.IsPrimary));
        }
        var clamped = WindowPositionPolicy.ClampToVisibleScreens(Position.X, Position.Y, width, height, infos);
        if (clamped is { } pt) Position = new PixelPoint(pt.X, pt.Y);
    }

    private void PersistWindowState()
    {
        if (!_stateRestored) return;
        PersistedWindowStateStore.Save(
            PersistedWindowStateStore.DefaultFilePath,
            _currentFingerprint,
            new PersistedWindowState(Position.X, Position.Y, Width, Height, Topmost));
    }

    private void ScheduleSnap()
    {
        if (!_stateRestored) return;
        if (_snapTimer is null)
            _snapTimer = new DispatcherTimer(SnapDebounce, DispatcherPriority.Background, (_, _) => SnapToRows());
        _snapTimer.Stop();
        _snapTimer.Start();
    }

    private void SnapToRows()
    {
        _snapTimer?.Stop();
        if (!TryMeasureLayout(out var chromeH, out var cardH)) return;
        var maxCount = MaxRowCount();
        var current = Height;
        var rawCount = (int)Math.Round((current - chromeH) / cardH);
        var newCount = Math.Clamp(rawCount, 1, maxCount);
        var target = chromeH + newCount * cardH;
        if (Math.Abs(target - current) < 1) return;
        Height = target;
    }

    private void OnDecreaseRowsClick(object? sender, RoutedEventArgs e) => StepRows(-1);

    private void OnIncreaseRowsClick(object? sender, RoutedEventArgs e) => StepRows(+1);

    private void StepRows(int delta)
    {
        if (!TryMeasureLayout(out var chromeH, out var cardH)) return;
        var maxCount = MaxRowCount();
        var current = Height;
        var currentCount = Math.Max(0, (int)Math.Round((current - chromeH) / cardH));
        var newCount = Math.Clamp(currentCount + delta, 1, maxCount);
        Height = chromeH + newCount * cardH;
    }

    private int MaxRowCount()
    {
        var list = this.FindControl<ItemsControl>("SessionsList");
        return Math.Max(1, list?.ItemCount ?? 1);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var sv = this.FindControl<ScrollViewer>("SessionsScroll");
        sv?.AddHandler(InputElement.PointerWheelChangedEvent,
            OnSessionsScrollWheel,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private void OnSessionsScrollWheel(object? sender, PointerWheelEventArgs e)
    {
        var sv = this.FindControl<ScrollViewer>("SessionsScroll");
        if (sv is null) return;
        if (!TryMeasureLayout(out _, out var cardH)) return;
        var step = Math.Max(1.0, cardH / 4.0);
        var deltaY = -e.Delta.Y * step;
        var maxY = Math.Max(0, sv.Extent.Height - sv.Viewport.Height);
        if (maxY <= 0) return;
        var newY = Math.Clamp(sv.Offset.Y + deltaY, 0, maxY);
        sv.Offset = new Avalonia.Vector(sv.Offset.X, newY);
        e.Handled = true;
    }

    private bool TryMeasureLayout(out double chromeHeight, out double cardHeight)
    {
        chromeHeight = 0;
        cardHeight = 0;
        var list = this.FindControl<ItemsControl>("SessionsList");
        var area = this.FindControl<Grid>("SessionsArea");
        if (list is null || area is null) return false;

        if (list.ItemCount > 0 && list.ContainerFromIndex(0) is Control firstItem && firstItem.Bounds.Height > 0)
        {
            _lastCardHeight = firstItem.Bounds.Height;
        }
        if (_lastCardHeight < 1) return false;

        cardHeight = _lastCardHeight;
        chromeHeight = Bounds.Height - area.Bounds.Height;
        return chromeHeight > 0;
    }

    private void OnDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private const double PixelBarSlotPitch = 12;

    private void OnPixelBarSizeChanged(object? sender, Avalonia.Controls.SizeChangedEventArgs e)
    {
        if (DataContext is not PopoverViewModel vm) return;
        var width = e.NewSize.Width;
        if (width <= 0) return;
        var count = (int)Math.Floor(width / PixelBarSlotPitch);
        if (count < 1) count = 1;
        vm.Bar.SetColumnCount(count);
    }

    public event EventHandler? DashboardRequested;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnOpenDashboardClick(object? sender, RoutedEventArgs e)
        => DashboardRequested?.Invoke(this, EventArgs.Empty);

    private void OnPinClick(object? sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdatePinVisual();
        ApplyTopmost();
        PersistWindowState();
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
        _topmostTimer = new DispatcherTimer(TopmostReapplyInterval, DispatcherPriority.Background, (_, _) =>
        {
            ApplyTopmost();
            ApplyToolWindowStyle();
        });
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

    private void ApplyToolWindowStyle()
    {
        if (!OperatingSystem.IsWindows()) return;

        var hwnd = TryGetNativeHandle();
        if (hwnd == IntPtr.Zero) return;

        var current = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        var desired = (current | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        if (desired == current) return;

        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(desired));
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
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
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_APPWINDOW = 0x00040000;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
