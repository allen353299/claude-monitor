using System;
using ClaudeMonitor.App.ViewModels;
using ClaudeMonitor.Core.Models;
using ClaudeMonitor.Core.Platform;
using ClaudeMonitor.Core.Sessions;
using ClaudeMonitor.Platform.Windows;

namespace ClaudeMonitor.App.Composition;

public static class AppComposition
{
    public static PopoverViewModel CreatePopoverViewModel(Func<IntPtr> getMainWindowHandle)
    {
        var liveSessions = new LiveSessionIndex();
        var scanner = new SessionScanner(
            liveSessions,
            new StatusAnalyzer(),
            new SessionEnricher(),
            new ProjectPathDecoder());
        var activator = CreateSessionActivator(liveSessions);
        var notifications = CreateNotificationService(getMainWindowHandle);
        return new PopoverViewModel(scanner, activator, notifications);
    }

    public static DashboardViewModel CreateDashboardViewModel(PopoverViewModel popover)
    {
        var decoder = new ProjectPathDecoder();
        return new DashboardViewModel(
            popover,
            new HistoryReader(),
            new MemoryReader(decoder),
            new CostReader(decoder),
            new SessionDiagnosticsReader());
    }

    private static ISessionActivator CreateSessionActivator(ILiveSessionIndex liveSessions)
    {
        if (OperatingSystem.IsWindows()) return new WindowsSessionActivator(liveSessions);
        return new NoopSessionActivator();
    }

    private static INotificationService CreateNotificationService(Func<IntPtr> getMainWindowHandle)
    {
        if (OperatingSystem.IsWindows()) return new WindowsNotificationService(getMainWindowHandle);
        return new NoopNotificationService();
    }
}
