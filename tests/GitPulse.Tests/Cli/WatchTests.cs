using GitPulse.Git;
using GitPulse.Interactive;
using GitPulse.Models;
using GitPulse.Tests.Git;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Cli;

public sealed class WatchSessionTests
{
    private readonly TestConsole _console = new TestConsole().Width(120).Interactive();

    private static RepoSnapshot Snapshot(params CommitRecord[] commits) =>
        new("demo", "/tmp/demo", commits, [new TrackedFile("a.cs", 10)], 1, 0, ScanFilter.None);

    private static readonly RepoSnapshot One = Snapshot(Commit("Ada", Day(1, 1), ("a.cs", 1, 0)));
    private static readonly RepoSnapshot Two = Snapshot(Commit("Ada", Day(1, 1), ("a.cs", 1, 0)), Commit("Linus", Day(1, 2), ("a.cs", 2, 0)));

    private sealed class FakeSignal(params bool[] changes) : IChangeSignal
    {
        private readonly Queue<bool> _changes = new(changes);

        public bool ConsumeChange() => _changes.Count > 0 && _changes.Dequeue();

        public void Dispose()
        {
        }
    }

    private WatchSession Session(Func<RepoSnapshot> rescan, IChangeSignal? signal = null) =>
        new(_console, One, rescan, signal ?? new FakeSignal(), "polling", TimeProvider.System);

    /// <summary>A rescan that quits right after it ran, so the loop ends.</summary>
    private Func<RepoSnapshot> RescanThenQuit(RepoSnapshot result) => () =>
    {
        _console.Input.PushKey(ConsoleKey.Q);
        return result;
    };

    [Fact]
    public void Q_quits_and_shows_the_overview_with_a_footer()
    {
        _console.Input.PushKey(ConsoleKey.Q);
        var session = Session(() => throw new InvalidOperationException("no rescan expected"));

        session.Run(TestContext.Current.CancellationToken);

        Assert.Equal(0, session.Refreshes);
        Assert.Contains("1 commit · 1 author", _console.Output);
        Assert.Contains("● watching", _console.Output);
        Assert.Contains("refresh", _console.Output);
    }

    [Fact]
    public void R_refreshes_now()
    {
        _console.Input.PushKey(ConsoleKey.R);
        var session = Session(RescanThenQuit(Two));

        session.Run(TestContext.Current.CancellationToken);

        Assert.Equal(1, session.Refreshes);
        Assert.Contains("2 commits · 2 authors", _console.Output);
    }

    [Fact]
    public void A_repository_change_refreshes_without_a_key()
    {
        var session = Session(RescanThenQuit(Two), new FakeSignal(true));

        session.Run(TestContext.Current.CancellationToken);

        Assert.Equal(1, session.Refreshes);
        Assert.Contains("2 commits · 2 authors", _console.Output);
    }

    [Fact]
    public void A_failed_refresh_keeps_the_last_good_state_and_says_why()
    {
        _console.Input.PushKey(ConsoleKey.R);
        var session = Session(() =>
        {
            _console.Input.PushKey(ConsoleKey.Q);
            throw new BranchNotFoundException("gone", ["main"]);
        });

        session.Run(TestContext.Current.CancellationToken);

        Assert.Contains("Couldn't update: There is no local branch named 'gone'.", _console.Output);
        Assert.Contains("1 commit · 1 author", _console.Output);
    }

    [Fact]
    public void Cancellation_stops_the_loop()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var session = Session(() => One);

        session.Run(cancel.Token); // returns instead of waiting for a key

        Assert.Equal(0, session.Refreshes);
    }
}

public sealed class RepoChangeWatcherTests : IDisposable
{
    private readonly TempRepo _repo = new();

    [Theory]
    [InlineData("HEAD", true)]
    [InlineData("refs/heads/main", true)]
    [InlineData("refs\\tags\\v1", true)]
    [InlineData("packed-refs", true)]
    [InlineData("logs/HEAD", true)]
    [InlineData("index", false)]
    [InlineData("objects/ab/cdef", false)]
    [InlineData("FETCH_HEAD", false)]
    public void Only_history_files_count_as_changes(string path, bool relevant)
    {
        Assert.Equal(relevant, RepoChangeWatcher.IsRelevant(path));
    }

    [Fact]
    public void Notices_a_new_commit_once()
    {
        _repo.Commit("One", "Ada", Day(1, 1), ("a.txt", "a\n"));
        using var watcher = new RepoChangeWatcher(_repo.Dir, TimeSpan.FromMilliseconds(50));
        Assert.False(watcher.ConsumeChange());

        _repo.Commit("Two", "Ada", Day(1, 2), ("a.txt", "b\n"));

        Assert.True(WaitFor(watcher.ConsumeChange, TimeSpan.FromSeconds(10)));
        Thread.Sleep(200);
        Assert.False(watcher.ConsumeChange()); // reported once, not on every poll
    }

    [Fact]
    public void Notices_a_new_tag()
    {
        _repo.Commit("One", "Ada", Day(1, 1), ("a.txt", "a\n"));
        using var watcher = new RepoChangeWatcher(_repo.Dir, TimeSpan.FromMilliseconds(50));

        _repo.Tag("v1");

        Assert.True(WaitFor(watcher.ConsumeChange, TimeSpan.FromSeconds(10)));
    }

    private static bool WaitFor(Func<bool> condition, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(60);
        }

        return false;
    }

    public void Dispose() => _repo.Dispose();
}

public sealed class WatchCliTests : IDisposable
{
    private readonly TempRepo _repo = new();

    private static CommandAppResult Run(params string[] args)
    {
        var tester = new CommandAppTester(null!, new CommandAppTesterSettings(), new TestConsole().Width(120));
        tester.SetDefaultCommand<InteractiveCommand>();
        tester.Configure(GitPulseApp.Configure);
        return tester.Run(args);
    }

    [Fact]
    public void Watch_needs_an_interactive_terminal()
    {
        _repo.Commit("One", "Ada", Day(1, 1), ("a.txt", "a\n"));

        var result = Run("watch", "--path", _repo.Dir);

        Assert.Equal(GitPulseApp.ExitExpectedError, result.ExitCode);
        Assert.Contains("Watch needs a terminal", result.Output);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("61")]
    public void Interval_out_of_range_is_a_usage_error(string interval)
    {
        var result = Run("watch", "--path", _repo.Dir, "--interval", interval);

        Assert.Equal(GitPulseApp.ExitUsageError, result.ExitCode);
        Assert.Contains("--interval must be between 1 and 60", result.Output);
    }

    public void Dispose() => _repo.Dispose();
}
