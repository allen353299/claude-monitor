using System.Collections.Concurrent;
using ClaudeMonitor.Core.Models;
using ClaudeMonitor.Core.Platform;

namespace ClaudeMonitor.Core.Sessions;

public sealed class SessionScanner : ISessionScanner
{
    private readonly string _claudeHome;
    private readonly string _projectsRoot;
    private readonly TimeSpan _fallbackInterval;
    private readonly TimeSpan _debounce;

    private readonly ConcurrentDictionary<JsonlPath, Session> _sessionsByPath = new();
    private readonly ConcurrentDictionary<JsonlPath, CancellationTokenSource> _debouncersByPath = new();
    private readonly CancellationTokenSource _lifetimeCts = new();

    private FileSystemWatcher? _watcher;
    private Task? _fallbackLoop;
    private int _started;

    public SessionScanner(
        string? claudeHome = null,
        TimeSpan? fallbackInterval = null,
        TimeSpan? debounce = null)
    {
        _claudeHome = claudeHome
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        _projectsRoot = Path.Combine(_claudeHome, "projects");
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

        _fallbackLoop = Task.Run(() => FallbackLoopAsync(_lifetimeCts.Token));
    }

    private async Task InitialScanAsync(CancellationToken ct)
    {
        var livePidSessionIds = GetLivePidSessionIds();

        foreach (var file in EnumerateJsonlFiles())
        {
            ct.ThrowIfCancellationRequested();
            var sessionId = SessionId.From(Path.GetFileNameWithoutExtension(file.Value));
            if (!livePidSessionIds.Contains(sessionId)) continue;
            await RefreshAsync(file, ct).ConfigureAwait(false);
        }
    }

    private HashSet<SessionId> GetLivePidSessionIds()
    {
        var snapshot = LiveSessionIndex.Snapshot(_claudeHome);
        return new HashSet<SessionId>(snapshot.Keys);
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

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_debounce, newCts.Token).ConfigureAwait(false);
                await RefreshAsync(path, newCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
            finally
            {
                if (_debouncersByPath.TryGetValue(path, out var current) && current == newCts)
                {
                    _debouncersByPath.TryRemove(path, out _);
                }
                newCts.Dispose();
            }
        });
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
        var livePidSessionIds = GetLivePidSessionIds();

        foreach (var file in EnumerateJsonlFiles())
        {
            if (ct.IsCancellationRequested) return;
            seenPaths.Add(file);

            var sessionId = SessionId.From(Path.GetFileNameWithoutExtension(file.Value));
            if (!livePidSessionIds.Contains(sessionId))
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

        var mtime = SafeLastWrite(path.Value);
        if (mtime == null) return;

        var sessionIdFromPath = SessionId.From(Path.GetFileNameWithoutExtension(path.Value));
        var liveIds = GetLivePidSessionIds();
        var isLive = liveIds.Contains(sessionIdFromPath);

        if (!isLive)
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

        var status = StatusAnalyzer.DetermineStatus(entries, mtime);
        var pendingTool = StatusAnalyzer.GetPendingToolName(entries);
        var enriched = SessionEnricher.Enrich(entries);
        var customTitle = ExtractCustomTitle(entries);

        var id = SessionId.From(Path.GetFileNameWithoutExtension(path.Value));
        var projectName = ExtractProjectName(path);
        var projectPath = DecodeProjectPath(path);

        var session = new Session(
            Id: id,
            SessionName: projectName,
            CustomTitle: customTitle,
            ProjectPath: projectPath,
            JsonlPath: path,
            Status: status,
            PendingTool: pendingTool,
            FirstPrompt: enriched.FirstPrompt,
            LatestMessage: enriched.LatestMessage,
            Recap: enriched.Recap,
            MessageCount: enriched.MessageCount,
            IsLive: isLive,
            LastActivity: mtime.Value);

        Session? previous = null;
        _sessionsByPath.AddOrUpdate(
            path,
            _ => session,
            (_, existing) => { previous = existing; return session; });

        if (previous is null || !SessionsEqual(previous, session))
        {
            SessionUpdated?.Invoke(this, new SessionUpdatedEventArgs(session));
        }
    }

    private static bool SessionsEqual(Session a, Session b)
    {
        return a.Id == b.Id
            && a.SessionName == b.SessionName
            && a.CustomTitle == b.CustomTitle
            && a.JsonlPath == b.JsonlPath
            && a.Status == b.Status
            && a.PendingTool == b.PendingTool
            && a.FirstPrompt == b.FirstPrompt
            && a.LatestMessage == b.LatestMessage
            && a.Recap == b.Recap
            && a.MessageCount == b.MessageCount
            && a.IsLive == b.IsLive
            && a.LastActivity == b.LastActivity;
    }

    private static string ExtractProjectName(JsonlPath jsonlPath)
    {
        var parentDir = Path.GetDirectoryName(jsonlPath.Value);
        if (string.IsNullOrEmpty(parentDir)) return "unknown";

        var dirName = new DirectoryInfo(parentDir).Name;
        var trimmed = dirName.TrimStart('-');

        if (string.IsNullOrEmpty(trimmed)) return dirName;

        var segments = trimmed.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return dirName;

        return segments[^1];
    }

    private static ProjectPath DecodeProjectPath(JsonlPath jsonlPath)
    {
        var parentDir = Path.GetDirectoryName(jsonlPath.Value);
        if (string.IsNullOrEmpty(parentDir)) return ProjectPath.From("unknown");

        var dirName = new DirectoryInfo(parentDir).Name;
        var decoded = dirName.Replace('-', Path.DirectorySeparatorChar);
        return ProjectPath.From(string.IsNullOrWhiteSpace(decoded) ? "unknown" : decoded);
    }

    private static string? ExtractCustomTitle(IReadOnlyList<SessionEntry> entries)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] is CustomTitleEntry ct && !string.IsNullOrWhiteSpace(ct.CustomTitle))
            {
                return ct.CustomTitle;
            }
        }
        return null;
    }

    private static DateTimeOffset? SafeLastWrite(string path)
    {
        try
        {
            return new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
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
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
        }

        _lifetimeCts.Dispose();
    }
}
