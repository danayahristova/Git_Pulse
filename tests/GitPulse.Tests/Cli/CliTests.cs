using GitPulse.Interactive;
using GitPulse.Tests.Git;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Cli;

/// <summary>
/// End-to-end: real command-line parsing, validation, error handling and commands,
/// against throwaway repositories.
/// </summary>
public sealed class CliTests : IDisposable
{
    private readonly TempRepo _repo = new();

    private static CommandAppResult Run(params string[] args)
    {
        var tester = new CommandAppTester(null!, new CommandAppTesterSettings(), new TestConsole().Width(120));
        tester.SetDefaultCommand<InteractiveCommand>();
        tester.Configure(GitPulseApp.Configure);
        return tester.Run(args);
    }

    private void SeedHistory()
    {
        _repo.Commit("Add app", "Ada Lovelace", Day(1, 1), ("src/App.cs", "class App {}\n"), ("README.md", "# demo\n"));
        _repo.Commit("Grow app", "Ada Lovelace", Day(2, 1), ("src/App.cs", "class App {\n  int x;\n}\n"));
        _repo.Commit("Docs", "Linus Torvalds", Day(3, 1), ("README.md", "# demo\n\nMore.\n"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    public void Top_out_of_range_is_a_usage_error(string top)
    {
        var result = Run("authors", "--path", _repo.Dir, "--top", top);

        Assert.Equal(GitPulseApp.ExitUsageError, result.ExitCode);
        Assert.Contains("--top must be between 1 and 100", result.Output);
    }

    [Fact]
    public void Since_that_is_not_a_date_is_a_usage_error()
    {
        var result = Run("authors", "--path", _repo.Dir, "--since", "banana");

        Assert.Equal(GitPulseApp.ExitUsageError, result.ExitCode);
        Assert.Contains("banana", result.Output);
    }

    [Fact]
    public void Outside_a_repository_shows_a_friendly_error()
    {
        using var notARepo = new TempRepo(init: false);

        var result = Run("overview", "--path", notARepo.Dir);

        Assert.Equal(GitPulseApp.ExitExpectedError, result.ExitCode);
        Assert.Contains("Not a Git repository", result.Output);
        Assert.DoesNotContain("   at ", result.Output); // no stack trace
    }

    [Fact]
    public void Empty_repository_shows_a_friendly_error()
    {
        var result = Run("authors", "--path", _repo.Dir);

        Assert.Equal(GitPulseApp.ExitExpectedError, result.ExitCode);
        Assert.Contains("Empty repository", result.Output);
    }

    [Fact]
    public void Authors_lists_contributors()
    {
        SeedHistory();

        var result = Run("authors", "--path", _repo.Dir);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Ada Lovelace", result.Output);
        Assert.Contains("Linus Torvalds", result.Output);
        Assert.True(result.Output.IndexOf("Ada Lovelace", StringComparison.Ordinal)
                    < result.Output.IndexOf("Linus Torvalds", StringComparison.Ordinal));
    }

    [Fact]
    public void Hotspots_lists_changed_files()
    {
        SeedHistory();

        var result = Run("hotspots", "--path", _repo.Dir, "--top", "1");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Top 1 of 2 changed files", result.Output);
    }

    [Fact]
    public void Overview_shows_the_dashboard()
    {
        SeedHistory();

        var result = Run("overview", "--path", _repo.Dir);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Summary", result.Output);
        Assert.Contains("Languages", result.Output);
        Assert.Contains("C#", result.Output);
    }

    [Fact]
    public void Scan_reports_what_it_read()
    {
        SeedHistory();

        var result = Run("scan", "--path", _repo.Dir);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Scanned 3 commits by 2 authors and 2 files", result.Output);
    }

    [Fact]
    public void Since_with_no_matching_commits_shows_a_friendly_error()
    {
        SeedHistory();

        var result = Run("authors", "--path", _repo.Dir, "--since", "2026-10-01");

        Assert.Equal(GitPulseApp.ExitExpectedError, result.ExitCode);
        Assert.Contains("No commits in range", result.Output);
    }

    [Fact]
    public void Default_command_prints_the_overview_when_not_interactive()
    {
        SeedHistory();

        var result = Run("--path", _repo.Dir);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Summary", result.Output);
    }

    public void Dispose() => _repo.Dispose();
}

public sealed class RepoSettingsTests
{
    [Fact]
    public void Future_since_is_rejected()
    {
        var settings = new GitPulse.Commands.RepoSettings { Since = DateOnly.FromDateTime(DateTime.Today.AddDays(2)) };
        Assert.False(settings.Validate().Successful);
    }

    [Fact]
    public void Defaults_are_valid()
    {
        Assert.True(new GitPulse.Commands.RepoSettings().Validate().Successful);
    }
}
