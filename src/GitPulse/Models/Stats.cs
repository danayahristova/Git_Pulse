namespace GitPulse.Models;

/// <summary>Contribution totals for one author.</summary>
internal sealed record AuthorStats(
    string Name,
    string Email,
    int Commits,
    int LinesAdded,
    int LinesDeleted,
    DateTimeOffset FirstCommit,
    DateTimeOffset LastCommit);

/// <summary>How often a file changed. Churn = lines added + lines deleted.</summary>
internal sealed record FileChurn(string Path, int Commits, int LinesAdded, int LinesDeleted)
{
    public int Churn => LinesAdded + LinesDeleted;
}

/// <summary>Share of the HEAD tree (by bytes) for one language.</summary>
internal sealed record LanguageShare(string Language, long Bytes, double Percent);

/// <summary>Headline numbers for the overview.</summary>
internal sealed record RepoSummary(
    string Name,
    int Commits,
    int Authors,
    DateTimeOffset FirstCommit,
    DateTimeOffset LastCommit,
    int Branches,
    int Tags,
    int Files,
    DateOnly? Since);

/// <summary>All derived statistics for one scan. This is what renderers consume.</summary>
internal sealed record RepoStats(
    RepoSummary Summary,
    IReadOnlyList<AuthorStats> Authors,
    IReadOnlyList<FileChurn> Hotspots,
    IReadOnlyList<LanguageShare> Languages,
    ActivityStats Activity,
    Heatmap Heatmap,
    ChurnNode FileTree);
