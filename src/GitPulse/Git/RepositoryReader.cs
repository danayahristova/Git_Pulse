using LibGit2Sharp;

namespace GitPulse.Git;

/// <summary>
/// Read-only access to a local Git repository. No Spectre.Console references here:
/// this layer only produces raw data.
/// </summary>
internal sealed class RepositoryReader : IDisposable
{
    private readonly Repository _repo;

    private RepositoryReader(Repository repo) => _repo = repo;

    /// <summary>
    /// Opens the repository that contains <paramref name="path"/> (searching parent folders).
    /// </summary>
    /// <exception cref="NotAGitRepositoryException">No repository was found.</exception>
    public static RepositoryReader Open(string path)
    {
        var gitDir = Repository.Discover(Path.GetFullPath(path))
            ?? throw new NotAGitRepositoryException(path);
        return new RepositoryReader(new Repository(gitDir));
    }

    public bool IsEmpty => _repo.Info.IsHeadUnborn;

    /// <summary>
    /// Commits reachable from HEAD, newest first, with per-file line stats.
    /// Merge commits are compared against their first parent.
    /// </summary>
    public IEnumerable<RawCommit> ReadCommits(DateTimeOffset? since = null, int? max = null)
    {
        if (IsEmpty)
        {
            yield break;
        }

        var filter = new CommitFilter { SortBy = CommitSortStrategies.Time };
        var count = 0;

        foreach (var commit in _repo.Commits.QueryBy(filter))
        {
            if (since is { } s && commit.Author.When < s)
            {
                yield break;
            }

            if (max is { } m && count++ >= m)
            {
                yield break;
            }

            yield return ToRaw(commit);
        }
    }

    private RawCommit ToRaw(Commit commit)
    {
        var parentTree = commit.Parents.FirstOrDefault()?.Tree;
        using var patch = _repo.Diff.Compare<Patch>(parentTree, commit.Tree);

        var files = patch
            .Select(f => new RawFileChange(f.Path, f.LinesAdded, f.LinesDeleted))
            .ToList();

        return new RawCommit(
            commit.Sha,
            commit.Author.Name,
            commit.Author.Email,
            commit.Author.When,
            commit.MessageShort,
            patch.LinesAdded,
            patch.LinesDeleted,
            files);
    }

    public void Dispose() => _repo.Dispose();
}

internal sealed class NotAGitRepositoryException(string path)
    : Exception($"'{path}' is not inside a Git repository.")
{
    public string SearchedPath { get; } = path;
}
