using GitPulse.Models;

namespace GitPulse.Git;

/// <summary>The stages of a scan, in order.</summary>
internal enum ScanStage
{
    ReadingCommits,
    ComputingDiffStats,
    DetectingLanguages,
}

/// <summary>
/// Progress callbacks for a scan. Implemented by the Spectre progress display in the
/// command layer; the Git layer itself knows nothing about rendering.
/// </summary>
internal interface IScanProgress
{
    /// <summary>A stage started. <paramref name="total"/> is null when the size is not known yet.</summary>
    void Started(ScanStage stage, int? total);

    void Advanced(ScanStage stage, int amount = 1);

    void Completed(ScanStage stage);
}

/// <summary>Reads a whole repository into a <see cref="RepoSnapshot"/>.</summary>
internal static class RepositoryScanner
{
    /// <param name="filter">
    /// Dates and branches limit what is read. Authors are kept in the snapshot and applied by the
    /// analysis, so they can change without scanning again.
    /// </param>
    /// <exception cref="EmptyRepositoryException">The repository has no commits.</exception>
    /// <exception cref="NoCommitsInRangeException">The dates/branches filter out every commit.</exception>
    /// <exception cref="BranchNotFoundException">A branch in the filter does not exist.</exception>
    public static RepoSnapshot Scan(
        RepositoryReader reader,
        ScanFilter? filter = null,
        IScanProgress? progress = null,
        CancellationToken cancellationToken = default,
        ICommitStatsCache? cache = null,
        int maxDegreeOfParallelism = 0)
    {
        filter ??= ScanFilter.None;
        if (reader.IsEmpty)
        {
            throw new EmptyRepositoryException(reader.SearchedPath);
        }

        // 1. Walk history (cheap) so the diff stage knows its total.
        progress?.Started(ScanStage.ReadingCommits, null);
        var commits = reader.ListCommits(filter, () => progress?.Advanced(ScanStage.ReadingCommits));
        progress?.Completed(ScanStage.ReadingCommits);

        if (commits.Count == 0)
        {
            throw new NoCommitsInRangeException(filter.WithoutAuthors().Describe());
        }

        // 2. Diff stats per commit (the expensive part). Merges and cached commits are free;
        //    the rest are diffed, in parallel when there are many.
        progress?.Started(ScanStage.ComputingDiffStats, commits.Count);
        var stats = new IReadOnlyList<FileChange>?[commits.Count];
        var toDiff = new List<int>();
        for (var i = 0; i < commits.Count; i++)
        {
            if (RepositoryReader.IsMerge(commits[i]))
            {
                stats[i] = []; // like git log --numstat: merges add no line counts
            }
            else if (cache is not null && cache.TryGet(commits[i].Sha, out var cached))
            {
                stats[i] = cached;
            }
            else
            {
                toDiff.Add(i);
                continue;
            }

            progress?.Advanced(ScanStage.ComputingDiffStats);
        }

        Diff(reader, commits, toDiff, stats, progress, WorkersFor(toDiff.Count, maxDegreeOfParallelism), cancellationToken);

        foreach (var i in toDiff)
        {
            cache?.Put(commits[i].Sha, stats[i]!);
        }

        var records = commits.Select((c, i) => RepositoryReader.ToRecord(c, stats[i]!)).ToList();
        progress?.Completed(ScanStage.ComputingDiffStats);

        // 3. Files in HEAD, with sizes, for language share.
        var blobs = reader.ListHeadBlobs();
        progress?.Started(ScanStage.DetectingLanguages, blobs.Count);
        var files = new List<TrackedFile>(blobs.Count);
        foreach (var blob in blobs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            files.Add(RepositoryReader.ToTrackedFile(blob));
            progress?.Advanced(ScanStage.DetectingLanguages);
        }

        progress?.Completed(ScanStage.DetectingLanguages);

        return new RepoSnapshot(
            reader.Name,
            reader.WorkingDirectory,
            records,
            files,
            reader.BranchCount,
            reader.TagCount,
            filter);
    }

    /// <summary>Below this many commits to diff, starting workers costs more than it saves.</summary>
    internal const int ParallelThreshold = 200;

    /// <summary>
    /// 0 = automatic: one worker for small scans, otherwise up to 8 (diffing is I/O and
    /// decompression bound; more workers stop helping). A positive value is used as given (tests).
    /// </summary>
    internal static int WorkersFor(int commitsToDiff, int requested) =>
        requested > 0
            ? Math.Max(1, Math.Min(requested, commitsToDiff))
            : commitsToDiff < ParallelThreshold ? 1 : Math.Clamp(Environment.ProcessorCount, 1, 8);

    private static void Diff(
        RepositoryReader reader,
        IReadOnlyList<LibGit2Sharp.Commit> commits,
        List<int> toDiff,
        IReadOnlyList<FileChange>?[] stats,
        IScanProgress? progress,
        int workers,
        CancellationToken cancellationToken)
    {
        if (workers <= 1)
        {
            foreach (var i in toDiff)
            {
                cancellationToken.ThrowIfCancellationRequested();
                stats[i] = reader.ComputeFileStats(commits[i]);
                progress?.Advanced(ScanStage.ComputingDiffStats);
            }

            return;
        }

        // LibGit2Sharp's Repository is not thread-safe: every worker opens its own and diffs every
        // n-th commit by SHA. Results land in distinct array slots, so no locking is needed there.
        var shas = toDiff.Select(i => commits[i].Sha).ToArray();
        var gitDirectory = reader.GitDirectory;
        var options = new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = cancellationToken };

        Parallel.For(0, workers, options, worker =>
        {
            using var own = RepositoryReader.Open(gitDirectory);
            for (var k = worker; k < shas.Length; k += workers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                stats[toDiff[k]] = own.ComputeFileStats(shas[k]);
                progress?.Advanced(ScanStage.ComputingDiffStats);
            }
        });
    }
}
