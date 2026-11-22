using GitPulse.Models;
using LibGit2Sharp;

namespace GitPulse.Git;

/// <summary>
/// Read-only access to a local Git repository. No Spectre.Console references here:
/// this layer only produces raw data. LibGit2Sharp types never leave the Git namespace.
/// </summary>
internal sealed class RepositoryReader : IDisposable
{
    private readonly Repository _repo;

    private RepositoryReader(Repository repo, string searchedPath)
    {
        _repo = repo;
        SearchedPath = searchedPath;
    }

    /// <summary>
    /// Opens the repository that contains <paramref name="path"/> (searching parent folders).
    /// </summary>
    /// <exception cref="NotAGitRepositoryException">No repository was found.</exception>
    public static RepositoryReader Open(string path)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        var gitDir = Directory.Exists(fullPath) ? Repository.Discover(fullPath) : null;
        if (gitDir is null)
        {
            throw new NotAGitRepositoryException(path);
        }

        return new RepositoryReader(new Repository(gitDir), path);
    }

    public string SearchedPath { get; }

    public bool IsEmpty => _repo.Info.IsHeadUnborn;

    /// <summary>Working directory, or the .git folder for bare repositories.</summary>
    public string WorkingDirectory =>
        System.IO.Path.TrimEndingDirectorySeparator(_repo.Info.WorkingDirectory ?? _repo.Info.Path);

    public string Name => System.IO.Path.GetFileName(WorkingDirectory);

    public int BranchCount => _repo.Branches.Count(b => !b.IsRemote);

    public int TagCount => _repo.Tags.Count();

    /// <summary>
    /// Commits reachable from HEAD, newest first, without diff stats (cheap).
    /// <paramref name="onCommit"/> is called for each commit found, for progress reporting.
    /// </summary>
    internal IReadOnlyList<Commit> ListCommits(DateTimeOffset? since = null, Action? onCommit = null)
    {
        var result = new List<Commit>();
        if (IsEmpty)
        {
            return result;
        }

        var filter = new CommitFilter { SortBy = CommitSortStrategies.Time };
        foreach (var commit in _repo.Commits.QueryBy(filter))
        {
            if (since is { } s)
            {
                // Sorted by committer time, so once that is older we can stop.
                // Author time decides inclusion (rebased commits keep their author date).
                if (commit.Committer.When < s)
                {
                    break;
                }

                if (commit.Author.When < s)
                {
                    continue;
                }
            }

            result.Add(commit);
            onCommit?.Invoke();
        }

        return result;
    }

    /// <summary>
    /// Reads per-file line stats for one commit. Merge commits get no file stats,
    /// like <c>git log --numstat</c>, so merged changes are not counted twice.
    /// </summary>
    internal CommitRecord ReadStats(Commit commit)
    {
        var parents = commit.Parents.ToList();
        var isMerge = parents.Count > 1;

        IReadOnlyList<FileChange> files = [];
        if (!isMerge)
        {
            using var patch = _repo.Diff.Compare<Patch>(parents.FirstOrDefault()?.Tree, commit.Tree);
            files = patch.Select(f => new FileChange(f.Path, f.LinesAdded, f.LinesDeleted)).ToList();
        }

        return new CommitRecord(
            commit.Sha,
            commit.Author.Name,
            commit.Author.Email,
            commit.Author.When,
            commit.MessageShort,
            isMerge,
            files);
    }

    /// <summary>Convenience: commits with stats, newest first.</summary>
    public IEnumerable<CommitRecord> ReadCommits(DateTimeOffset? since = null, int? max = null)
    {
        var commits = ListCommits(since);
        return (max is { } m ? commits.Take(m) : commits).Select(ReadStats);
    }

    /// <summary>Paths of all files (blobs) in the HEAD tree. Submodules are skipped.</summary>
    internal IReadOnlyList<TreeEntry> ListHeadBlobs()
    {
        var result = new List<TreeEntry>();
        if (!IsEmpty)
        {
            Collect(_repo.Head.Tip.Tree, result);
        }

        return result;

        static void Collect(Tree tree, List<TreeEntry> into)
        {
            foreach (var entry in tree)
            {
                switch (entry.TargetType)
                {
                    case TreeEntryTargetType.Blob:
                        into.Add(entry);
                        break;
                    case TreeEntryTargetType.Tree:
                        Collect((Tree)entry.Target, into);
                        break;
                }
            }
        }
    }

    internal static TrackedFile ToTrackedFile(TreeEntry entry) =>
        new(entry.Path.Replace('\\', '/'), ((Blob)entry.Target).Size);

    public void Dispose() => _repo.Dispose();
}
