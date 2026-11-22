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
        CancellationToken cancellationToken = default)
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
            throw new NoCommitsInRangeException((filter with { Authors = null }).Describe());
        }

        // 2. Diff stats per commit (the expensive part).
        progress?.Started(ScanStage.ComputingDiffStats, commits.Count);
        var records = new List<CommitRecord>(commits.Count);
        foreach (var commit in commits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            records.Add(reader.ReadStats(commit));
            progress?.Advanced(ScanStage.ComputingDiffStats);
        }

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
}
