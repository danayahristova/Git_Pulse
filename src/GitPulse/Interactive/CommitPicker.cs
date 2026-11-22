using System.Globalization;
using GitPulse.Models;
using GitPulse.Rendering;
using Spectre.Console;

namespace GitPulse.Interactive;

/// <summary>
/// Pick a commit from recent history: a <see cref="SelectionPrompt{T}"/> grouped by day,
/// with search. Group headers (days) cannot be selected.
/// </summary>
internal static class CommitPicker
{
    public const int DefaultCount = 300;

    /// <returns>The chosen commit's SHA.</returns>
    public static string Pick(IAnsiConsole console, IReadOnlyList<CommitHeader> commits)
    {
        if (commits.Count == 0)
        {
            throw new GitPulseException("No commits", "There are no commits to pick from.", "Check the filters, or pass a commit: gitpulse commit <sha>.");
        }

        var prompt = new SelectionPrompt<Choice>()
            .Title($"{Theme.Title("Pick a commit")} {Theme.Dim($"(latest {Format.Plural(commits.Count, "commit")})")}")
            .PageSize(15)
            .Mode(SelectionMode.Leaf)
            .HighlightStyle(new Style(Theme.Primary, decoration: Decoration.Bold))
            .MoreChoicesText(Theme.Dim("(move up and down to see more)"))
            .EnableSearch()
            .SearchPlaceholderText(Theme.Dim("type to search messages, authors or SHAs"))
            .UseConverter(c => c.Label);

        foreach (var day in commits.GroupBy(c => DateOnly.FromDateTime(c.When.DateTime)))
        {
            var header = new Choice(
                Theme.Colored(
                    $"{day.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} · {CultureInfo.InvariantCulture.DateTimeFormat.GetDayName(day.Key.DayOfWeek)}",
                    Theme.Muted),
                null);

            prompt.AddChoiceGroup(header, day.Select(c => new Choice(Label(c), c.Sha)));
        }

        return console.Prompt(prompt).Sha!;
    }

    /// <summary>Commit messages and names are repo text: always escaped (choices are parsed as markup).</summary>
    private static string Label(CommitHeader c) =>
        $"{Theme.Colored(c.Sha[..7], Theme.Accent)}  {Markup.Escape(Format.Truncate(c.MessageShort, 60))}  {Theme.Dim(c.AuthorName)}";

    private sealed record Choice(string Label, string? Sha);
}
