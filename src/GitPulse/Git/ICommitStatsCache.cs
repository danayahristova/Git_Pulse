using GitPulse.Models;

namespace GitPulse.Git;

/// <summary>
/// Per-commit file stats by SHA. A commit's content never changes, so its stats can be reused
/// forever; only commits not in the cache need a diff. Implemented in <c>Caching/</c>; the Git
/// layer only sees this interface.
/// </summary>
internal interface ICommitStatsCache
{
    bool TryGet(string sha, out IReadOnlyList<FileChange> files);

    void Put(string sha, IReadOnlyList<FileChange> files);

    /// <summary>Commits whose stats came from the cache during this run.</summary>
    int Hits { get; }

    /// <summary>Commits that had to be diffed during this run.</summary>
    int Misses { get; }
}
