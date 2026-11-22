namespace GitPulse;

/// <summary>
/// An expected, user-facing error (bad path, empty repository, ...).
/// Shown as a friendly error panel with a non-zero exit code instead of a stack trace.
/// </summary>
internal class GitPulseException(string title, string message, string? hint = null) : Exception(message)
{
    public string Title { get; } = title;

    public string? Hint { get; } = hint;
}

internal sealed class NotAGitRepositoryException(string path)
    : GitPulseException(
        "Not a Git repository",
        $"'{path}' is not inside a Git repository.",
        "Run gitpulse inside a repository, or point at one with --path <dir>.")
{
    public string SearchedPath { get; } = path;
}

internal sealed class EmptyRepositoryException(string path)
    : GitPulseException(
        "Empty repository",
        $"The repository at '{path}' has no commits yet.",
        "Make a first commit, then run gitpulse again.");

internal sealed class NoCommitsInRangeException(string filterDescription)
    : GitPulseException(
        "No commits in range",
        $"No commits match: {filterDescription}.",
        "Widen the date range, or remove --since/--until/--branch/--author.");

internal sealed class NoCommitsByAuthorException(string filterDescription)
    : GitPulseException(
        "No matching commits",
        $"No commits match: {filterDescription}.",
        "Check the --author spelling; it matches part of a name or email, ignoring case.");

internal sealed class BranchNotFoundException(string branch, IReadOnlyList<string> available)
    : GitPulseException(
        "Branch not found",
        $"There is no local branch named '{branch}'.",
        available.Count == 0 ? null : "Local branches: " + string.Join(", ", available.Take(10)) +
            (available.Count > 10 ? $" and {available.Count - 10} more" : string.Empty) + ".");

internal sealed class CommitNotFoundException(string revision, bool ambiguous = false)
    : GitPulseException(
        ambiguous ? "Ambiguous commit" : "Commit not found",
        ambiguous
            ? $"'{revision}' matches more than one commit."
            : $"No commit matches '{revision}'.",
        ambiguous
            ? "Use more characters of the SHA."
            : "Use a SHA (or the start of one), HEAD~N, a branch or a tag.");

/// <summary>A command needs something it cannot ask for, because the console is not interactive.</summary>
internal sealed class NeedsInputException(string title, string message, string hint)
    : GitPulseException(title, message, hint);
