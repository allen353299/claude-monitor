using System;
using ClaudeMonitor.App.ViewModels;
using ClaudeMonitor.Core.Platform;
using ClaudeMonitor.Core.Sessions;
using ClaudeMonitor.Platform.Windows;

namespace ClaudeMonitor.App.Composition;

public static class AppComposition
{
    public static PopoverViewModel CreatePopoverViewModel()
    {
        var liveSessions = new LiveSessionIndex();
        var scanner = new SessionScanner(
            liveSessions,
            new StatusAnalyzer(),
            new SessionEnricher(),
            new ProjectPathDecoder());
        var activator = CreateSessionActivator(liveSessions);
        return new PopoverViewModel(scanner, activator);
    }

    private static ISessionActivator CreateSessionActivator(ILiveSessionIndex liveSessions)
    {
        if (OperatingSystem.IsWindows()) return new WindowsSessionActivator(liveSessions);
        return new NoopSessionActivator();
    }
}
