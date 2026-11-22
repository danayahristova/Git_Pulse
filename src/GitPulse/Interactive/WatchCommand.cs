using System.ComponentModel;
using System.Globalization;
using GitPulse.Analysis;
using GitPulse.Commands;
using GitPulse.Git;
using GitPulse.Models;
using GitPulse.Rendering;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace GitPulse.Interactive;

internal sealed class WatchSettings : RepoSettings
{
    [CommandOption("-i|--interval <SECONDS>")]
    [Description("How often to check the repository for changes (1-60 seconds).")]
    [DefaultValue(2)]
    public int Interval { get; init; } = 2;

    public override ValidationResult Validate()
    {
        var baseResult = base.Validate();
        if (!baseResult.Successful)
        {
            return baseResult;
        }

        return Interval is < 1 or > 60
            ? ValidationResult.Error($"--interval must be between 1 and 60 seconds, but was {Interval}.")
            : ValidationResult.Success();
    }
}

/// <summary>
/// <c>gitpulse watch</c>: the overview in a <see cref="Live"/> display that refreshes when the
/// repository changes. Keys are read by hand (prompts can't run inside Live): q quits, r refreshes.
/// </summary>
internal sealed class WatchCommand(IAnsiConsole console) : Command<WatchSettings>
{
    public override int Execute(CommandContext context, WatchSettings settings, CancellationToken cancellationToken)
    {
        if (!console.Profile.Capabilities.Interactive)
        {
            throw new NeedsInputException(
                "Watch needs a terminal",
                "gitpulse watch updates the screen as the repository changes, so it needs an interactive terminal.",
                "Run it in a terminal, or use gitpulse overview for a one-off report.");
        }

        var filter = settings.ToFilter();
        var useCache = !settings.NoCache;

        // The first scan shows progress (and fills the cache, so refreshes are fast).
        var first = ScanDisplay.Scan(console, settings.Path, filter, useCache, cancellationToken).Snapshot;

        using var signal = new RepoChangeWatcher(settings.Path, TimeSpan.FromSeconds(settings.Interval));
        var session = new WatchSession(
            console,
            first,
            () => ScanDisplay.ScanQuietly(settings.Path, filter, useCache, cancellationToken),
            signal,
            signal.UsesFileEvents ? "file events + polling" : "polling",
            TimeProvider.System);

        session.Run(cancellationToken);
        return 0;
    }
}

/// <summary>The watch loop, separate from the command so tests can drive it with fake keys and signals.</summary>
internal sealed class WatchSession
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);

    private readonly IAnsiConsole _console;
    private readonly Func<RepoSnapshot> _rescan;
    private readonly IChangeSignal _signal;
    private readonly string _mode;
    private readonly TimeProvider _time;

    private RepoStats _stats;
    private DateTimeOffset _updated;
    private string? _problem;

    public WatchSession(
        IAnsiConsole console,
        RepoSnapshot first,
        Func<RepoSnapshot> rescan,
        IChangeSignal signal,
        string mode,
        TimeProvider time)
    {
        _console = console;
        _rescan = rescan;
        _signal = signal;
        _mode = mode;
        _time = time;
        _stats = RepoAnalyzer.Analyze(first);
        _updated = time.GetLocalNow();
    }

    /// <summary>How many times the repository was scanned again (for tests).</summary>
    public int Refreshes { get; private set; }

    public void Run(CancellationToken cancellationToken)
    {
        var width = _console.Profile.Width;

        _console.Clear();
        _console.Live(Screen(width, refreshing: false))
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Bottom)
            .Start(ctx =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var refresh = _signal.ConsumeChange();

                    while (_console.Input.IsKeyAvailable())
                    {
                        var key = _console.Input.ReadKey(intercept: true);
                        switch (key?.Key)
                        {
                            case ConsoleKey.Q or ConsoleKey.Escape:
                                return;
                            case ConsoleKey.R:
                                refresh = true;
                                break;
                        }
                    }

                    if (_console.Profile.Width != width)
                    {
                        width = _console.Profile.Width; // the window was resized
                        ctx.UpdateTarget(Screen(width, refreshing: false));
                    }

                    if (refresh)
                    {
                        ctx.UpdateTarget(Screen(width, refreshing: true));
                        Refresh();
                        ctx.UpdateTarget(Screen(width, refreshing: false));
                    }

                    cancellationToken.WaitHandle.WaitOne(Tick);
                }
            });
    }

    private void Refresh()
    {
        try
        {
            _stats = RepoAnalyzer.Analyze(_rescan());
            _problem = null;
        }
        catch (GitPulseException error)
        {
            // Keep showing the last good state and say why it didn't update.
            _problem = error.Message;
        }

        Refreshes++;
        _updated = _time.GetLocalNow();
    }

    private IRenderable Screen(int width, bool refreshing) =>
        new Rows(OverviewRenderer.Render(_stats, width), new Text(string.Empty), Footer(refreshing));

    private Markup Footer(bool refreshing)
    {
        var state = refreshing
            ? Theme.Colored("● refreshing…", Theme.Warning)
            : Theme.Colored("● watching", Theme.Added);
        var time = _updated.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        var line = $"{state} {Theme.Dim($"· updated {time} · {_mode}")}   " +
                   $"{Theme.Bold("r")} {Theme.Dim("refresh")}  {Theme.Bold("q")} {Theme.Dim("quit")}";

        if (_problem is not null)
        {
            line += "\n" + Theme.Colored("Couldn't update: " + _problem, Theme.Error);
        }

        return new Markup(line);
    }
}
