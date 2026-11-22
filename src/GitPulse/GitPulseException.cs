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

internal sealed class NoCommitsInRangeException(DateOnly since)
    : GitPulseException(
        "No commits in range",
        $"No commits were found since {since:yyyy-MM-dd}.",
        "Try an earlier --since date, or leave it out.");
