using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitPulse.Git;
using GitPulse.Models;

namespace GitPulse.Caching;

/// <summary>
/// Where scan caches live. Never inside the repository (GitPulse doesn't write to repos):
/// <c>$GITPULSE_CACHE_DIR</c>, else the user's cache folder (<c>%LOCALAPPDATA%\GitPulse\Cache</c>,
/// <c>~/Library/Caches/GitPulse</c>, or <c>$XDG_CACHE_HOME/gitpulse</c> / <c>~/.cache/gitpulse</c>).
/// </summary>
internal static class CacheLocation
{
    public const string EnvironmentVariable = "GITPULSE_CACHE_DIR";

    public static string Directory
    {
        get
        {
            if (Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } custom)
            {
                return custom;
            }

            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitPulse", "Cache");
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(home, "Library", "Caches", "GitPulse");
            }

            var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            return Path.Combine(string.IsNullOrEmpty(xdg) ? Path.Combine(home, ".cache") : xdg, "gitpulse");
        }
    }

    /// <summary>One file per repository, named by a hash of its .git path.</summary>
    public static string FileFor(string gitDirectory) =>
        Path.Combine(Directory, Key(gitDirectory) + ".json");

    internal static string NormalizedPath(string gitDirectory)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gitDirectory));
        return OperatingSystem.IsWindows() ? full.ToLowerInvariant() : full;
    }

    private static string Key(string gitDirectory) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizedPath(gitDirectory))))[..24];
}

/// <summary>
/// Per-commit file stats for one repository, stored as JSON. A commit's stats never change, so
/// a later scan only diffs commits it has not seen: that is the incremental update. A missing,
/// unreadable or old-format file is simply an empty cache; failing to save is not an error either.
/// </summary>
internal sealed class ScanCache : ICommitStatsCache
{
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly Dictionary<string, IReadOnlyList<FileChange>> _commits;
    private readonly string _repository;
    private bool _dirty;

    private ScanCache(string path, string repository, Dictionary<string, IReadOnlyList<FileChange>> commits, bool loadFailed)
    {
        FilePath = path;
        _repository = repository;
        _commits = commits;
        LoadFailed = loadFailed;
    }

    public string FilePath { get; }

    /// <summary>The file existed but could not be used (corrupt, other version, other repository).</summary>
    public bool LoadFailed { get; }

    public int Count => _commits.Count;

    public int Hits { get; private set; }

    public int Misses { get; private set; }

    public static ScanCache Load(string gitDirectory, string? path = null)
    {
        path ??= CacheLocation.FileFor(gitDirectory);
        var repository = CacheLocation.NormalizedPath(gitDirectory);
        var empty = new Dictionary<string, IReadOnlyList<FileChange>>(StringComparer.Ordinal);

        if (!File.Exists(path))
        {
            return new ScanCache(path, repository, empty, loadFailed: false);
        }

        try
        {
            using var stream = File.OpenRead(path);
            var file = JsonSerializer.Deserialize<CacheFile>(stream, Json);
            if (file is null || file.Version != FormatVersion || file.Repository != repository || file.Commits is null)
            {
                return new ScanCache(path, repository, empty, loadFailed: true);
            }

            var commits = file.Commits.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<FileChange>)kv.Value.Select(f => new FileChange(f.Path, f.Added, f.Deleted)).ToList(),
                StringComparer.Ordinal);
            return new ScanCache(path, repository, commits, loadFailed: false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new ScanCache(path, repository, empty, loadFailed: true);
        }
    }

    public bool TryGet(string sha, out IReadOnlyList<FileChange> files)
    {
        if (_commits.TryGetValue(sha, out var found))
        {
            Hits++;
            files = found;
            return true;
        }

        files = [];
        return false;
    }

    public void Put(string sha, IReadOnlyList<FileChange> files)
    {
        Misses++;
        _commits[sha] = files;
        _dirty = true;
    }

    /// <summary>Forgets commits that are no longer in the history (after a rebase or a deleted branch).</summary>
    public void Prune(IEnumerable<string> reachable)
    {
        var keep = reachable.ToHashSet(StringComparer.Ordinal);
        foreach (var sha in _commits.Keys.Where(k => !keep.Contains(k)).ToList())
        {
            _commits.Remove(sha);
            _dirty = true;
        }
    }

    /// <returns>False when the cache could not be written; GitPulse works without it.</returns>
    public bool Save()
    {
        if (!_dirty && !LoadFailed)
        {
            return true;
        }

        var temp = FilePath + "." + Environment.ProcessId + ".tmp";
        try
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var file = new CacheFile
            {
                Version = FormatVersion,
                Repository = _repository,
                Commits = _commits.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.Select(f => new CachedFile { Path = f.Path, Added = f.LinesAdded, Deleted = f.LinesDeleted }).ToList()),
            };

            using (var stream = File.Create(temp))
            {
                JsonSerializer.Serialize(stream, file, Json);
            }

            // Write then move, so a crash never leaves a half-written cache behind.
            File.Move(temp, FilePath, overwrite: true);
            _dirty = false;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // Nothing more to do.
            }

            return false;
        }
    }

    private sealed class CacheFile
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("repository")]
        public string? Repository { get; set; }

        /// <summary>SHA → files changed by that commit.</summary>
        [JsonPropertyName("commits")]
        public Dictionary<string, List<CachedFile>>? Commits { get; set; }
    }

    private sealed class CachedFile
    {
        [JsonPropertyName("p")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("a")]
        public int Added { get; set; }

        [JsonPropertyName("d")]
        public int Deleted { get; set; }
    }
}
