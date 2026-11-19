namespace GitPulse.Git;

/// <summary>A commit as read from the repository, before any analysis.</summary>
internal sealed record RawCommit(
    string Sha,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset When,
    string MessageShort,
    int LinesAdded,
    int LinesDeleted,
    IReadOnlyList<RawFileChange> Files);

/// <summary>Line stats for one file touched by a commit.</summary>
internal sealed record RawFileChange(string Path, int LinesAdded, int LinesDeleted);
