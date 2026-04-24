using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using C9Watch.Core.Models;

namespace C9Watch.Core.Sessions;

public sealed class SessionScanner : ISessionScanner
{
    private readonly string _claudeHome;
    private readonly string _projectsRoot;
    private readonly TimeSpan _activeWindow;
    private readonly TimeSpan _fallbackInterval;
    private readonly TimeSpan _debounce;

    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debouncers = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _lifetimeCts = new();

    private FileSystemWatcher? _watcher;
    private Task? _fallbackLoop;
    private int _started;

    public SessionScanner(
        string? claudeHome = null,
        TimeSpan? activeWindow = null,
        TimeSpan? fallbackInterval = null,
        TimeSpan? debounce = null)
    {
        _claudeHome = claudeHome
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        _projectsRoot = Path.Combine(_claudeHome, "projects");
        _activeWindow = activeWindow ?? TimeSpan.FromMinutes(60);
        _fallbackInterval = fallbackInterval ?? TimeSpan.FromSeconds(5);
        _debounce = debounce ?? TimeSpan.FromMilliseconds(200);
    }

    public event EventHandler<SessionUpdatedEventArgs>? SessionUpdated;
    public event EventHandler<SessionRemovedEventArgs>? SessionRemoved;

    public IReadOnlyCollection<Session> CurrentSessions => _sessions.Values.ToArray();

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
            var sessionId = Path.GetFileNameWithoutExtension(file);
            if (!livePidSessionIds.Contains(sessionId)) continue;
            await RefreshAsync(file, ct).ConfigureAwait(false);
        }
    }

    private HashSet<string> GetLivePidSessionIds()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sessionsDir = Path.Combine(_claudeHome, "sessions");
        if (!Directory.Exists(sessionsDir)) return result;

        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(sessionsDir, "*.json"); }
        catch (IOException) { return result; }
        catch (UnauthorizedAccessException) { return result; }

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!int.TryParse(name, out var pid)) continue;
            if (!IsProcessAlive(pid)) continue;

            string? sid = null;
            try
            {
                using var stream = File.OpenRead(file);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("sessionId", out var el)
                    && el.ValueKind == JsonValueKind.String)
                {
                    sid = el.GetString();
                }
            }
            catch (IOException) { continue; }
            catch (JsonException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            if (!string.IsNullOrEmpty(sid)) result.Add(sid);
        }

        return result;
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (p.HasExited) return false;
            var name = p.ProcessName.ToLowerInvariant();
            return name.Contains("claude") || name.Contains("node");
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    private IEnumerable<string> EnumerateJsonlFiles()
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

            foreach (var f in files) yield return f;
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

    private void QueueDebouncedRefresh(string path)
    {
        if (_lifetimeCts.IsCancellationRequested) return;
        if (!path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)) return;

        var newCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);

        if (_debouncers.TryRemove(path, out var old))
        {
            try { old.Cancel(); } catch (ObjectDisposedException) { }
            old.Dispose();
        }
        _debouncers[path] = newCts;

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
                if (_debouncers.TryGetValue(path, out var current) && current == newCts)
                {
                    _debouncers.TryRemove(path, out _);
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
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var livePidSessionIds = GetLivePidSessionIds();

        foreach (var file in EnumerateJsonlFiles())
        {
            if (ct.IsCancellationRequested) return;
            seen.Add(file);

            var sessionId = Path.GetFileNameWithoutExtension(file);
            if (!livePidSessionIds.Contains(sessionId))
            {
                if (_sessions.TryRemove(file, out var removed))
                {
                    SessionRemoved?.Invoke(this, new SessionRemovedEventArgs(removed.Id));
                }
                continue;
            }

            await RefreshAsync(file, ct).ConfigureAwait(false);
        }

        foreach (var path in _sessions.Keys.ToArray())
        {
            if (!seen.Contains(path))
            {
                if (_sessions.TryRemove(path, out var removed))
                {
                    SessionRemoved?.Invoke(this, new SessionRemovedEventArgs(removed.Id));
                }
            }
        }
    }

    private async Task RefreshAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            if (_sessions.TryRemove(path, out var removed))
            {
                SessionRemoved?.Invoke(this, new SessionRemovedEventArgs(removed.Id));
            }
            return;
        }

        var mtime = SafeLastWrite(path);
        if (mtime == null) return;

        var sessionIdFromPath = Path.GetFileNameWithoutExtension(path);
        var liveIds = GetLivePidSessionIds();
        var isLive = liveIds.Contains(sessionIdFromPath);

        if (!isLive)
        {
            if (_sessions.TryRemove(path, out var removed))
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

        var status = StatusAnalyzer.DetermineStatus(entries);
        var pendingTool = StatusAnalyzer.GetPendingToolName(entries);

        if (pendingTool is not null
            && pendingTool != "Question"
            && status != SessionStatus.NeedsAttention
            && status != SessionStatus.Working)
        {
            status = SessionStatus.NeedsAttention;
        }
        var enriched = SessionEnricher.Enrich(entries);
        var customTitle = ExtractCustomTitle(entries);

        var id = Path.GetFileNameWithoutExtension(path);
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

        var isChanged = true;
        _sessions.AddOrUpdate(
            path,
            _ => session,
            (_, existing) =>
            {
                isChanged = !SessionsEqual(existing, session);
                return session;
            });

        if (isChanged)
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

    private static string ExtractProjectName(string jsonlPath)
    {
        var parentDir = Path.GetDirectoryName(jsonlPath);
        if (string.IsNullOrEmpty(parentDir)) return "unknown";

        var dirName = new DirectoryInfo(parentDir).Name;
        var trimmed = dirName.TrimStart('-');

        if (string.IsNullOrEmpty(trimmed)) return dirName;

        var segments = trimmed.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return dirName;

        return segments[^1];
    }

    private static string DecodeProjectPath(string jsonlPath)
    {
        var parentDir = Path.GetDirectoryName(jsonlPath);
        if (string.IsNullOrEmpty(parentDir)) return "";

        var dirName = new DirectoryInfo(parentDir).Name;
        return dirName.Replace('-', Path.DirectorySeparatorChar);
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

        foreach (var kv in _debouncers.ToArray())
        {
            try { kv.Value.Cancel(); } catch (ObjectDisposedException) { }
            kv.Value.Dispose();
        }
        _debouncers.Clear();

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
