using GitPulse.Commands;
using GitPulse.Models;
using GitPulse.Rendering;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace GitPulse.Interactive;

/// <summary>
/// <c>gitpulse</c> with no command: banner, one scan, then a menu loop until Exit.
/// When the console is not interactive (redirected output, CI) it prints the overview instead.
/// </summary>
internal sealed class InteractiveCommand(IAnsiConsole console) : Command<RepoSettings>
{
    private enum MenuItem
    {
        Overview,
        Authors,
        Hotspots,
        RowCount,
        Exit,
    }

    public override int Execute(CommandContext context, RepoSettings settings, CancellationToken cancellationToken)
    {
        var interactive = console.Profile.Capabilities.Interactive;
        if (interactive)
        {
            console.Write(Banner.Render(console.Profile.Width));
        }

        var stats = ScanDisplay.Run(console, settings, cancellationToken);

        if (!interactive)
        {
            console.WriteLine();
            console.Write(OverviewRenderer.Render(stats, console.Profile.Width));
            console.WriteLine();
            return 0;
        }

        var top = settings.Top;
        var view = MenuItem.Overview;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (view is MenuItem.Overview or MenuItem.Authors or MenuItem.Hotspots)
            {
                console.Clear();
                console.Write(Render(view, stats, top, console.Profile.Width));
                console.WriteLine();
            }

            var choice = console.Prompt(Menu(top));
            switch (choice)
            {
                case MenuItem.Exit:
                    return 0;
                case MenuItem.RowCount:
                    top = AskRowCount(top);
                    break;
                default:
                    view = choice;
                    break;
            }
        }

        return 0;
    }

    private static IRenderable Render(MenuItem view, RepoStats stats, int top, int width) => view switch
    {
        MenuItem.Authors => AuthorsRenderer.Render(stats, top, width),
        MenuItem.Hotspots => HotspotsRenderer.Render(stats, top, width),
        _ => OverviewRenderer.Render(stats, width),
    };

    private static SelectionPrompt<MenuItem> Menu(int top)
    {
        var prompt = new SelectionPrompt<MenuItem>()
            .Title(Theme.Title("What next?"))
            .HighlightStyle(new Style(Theme.Primary, decoration: Decoration.Bold))
            .MoreChoicesText(Theme.Dim("(move up and down to see more)"))
            .EnableSearch()
            .SearchPlaceholderText(Theme.Dim("type to search"))
            .UseConverter(item => item switch
            {
                MenuItem.Overview => "Overview dashboard",
                MenuItem.Authors => "Top authors",
                MenuItem.Hotspots => "Most-changed files",
                MenuItem.RowCount => $"Rows to show ({top})",
                MenuItem.Exit => "Exit",
                _ => item.ToString(),
            });

        prompt.AddChoices(MenuItem.Overview, MenuItem.Authors, MenuItem.Hotspots, MenuItem.RowCount, MenuItem.Exit);
        return prompt;
    }

    private int AskRowCount(int current) =>
        console.Prompt(
            new TextPrompt<int>($"How many rows should tables show? {Theme.Dim($"({RepoSettings.MinTop}-{RepoSettings.MaxTop})")}")
                .DefaultValue(current)
                .ValidationErrorMessage(Theme.Colored("Please enter a whole number.", Theme.Error))
                .Validate(n => n is >= RepoSettings.MinTop and <= RepoSettings.MaxTop
                    ? ValidationResult.Success()
                    : ValidationResult.Error(Theme.Colored(
                        $"Choose a number from {RepoSettings.MinTop} to {RepoSettings.MaxTop}.", Theme.Error))));
}
