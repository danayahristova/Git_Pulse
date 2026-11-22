using GitPulse.Analysis;
using GitPulse.Git;
using GitPulse.Models;
using GitPulse.Rendering;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace GitPulse.Commands;

/// <summary>
/// Opens a repository (with a Status spinner) and scans it (with a multi-task Progress display),
/// then runs the analysis. Shared by every command.
/// </summary>
internal static class ScanDisplay
{
    public static RepoStats Run(IAnsiConsole console, RepoSettings settings, CancellationToken cancellationToken) =>
        RepoAnalyzer.Analyze(Scan(console, settings.Path, settings.ToFilter(), cancellationToken));

    /// <summary>Opens the repository with a spinner (also used by <c>commit</c>, which does not scan).</summary>
    public static RepositoryReader Open(IAnsiConsole console, string path) =>
        console.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(new Style(Theme.Primary))
            .Start("Opening repository…", _ => RepositoryReader.Open(path));

    /// <summary>
    /// Scans without analyzing. The interactive mode keeps the snapshot so it can re-apply
    /// author filters without reading the repository again.
    /// </summary>
    public static RepoSnapshot Scan(IAnsiConsole console, string path, ScanFilter filter, CancellationToken cancellationToken)
    {
        using var reader = Open(console, path);
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
                snapshot = RepositoryScanner.Scan(reader, filter, progress, cancellationToken);
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
