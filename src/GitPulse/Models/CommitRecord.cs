namespace GitPulse.Models;

/// <summary>A commit as read from the repository, before any analysis.</summary>
internal sealed record CommitRecord(
    string Sha,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset When,
    string MessageShort,
    bool IsMerge,
    IReadOnlyList<FileChange> Files)
{
    public int LinesAdded => Files.Sum(f => f.LinesAdded);

    public int LinesDeleted => Files.Sum(f => f.LinesDeleted);
}

/// <summary>Line stats for one file touched by a commit.</summary>
internal sealed record FileChange(string Path, int LinesAdded, int LinesDeleted);

/// <summary>A file in the HEAD tree, used for language detection.</summary>
internal sealed record TrackedFile(string Path, long SizeBytes);

/// <summary>Everything read from a repository in one scan. Pure data, no LibGit2Sharp types.</summary>
internal sealed record RepoSnapshot(
    string Name,
    string WorkingDirectory,
    IReadOnlyList<CommitRecord> Commits,
    IReadOnlyList<TrackedFile> Files,
    int BranchCount,
    int TagCount,
    ScanFilter Filter);
