using GitPulse.Analysis;
using GitPulse.Caching;
using GitPulse.Git;
using GitPulse.Models;
using GitPulse.Rendering;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace GitPulse.Commands;

/// <summary>The result of a scan, with what the cache contributed (null when the cache was off).</summary>
internal sealed record ScanOutcome(RepoSnapshot Snapshot, ScanCache? Cache);

/// <summary>
/// Opens a repository (Status spinner), loads its cache (Status), scans it (multi-task Progress),
/// saves the cache (Status), then runs the analysis. Shared by every command.
/// </summary>
internal static class ScanDisplay
{
    public static RepoStats Run(IAnsiConsole console, RepoSettings settings, CancellationToken cancellationToken) =>
        RepoAnalyzer.Analyze(Scan(console, settings.Path, settings.ToFilter(), !settings.NoCache, cancellationToken).Snapshot);

    /// <summary>Opens the repository with a spinner (also used by <c>commit</c>, which does not scan).</summary>
    public static RepositoryReader Open(IAnsiConsole console, string path) =>
        Spin(console, "Opening repository…", () => RepositoryReader.Open(path));

    /// <summary>
    /// Scans without analyzing. The interactive mode keeps the snapshot so it can re-apply
    /// author filters without reading the repository again.
    /// </summary>
    public static ScanOutcome Scan(IAnsiConsole console, string path, ScanFilter filter, bool useCache, CancellationToken cancellationToken)
    {
        using var reader = Open(console, path);
        var cache = useCache ? Spin(console, "Loading cache…", () => ScanCache.Load(reader.GitDirectory)) : null;
        var snapshot = ScanWithProgress(console, reader, filter, cache, cancellationToken);

        if (cache is not null)
        {
            Spin(console, "Saving cache…", () => SaveCache(cache, snapshot, filter));
        }

        return new ScanOutcome(snapshot, cache);
    }

    /// <summary>
    /// The same scan with no progress display, for places where one can't be shown
    /// (inside a <c>Live</c> display, such as <c>watch</c>).
    /// </summary>
    public static RepoSnapshot ScanQuietly(string path, ScanFilter filter, bool useCache, CancellationToken cancellationToken)
    {
        using var reader = RepositoryReader.Open(path);
        var cache = useCache ? ScanCache.Load(reader.GitDirectory) : null;
        var snapshot = RepositoryScanner.Scan(reader, filter, progress: null, cancellationToken, cache);
        if (cache is not null)
        {
            SaveCache(cache, snapshot, filter);
        }

        return snapshot;
    }

    private static bool SaveCache(ScanCache cache, RepoSnapshot snapshot, ScanFilter filter)
    {
        // Only a full walk from HEAD knows which commits are gone; a filtered scan sees a subset.
        if (filter.WithoutAuthors().IsEmpty)
        {
            cache.Prune(snapshot.Commits.Select(c => c.Sha));
        }

        return cache.Save();
    }

    private static T Spin<T>(IAnsiConsole console, string status, Func<T> work) =>
        console.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(new Style(Theme.Primary))
            .Start(status, _ => work());

    private static RepoSnapshot ScanWithProgress(
        IAnsiConsole console, RepositoryReader reader, ScanFilter filter, ScanCache? cache, CancellationToken cancellationToken)
    {
        RepoSnapshot? snapshot = null;

        console.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new SpinnerColumn(Spinner.Known.Dots) { Style = new Style(Theme.Primary) },
                new TaskDescriptionColumn { Alignment = Justify.Left },
                new ProgressBarColumn
                {
                    CompletedStyle = new Style(Theme.Primary),
                    FinishedStyle = new Style(Theme.Added),
                },
                new PercentageColumn(),
                new CountColumn(),
                new ElapsedTimeColumn { Style = new Style(Theme.Muted) })
            .Start(ctx =>
            {
                var progress = new SpectreScanProgress(ctx);
                snapshot = RepositoryScanner.Scan(reader, filter, progress, cancellationToken, cache);
            });

        return snapshot!;
    }

    /// <summary>Bridges the Git layer's <see cref="IScanProgress"/> to Spectre progress tasks.</summary>
    private sealed class SpectreScanProgress : IScanProgress
    {
        private readonly Dictionary<ScanStage, ProgressTask> _tasks;

        public SpectreScanProgress(ProgressContext ctx)
        {
            // Create every stage up front so the user sees what is coming.
            _tasks = new Dictionary<ScanStage, ProgressTask>
            {
                [ScanStage.ReadingCommits] = ctx.AddTask("Reading commits", autoStart: false),
                [ScanStage.ComputingDiffStats] = ctx.AddTask("Computing diff stats", autoStart: false),
                [ScanStage.DetectingLanguages] = ctx.AddTask("Detecting languages", autoStart: false),
            };
        }

        public void Started(ScanStage stage, int? total)
        {
            var task = _tasks[stage];
            if (total is { } t)
            {
                task.MaxValue = Math.Max(1, t);
            }
            else
            {
                task.IsIndeterminate = true;
            }

            task.StartTask();
        }

        public void Advanced(ScanStage stage, int amount = 1) => _tasks[stage].Increment(amount);

        public void Completed(ScanStage stage)
        {
            var task = _tasks[stage];
            if (task.IsIndeterminate)
            {
                task.IsIndeterminate = false;
                task.MaxValue = Math.Max(1, task.Value);
            }

            task.Value = task.MaxValue;
            task.StopTask();
        }
    }

    /// <summary>Custom progress column: "1,234/5,678" items processed.</summary>
    private sealed class CountColumn : ProgressColumn
    {
        public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
        {
            var text = task.IsIndeterminate
                ? Format.Number((long)task.Value)
                : $"{Format.Number((long)task.Value)}/{Format.Number((long)task.MaxValue)}";
            return new Markup(Theme.Dim(text));
        }
    }
}
