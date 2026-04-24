using System.Collections.Concurrent;
using ClaudeMonitor.Core.Models;
using ClaudeMonitor.Core.Platform;

namespace ClaudeMonitor.Core.Sessions;

public sealed class SessionScanner : ISessionScanner
{
    private readonly string _projectsRoot;
    private readonly TimeSpan _fallbackInterval;
    private readonly TimeSpan _debounce;
    private readonly ILiveSessionIndex _liveSessions;
    private readonly IStatusAnalyzer _statusAnalyzer;
    private readonly ISessionEnricher _enricher;
    private readonly IProjectPathDecoder _projectPathDecoder;

    private readonly ConcurrentDictionary<JsonlPath, Session> _sessionsByPath = new();
    private readonly ConcurrentDictionary<JsonlPath, CancellationTokenSource> _debouncersByPath = new();
    private readonly CancellationTokenSource _lifetimeCts = new();

    private FileSystemWatcher? _watcher;
    private Task? _fallbackLoop;
    private int _started;

    public SessionScanner(
        ILiveSessionIndex liveSessions,
        IStatusAnalyzer statusAnalyzer,
        ISessionEnricher enricher,
        IProjectPathDecoder projectPathDecoder,
        string? claudeHome = null,
        TimeSpan? fallbackInterval = null,
        TimeSpan? debounce = null)
    {
        _liveSessions = liveSessions;
        _statusAnalyzer = statusAnalyzer;
        _enricher = enricher;
        _projectPathDecoder = projectPathDecoder;
        var home = claudeHome
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        _projectsRoot = Path.Combine(home, "projects");
        _fallbackInterval = fallbackInterval ?? TimeSpan.FromSeconds(5);
        _debounce = debounce ?? TimeSpan.FromMilliseconds(200);
    }

    public event EventHandler<SessionUpdatedEventArgs>? SessionUpdated;
    public event EventHandler<SessionRemovedEventArgs>? SessionRemoved;

    public IReadOnlyCollection<Session> CurrentSessions => _sessionsByPath.Values.ToArray();

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        if (!Directory.Exists(_projectsRoot))
        {
            try
            {
                Directory.CreateDirectory(_projectsRoot);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        await InitialScanAsync(ct).ConfigureAwait(false);

        if (Directory.Exists(_projectsRoot))
        {
            _watcher = new FileSystemWatcher(_projectsRoot)
            {
                IncludeSubdirectories = true,
                Filter = "*.jsonl",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            _watcher.Created += OnFileEvent;
            _watcher.Changed += OnFileEvent;
            _watcher.Renamed += OnFileRenamed;
            _watcher.EnableRaisingEvents = true;
        }

        _fallbackLoop = FallbackLoopAsync(_lifetimeCts.Token);
    }

    private async Task InitialScanAsync(CancellationToken ct)
    {
        var liveSessionIds = _liveSessions.Snapshot().Keys.ToHashSet();

        foreach (var file in EnumerateJsonlFiles())
        {
            ct.ThrowIfCancellationRequested();
            var sessionId = SessionId.From(Path.GetFileNameWithoutExtension(file.Value));
            if (!liveSessionIds.Contains(sessionId)) continue;
            await RefreshAsync(file, ct).ConfigureAwait(false);
        }
    }

    private IEnumerable<JsonlPath> EnumerateJsonlFiles()
    {
        if (!Directory.Exists(_projectsRoot)) yield break;

        IEnumerable<string> projectDirs;
        try
        {
            projectDirs = Directory.EnumerateDirectories(_projectsRoot);
        }
        catch (IOException) { yield break; }
        catch (UnauthorizedAccessException) { yield break; }

        foreach (var dir in projectDirs)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(dir, "*.jsonl");
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var f in files) yield return JsonlPath.From(f);
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        QueueDebouncedRefresh(e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        QueueDebouncedRefresh(e.FullPath);
    }

    private void QueueDebouncedRefresh(string rawPath)
    {
        if (_lifetimeCts.IsCancellationRequested) return;
        if (!rawPath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)) return;

        var path = JsonlPath.From(rawPath);
        var newCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);

        if (_debouncersByPath.TryRemove(path, out var old))
        {
            try { old.Cancel(); } catch (ObjectDisposedException) { }
            old.Dispose();
        }
        _debouncersByPath[path] = newCts;

        _ = DebounceAndRefreshAsync(path, newCts);
    }

    private async Task DebounceAndRefreshAsync(JsonlPath path, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(_debounce, cts.Token).ConfigureAwait(false);
            await RefreshAsync(path, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        finally
        {
            if (_debouncersByPath.TryGetValue(path, out var current) && current == cts)
            {
                _debouncersByPath.TryRemove(path, out _);
            }
            cts.Dispose();
        }
    }

    private async Task FallbackLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_fallbackInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await FullScanAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task FullScanAsync(CancellationToken ct)
    {
        var seenPaths = new HashSet<JsonlPath>();
        var liveSessionIds = _liveSessions.Snapshot().Keys.ToHashSet();

        foreach (var file in EnumerateJsonlFiles())
        {
            if (ct.IsCancellationRequested) return;
            seenPaths.Add(file);

            var sessionId = SessionId.From(Path.GetFileNameWithoutExtension(file.Value));
            if (!liveSessionIds.Contains(sessionId))
            {
                if (_sessionsByPath.TryRemove(file, out var removed))
                {
                    SessionRemoved?.Invoke(this, new SessionRemovedEventArgs(removed.Id));
                }
                continue;
            }

            await RefreshAsync(file, ct).ConfigureAwait(false);
        }

        foreach (var path in _sessionsByPath.Keys.ToArray())
        {
            if (!seenPaths.Contains(path))
            {
                if (_sessionsByPath.TryRemove(path, out var removed))
                {
                    SessionRemoved?.Invoke(this, new SessionRemovedEventArgs(removed.Id));
                }
            }
        }
    }

    private async Task RefreshAsync(JsonlPath path, CancellationToken ct)
    {
        if (!File.Exists(path.Value))
        {
            if (_sessionsByPath.TryRemove(path, out var removed))
            {
                SessionRemoved?.Invoke(this, new SessionRemovedEventArgs(removed.Id));
            }
            return;
        }

        if (!path.TryGetLastWrite(out var lastActivity)) return;

        var id = SessionId.From(Path.GetFileNameWithoutExtension(path.Value));
        var liveSessionIds = _liveSessions.Snapshot().Keys.ToHashSet();
        if (!liveSessionIds.Contains(id))
        {
            if (_sessionsByPath.TryRemove(path, out var removed))
            {
                SessionRemoved?.Invoke(this, new SessionRemovedEventArgs(removed.Id));
            }
            return;
        }

        IReadOnlyList<SessionEntry> entries;
        try
        {
            entries = await SessionParser.ParseTailAsync(path, maxLines: 20, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var enriched = _enricher.Enrich(entries);
        var session = new Session(
            Id: id,
            SessionName: _projectPathDecoder.ExtractProjectName(path),
            CustomTitle: enriched.CustomTitle,
            ProjectPath: _projectPathDecoder.Decode(path),
            JsonlPath: path,
            Status: _statusAnalyzer.DetermineStatus(entries, lastActivity),
            PendingTool: _statusAnalyzer.GetPendingToolName(entries),
            FirstPrompt: enriched.FirstPrompt,
            LatestMessage: enriched.LatestMessage,
            Recap: enriched.Recap,
            MessageCount: enriched.MessageCount,
            IsLive: true,
            LastActivity: lastActivity);

        Session? previous = null;
        _sessionsByPath.AddOrUpdate(
            path,
            _ => session,
            (_, existing) => { previous = existing; return session; });

        if (previous != session)
        {
            SessionUpdated?.Invoke(this, new SessionUpdatedEventArgs(session));
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _lifetimeCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        if (_watcher != null)
        {
            try
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnFileEvent;
                _watcher.Changed -= OnFileEvent;
                _watcher.Renamed -= OnFileRenamed;
                _watcher.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
            _watcher = null;
        }

        foreach (var kv in _debouncersByPath.ToArray())
        {
            try { kv.Value.Cancel(); } catch (ObjectDisposedException) { }
            kv.Value.Dispose();
        }
        _debouncersByPath.Clear();

        if (_fallbackLoop != null)
        {
            try
            {
                await _fallbackLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }

        _lifetimeCts.Dispose();
    }
}
