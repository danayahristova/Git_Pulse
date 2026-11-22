namespace GitPulse.Models;

internal enum FileChangeKind
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,
    TypeChanged,
    Other,
}

internal enum DiffLineKind
{
    Context,
    Added,
    Deleted,

    /// <summary>A hunk header such as <c>@@ -1,4 +1,5 @@</c>.</summary>
    Hunk,

    /// <summary>Lines like <c>\ No newline at end of file</c>.</summary>
    Note,
}

internal sealed record DiffLine(DiffLineKind Kind, string Text);

/// <summary>
/// One file changed by a commit. <see cref="Lines"/> may be cut short to keep the output readable;
/// <see cref="HiddenLines"/> says how many diff lines were left out.
/// </summary>
internal sealed record FileDiff(
    string Path,
    string? OldPath,
    FileChangeKind Kind,
    int LinesAdded,
    int LinesDeleted,
    bool IsBinary,
    IReadOnlyList<DiffLine> Lines,
    int HiddenLines);

/// <summary>Everything shown by <c>gitpulse commit</c>.</summary>
internal sealed record CommitDetail(
    string Sha,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset AuthorWhen,
    string CommitterName,
    string CommitterEmail,
    DateTimeOffset CommitterWhen,
    string Message,
    IReadOnlyList<string> Parents,
    IReadOnlyList<FileDiff> Files,
    string? WebUrl)
{
    public string ShortSha => Sha[..Math.Min(7, Sha.Length)];

    public bool IsMerge => Parents.Count > 1;

    public int LinesAdded => Files.Sum(f => f.LinesAdded);

    public int LinesDeleted => Files.Sum(f => f.LinesDeleted);

    public string Subject => Message.Split('\n', 2)[0].Trim();

    /// <summary>The message after the subject line, without leading/trailing blank lines.</summary>
    public string Body => Message.Split('\n', 2) is [_, var rest] ? rest.Trim('\n', '\r', ' ') : string.Empty;
}

/// <summary>A commit in a list (the commit picker): no diff, cheap to read.</summary>
internal sealed record CommitHeader(string Sha, string AuthorName, DateTimeOffset When, string MessageShort);

/// <summary>
/// What part of the history to analyze. Dates, branches and authors combine (AND).
/// <list type="bullet">
/// <item><see cref="Authors"/> (typed with <c>--author</c>): the name or email <em>contains</em> the text, ignoring case.</item>
/// <item><see cref="ExactAuthors"/> (ticked in the menu): the email, or the name when there is no email, <em>equals</em> it, ignoring case.</item>
/// </list>
/// </summary>
internal sealed record ScanFilter(
    DateOnly? Since = null,
    DateOnly? Until = null,
    IReadOnlyList<string>? Branches = null,
    IReadOnlyList<string>? Authors = null,
    IReadOnlyList<string>? ExactAuthors = null)
{
    public static ScanFilter None { get; } = new();

    public IReadOnlyList<string> BranchList => Branches ?? [];

    public IReadOnlyList<string> AuthorList => Authors ?? [];

    public IReadOnlyList<string> ExactAuthorList => ExactAuthors ?? [];

    public bool HasAuthorFilter => AuthorList.Count > 0 || ExactAuthorList.Count > 0;

    public bool IsEmpty => Since is null && Until is null && BranchList.Count == 0 && !HasAuthorFilter;

    /// <summary>The same filter without any author part (what the scan itself applies).</summary>
    public ScanFilter WithoutAuthors() => this with { Authors = null, ExactAuthors = null };

    /// <summary>The identity an exact author filter compares against.</summary>
    public static string AuthorKey(string name, string email) =>
        string.IsNullOrWhiteSpace(email) ? name.Trim() : email.Trim();

    /// <summary>Short description for headers, e.g. "since 2026-01-01 · branch main".</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Since is { } s)
        {
            parts.Add($"since {s:yyyy-MM-dd}");
        }

        if (Until is { } u)
        {
            parts.Add($"until {u:yyyy-MM-dd}");
        }

        if (BranchList.Count > 0)
        {
            parts.Add((BranchList.Count == 1 ? "branch " : "branches ") + string.Join(", ", BranchList));
        }

        var authors = AuthorList.Concat(ExactAuthorList).ToList();
        if (authors.Count > 0)
        {
            parts.Add((authors.Count == 1 ? "author " : "authors ") + string.Join(", ", authors));
        }

        return parts.Count == 0 ? "all history" : string.Join(" · ", parts);
    }

    public bool MatchesAuthor(string name, string email)
    {
        var typed = AuthorList.Count == 0 || AuthorList.Any(a =>
            name.Contains(a, StringComparison.OrdinalIgnoreCase) ||
            email.Contains(a, StringComparison.OrdinalIgnoreCase));

        var key = AuthorKey(name, email);
        var ticked = ExactAuthorList.Count == 0 ||
                     ExactAuthorList.Any(a => string.Equals(a, key, StringComparison.OrdinalIgnoreCase));

        return typed && ticked;
    }
}
