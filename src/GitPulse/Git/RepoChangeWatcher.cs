namespace GitPulse.Git;

/// <summary>Tells the watch loop that the repository may have changed.</summary>
internal interface IChangeSignal : IDisposable
{
    /// <summary>True once per change (or burst of changes); resets when read.</summary>
    bool ConsumeChange();
}

/// <summary>
/// Notices new commits, moved branches and new tags in two ways:
/// <list type="bullet">
/// <item>A <see cref="FileSystemWatcher"/> on <c>.git</c> (HEAD, refs, packed-refs): instant where it works.</item>
/// <item>Polling a cheap fingerprint of HEAD, branch tips and tags every <c>interval</c>: always works,
/// including WSL watching a repository on a Windows drive (<c>/mnt/c/…</c>), where no file events arrive.</item>
/// </list>
/// Only reads the repository.
/// </summary>
internal sealed class RepoChangeWatcher : IChangeSignal
{
    private readonly string _path;
    private readonly TimeSpan _interval;
    private readonly FileSystemWatcher? _watcher;
    private readonly object _gate = new();

    private string _fingerprint;
    private DateTime _nextPoll;
    private bool _changed;

    public RepoChangeWatcher(string repositoryPath, TimeSpan interval)
    {
        _path = repositoryPath;
        _interval = interval;
        _fingerprint = Fingerprint() ?? string.Empty;
        _nextPoll = DateTime.UtcNow + interval;

        string gitDirectory;
        using (var reader = RepositoryReader.Open(repositoryPath))
        {
            gitDirectory = reader.GitDirectory;
        }

        try
        {
            _watcher = new FileSystemWatcher(gitDirectory)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
            };
            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
            _watcher.Deleted += OnFileEvent;
            _watcher.Renamed += OnFileEvent;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception e) when (e is IOException or ArgumentException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            _watcher = null; // polling still works
        }
    }

    /// <summary>Whether file events are being received (otherwise only polling).</summary>
    public bool UsesFileEvents => _watcher is not null;

    public bool ConsumeChange()
    {
        if (DateTime.UtcNow >= _nextPoll)
        {
            _nextPoll = DateTime.UtcNow + _interval;
            if (Fingerprint() is { } now && now != _fingerprint)
            {
                _fingerprint = now;
                lock (_gate)
                {
                    _changed = true;
                }
            }
        }

        lock (_gate)
        {
            var changed = _changed;
            _changed = false;
            if (changed)
            {
                // Remember the new state, so the poll doesn't report the same change again.
                _fingerprint = Fingerprint() ?? _fingerprint;
            }

            return changed;
        }
    }

    /// <summary>Only files that move when history changes; not the index or objects (busy during any git command).</summary>
    internal static bool IsRelevant(string relativePath)
    {
        var path = relativePath.Replace('\\', '/');
        return path is "HEAD" or "packed-refs" or "logs/HEAD"
               || path.StartsWith("refs/", StringComparison.Ordinal)
               || path == "refs";
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        var relative = Path.GetRelativePath(((FileSystemWatcher)sender).Path, e.FullPath);
        if (IsRelevant(relative) || (e is RenamedEventArgs r && IsRelevant(Path.GetRelativePath(((FileSystemWatcher)sender).Path, r.OldFullPath))))
        {
            lock (_gate)
            {
                _changed = true;
            }
        }
    }

    private string? Fingerprint()
    {
        try
        {
            using var reader = RepositoryReader.Open(_path);
            return reader.StateFingerprint();
        }
        catch (Exception e) when (e is LibGit2Sharp.LibGit2SharpException or IOException or GitPulseException)
        {
            return null; // e.g. a git command holds a lock right now; try again next time
        }
    }

    public void Dispose() => _watcher?.Dispose();
}
