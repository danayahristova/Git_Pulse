using GitPulse.Commands;
using GitPulse.Rendering;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitPulse;

/// <summary>Command-line configuration, shared by <c>Program</c> and the tests.</summary>
internal static class GitPulseApp
{
    public const int ExitOk = 0;
    public const int ExitExpectedError = 1;
    public const int ExitUsageError = 2;
    public const int ExitUnexpectedError = 70;
    public const int ExitCancelled = 130;

    public static void Configure(IConfigurator config)
    {
        config.SetApplicationName("gitpulse");
        config.SetExceptionHandler(HandleException);

        config.AddCommand<ScanCommand>("scan")
            .WithDescription("Analyze the repository and show scan progress.")
            .WithExample("scan", "--since", "2026-01-01");

        config.AddCommand<OverviewCommand>("overview")
            .WithDescription("Dashboard: summary, languages, top authors and hotspots.")
            .WithExample("overview", "--path", "../other-repo");

        config.AddCommand<AuthorsCommand>("authors")
            .WithDescription("Contributors by number of commits.")
            .WithExample("authors", "--top", "20");

        config.AddCommand<HotspotsCommand>("hotspots")
            .WithDescription("Most-changed files (lines added + deleted) and a folder tree.")
            .WithExample("hotspots", "--since", "2026-06-01", "--top", "15");

        config.AddCommand<CommitCommand>("commit")
            .WithDescription("One commit in detail: message, changed files and diff. Without a revision, pick one.")
            .WithExample("commit", "HEAD~1")
            .WithExample("commit", "1a2b3c4", "--raw")
            .WithExample("commit");

        config.AddCommand<HeatmapCommand>("heatmap")
            .WithDescription("Activity heatmap of the last year, plus commits by weekday and time of day.")
            .WithExample("heatmap", "--path", "../other-repo");
    }

    /// <summary>
    /// Expected errors get a friendly panel; usage errors get Spectre's own message;
    /// anything else is a bug and gets the full (pretty) exception.
    /// </summary>
    internal static int HandleException(Exception exception, ITypeResolver? resolver)
    {
        var console = ResolveConsole(resolver);
        switch (exception)
        {
            case GitPulseException expected:
                console.Write(ErrorRenderer.Render(expected));
                return ExitExpectedError;

            case CommandAppException usage:
                if (usage.Pretty is { } pretty)
                {
                    console.Write(pretty);
                }
                else
                {
                    console.MarkupLine(Theme.Colored("Error: " + usage.Message, Theme.Error));
                }

                return ExitUsageError;

            case OperationCanceledException:
                console.MarkupLine(Theme.Dim("Cancelled."));
                return ExitCancelled;

            default:
                console.WriteException(exception, ExceptionFormats.ShortenPaths);
                return ExitUnexpectedError;
        }
    }

    private static IAnsiConsole ResolveConsole(ITypeResolver? resolver)
    {
        try
        {
            return resolver?.Resolve(typeof(IAnsiConsole)) as IAnsiConsole ?? AnsiConsole.Console;
        }
        catch (Exception)
        {
            return AnsiConsole.Console;
        }
    }
}
