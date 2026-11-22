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

    /// <summary>The <c>.git</c> folder; identifies the repository (e.g. for the scan cache).</summary>
    public string GitDirectory => System.IO.Path.TrimEndingDirectorySeparator(_repo.Info.Path);

    public int BranchCount => _repo.Branches.Count(b => !b.IsRemote);

    public int TagCount => _repo.Tags.Count();

    /// <summary>Local branch names, the current branch first, then alphabetical.</summary>
    public IReadOnlyList<string> BranchNames =>
        _repo.Branches.Where(b => !b.IsRemote)
            .OrderByDescending(b => b.IsCurrentRepositoryHead)
            .ThenBy(b => b.FriendlyName, StringComparer.Ordinal)
            .Select(b => b.FriendlyName)
            .ToList();

    /// <summary>The <c>origin</c> remote's URL (or the first remote's), if any. Read from config only.</summary>
    public string? RemoteUrl =>
        (_repo.Network.Remotes["origin"] ?? _repo.Network.Remotes.FirstOrDefault())?.Url;

    /// <summary>
    /// Commits newest first, without diff stats (cheap), limited by the filter's dates and branches.
    /// Without branches, history starts at HEAD. Authors are filtered later, in the analysis.
    /// <paramref name="onCommit"/> is called for each commit kept, for progress reporting.
    /// </summary>
    /// <exception cref="BranchNotFoundException">A branch in the filter does not exist.</exception>
    internal IReadOnlyList<Commit> ListCommits(ScanFilter? scanFilter = null, Action? onCommit = null)
    {
        var result = new List<Commit>();
        foreach (var commit in EnumerateCommits(scanFilter))
        {
            result.Add(commit);
            onCommit?.Invoke();
        }

        return result;
    }

    /// <summary>
    /// Same as <see cref="ListCommits"/>, but lazy: stopping early (e.g. <c>Take(300)</c>) stops the
    /// history walk, so a large repository is not read to the end. Branches are checked right away.
    /// </summary>
    /// <exception cref="BranchNotFoundException">A branch in the filter does not exist.</exception>
    internal IEnumerable<Commit> EnumerateCommits(ScanFilter? scanFilter = null)
    {
        if (IsEmpty)
        {
            return [];
        }

        scanFilter ??= ScanFilter.None;
        var filter = new CommitFilter { SortBy = CommitSortStrategies.Time };
        if (scanFilter.BranchList.Count > 0)
        {
            // Resolved here, not inside the iterator, so a bad branch fails immediately.
            filter.IncludeReachableFrom = scanFilter.BranchList.Select(ResolveBranch).ToList();
        }

        return Walk(_repo.Commits.QueryBy(filter), scanFilter);

        static IEnumerable<Commit> Walk(IEnumerable<Commit> commits, ScanFilter scanFilter)
        {
            var since = scanFilter.Since is { } s ? StartOfDay(s) : (DateTimeOffset?)null;
            var untilExclusive = scanFilter.Until is { } u ? StartOfDay(u.AddDays(1)) : (DateTimeOffset?)null;

            foreach (var commit in commits)
            {
                // Sorted by committer time, so once that is older than "since" we can stop.
                // Author time decides inclusion (rebased commits keep their author date).
                if (since is { } from && commit.Committer.When < from)
                {
                    yield break;
                }

                var when = commit.Author.When;
                if ((since is { } start && when < start) || (untilExclusive is { } end && when >= end))
                {
                    continue;
                }

                yield return commit;
            }
        }
    }

    /// <summary>The most recent commits from HEAD, for the commit picker. Reads only what it returns.</summary>
    public IReadOnlyList<CommitHeader> RecentCommits(int max) =>
        EnumerateCommits().Take(max)
            .Select(c => new CommitHeader(c.Sha, c.Author.Name, c.Author.When, c.MessageShort))
            .ToList();

    /// <summary>
    /// Full details of one commit, with its diff against the first parent.
    /// <paramref name="revision"/> can be a full or short SHA, <c>HEAD~2</c>, a branch or a tag.
    /// </summary>
    /// <exception cref="CommitNotFoundException">Nothing matches, or the revision is ambiguous.</exception>
    public CommitDetail ReadCommitDetail(string revision, DiffLimits limits)
    {
        var commit = LookupCommit(revision);
        var parents = commit.Parents.ToList();
        // Same options for both, so renames are detected the same way in the patch and the change list.
        var compare = new CompareOptions { ContextLines = 3, Similarity = SimilarityOptions.Renames };

        using var patch = _repo.Diff.Compare<Patch>(parents.FirstOrDefault()?.Tree, commit.Tree, compare);
        using var changes = _repo.Diff.Compare<TreeChanges>(parents.FirstOrDefault()?.Tree, commit.Tree, compare);
        var kinds = changes.ToDictionary(c => c.Path, c => (c.Status, c.OldPath), StringComparer.Ordinal);

        var budget = limits.MaxTotalLines;
        var files = new List<FileDiff>();
        foreach (var entry in patch)
        {
            var (status, oldPath) = kinds.TryGetValue(entry.Path, out var k) ? k : (entry.Status, entry.OldPath);
            var maxLines = Math.Min(limits.MaxLinesPerFile, budget);
            // No diff wanted at all (--no-diff, --raw): don't parse it.
            var (lines, hidden) = entry.IsBinaryComparison || limits.MaxLinesPerFile == 0
                ? ([], 0)
                : DiffParser.Parse(entry.Patch, maxLines);
            budget -= lines.Count;

            files.Add(new FileDiff(
                entry.Path,
                status is ChangeKind.Renamed or ChangeKind.Copied && oldPath != entry.Path ? oldPath : null,
                KindOf(status),
                entry.LinesAdded,
                entry.LinesDeleted,
                entry.IsBinaryComparison,
                lines,
                hidden));
        }

        return new CommitDetail(
            commit.Sha,
            commit.Author.Name,
            commit.Author.Email,
            commit.Author.When,
            commit.Committer.Name,
            commit.Committer.Email,
            commit.Committer.When,
            commit.Message.TrimEnd('\n', '\r'),
            parents.Select(p => p.Sha).ToList(),
            files,
            RemoteLinks.CommitUrl(RemoteUrl, commit.Sha));
    }

    private Commit LookupCommit(string revision)
    {
        if (IsEmpty)
        {
            throw new EmptyRepositoryException(SearchedPath);
        }

        try
        {
            return _repo.Lookup<Commit>(revision) ?? throw new CommitNotFoundException(revision);
        }
        catch (AmbiguousSpecificationException)
        {
            throw new CommitNotFoundException(revision, ambiguous: true);
        }
        catch (LibGit2SharpException)
        {
            throw new CommitNotFoundException(revision);
        }
    }

    private Commit ResolveBranch(string name) =>
        _repo.Branches[name]?.Tip ?? throw new BranchNotFoundException(name, BranchNames);

    private static FileChangeKind KindOf(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => FileChangeKind.Added,
        ChangeKind.Modified => FileChangeKind.Modified,
        ChangeKind.Deleted => FileChangeKind.Deleted,
        ChangeKind.Renamed => FileChangeKind.Renamed,
        ChangeKind.Copied => FileChangeKind.Copied,
        ChangeKind.TypeChanged => FileChangeKind.TypeChanged,
        _ => FileChangeKind.Other,
    };

    /// <summary>Local midnight of <paramref name="date"/>.</summary>
    private static DateTimeOffset StartOfDay(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local));

    /// <summary>
    /// Reads per-file line stats for one commit. Merge commits get no file stats,
    /// like <c>git log --numstat</c>, so merged changes are not counted twice.
    /// </summary>
    internal CommitRecord ReadStats(Commit commit, ICommitStatsCache? cache = null)
    {
        var parents = commit.Parents.ToList();
        var isMerge = parents.Count > 1;

        IReadOnlyList<FileChange> files = [];
        if (!isMerge)
        {
            if (cache is not null && cache.TryGet(commit.Sha, out var cached))
            {
                files = cached;
            }
            else
            {
                using var patch = _repo.Diff.Compare<Patch>(parents.FirstOrDefault()?.Tree, commit.Tree);
                files = patch.Select(f => new FileChange(f.Path, f.LinesAdded, f.LinesDeleted)).ToList();
                cache?.Put(commit.Sha, files);
            }
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
    public IEnumerable<CommitRecord> ReadCommits(ScanFilter? filter = null, int? max = null)
    {
        var commits = EnumerateCommits(filter);
        return (max is { } m ? commits.Take(m) : commits).Select(c => ReadStats(c));
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

/// <summary>How much diff to keep, so a huge commit stays readable.</summary>
internal sealed record DiffLimits(int MaxLinesPerFile = 200, int MaxTotalLines = 2000)
{
    public static DiffLimits Default { get; } = new();

    /// <summary>File list and stats only (<c>--no-diff</c>, <c>--raw</c>).</summary>
    public static DiffLimits None { get; } = new(0, 0);
}
