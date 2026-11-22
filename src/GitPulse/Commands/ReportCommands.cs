using System.Diagnostics;
using GitPulse.Models;
using GitPulse.Rendering;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace GitPulse.Commands;

/// <summary>
/// Base for the one-shot report commands: scan with progress, then write one renderable.
/// Commands stay thin; all drawing happens in Rendering/.
/// </summary>
internal abstract class ReportCommand(IAnsiConsole console) : Command<RepoSettings>
{
    protected IAnsiConsole Console { get; } = console;

    public sealed override int Execute(CommandContext context, RepoSettings settings, CancellationToken cancellationToken)
    {
        var stats = ScanDisplay.Run(Console, settings, cancellationToken);
        Console.WriteLine();
        Console.Write(Render(stats, settings, Console.Profile.Width));
        Console.WriteLine(); // Layout output does not end with a line break
        return 0;
    }

    protected abstract IRenderable Render(RepoStats stats, RepoSettings settings, int width);
}

internal sealed class OverviewCommand(IAnsiConsole console) : ReportCommand(console)
{
    protected override IRenderable Render(RepoStats stats, RepoSettings settings, int width) =>
        OverviewRenderer.Render(stats, width);
}

internal sealed class AuthorsCommand(IAnsiConsole console) : ReportCommand(console)
{
    protected override IRenderable Render(RepoStats stats, RepoSettings settings, int width) =>
        AuthorsRenderer.Render(stats, settings.Top, width);
}

internal sealed class HotspotsCommand(IAnsiConsole console) : ReportCommand(console)
{
    protected override IRenderable Render(RepoStats stats, RepoSettings settings, int width) =>
        HotspotsRenderer.Render(stats, settings.Top, width);
}

internal sealed class HeatmapCommand(IAnsiConsole console) : ReportCommand(console)
{
    protected override IRenderable Render(RepoStats stats, RepoSettings settings, int width) =>
        ActivityRenderer.Render(stats, width);
}

/// <summary>Scans (filling the cache, so later commands are fast) and prints a short result.</summary>
internal sealed class ScanCommand(IAnsiConsole console) : Command<RepoSettings>
{
    public override int Execute(CommandContext context, RepoSettings settings, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var outcome = ScanDisplay.Scan(console, settings.Path, settings.ToFilter(), !settings.NoCache, cancellationToken);
        var stats = Analysis.RepoAnalyzer.Analyze(outcome.Snapshot);
        timer.Stop();

        var s = stats.Summary;
        console.WriteLine();
        console.Write(new Rule(Theme.Title(s.Name)).LeftJustified().RuleStyle(Theme.Border));
        console.MarkupLine(
            $"Scanned {Theme.Bold(Format.Plural(s.Commits, "commit"))} by " +
            $"{Theme.Bold(Format.Plural(s.Authors, "author"))} and " +
            $"{Theme.Bold(Format.Plural(s.Files, "file"))} in " +
            Theme.Bold(timer.Elapsed.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s") + ".");
        console.MarkupLine(Theme.Dim(CacheLine(outcome.Cache)));
        console.MarkupLine(Theme.Dim("Next: gitpulse overview, gitpulse authors or gitpulse hotspots."));
        return 0;
    }

    internal static string CacheLine(Caching.ScanCache? cache) => cache switch
    {
        null => "Cache: off (--no-cache).",
        { LoadFailed: true } => $"Cache: the old cache could not be read and was rebuilt ({Format.Plural(cache.Misses, "commit")} diffed).",
        _ => $"Cache: {Format.Number(cache.Hits)} from cache, {Format.Plural(cache.Misses, "commit")} diffed.",
    };
}
