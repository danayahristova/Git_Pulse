using System.ComponentModel;
using GitPulse.Git;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitPulse.Commands;

/// <summary>
/// Phase 0 spike: prints the most recent commits in a table.
/// Will be replaced by interactive mode in task 1.7.
/// </summary>
internal sealed class RecentCommitsCommand : Command<RecentCommitsCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--path <DIR>")]
        [Description("Repository path (default: current directory).")]
        public string Path { get; init; } = ".";
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = RepositoryReader.Open(settings.Path);

            var table = new Table()
                .AddColumn("SHA")
                .AddColumn("Author")
                .AddColumn("Date")
                .AddColumn(new TableColumn("+").RightAligned())
                .AddColumn(new TableColumn("−").RightAligned())
                .AddColumn("Message");

            foreach (var c in reader.ReadCommits(max: 10))
            {
                table.AddRow(
                    Markup.Escape(c.Sha[..7]),
                    Markup.Escape(c.AuthorName),
                    c.When.ToString("yyyy-MM-dd"),
                    $"[green]{c.LinesAdded}[/]",
                    $"[red]{c.LinesDeleted}[/]",
                    Markup.Escape(c.MessageShort));
            }

            AnsiConsole.Write(table);
            return 0;
        }
        catch (NotAGitRepositoryException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}
