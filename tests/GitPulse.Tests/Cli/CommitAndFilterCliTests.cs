using System.Text.Json;
using GitPulse.Interactive;
using GitPulse.Tests.Git;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Cli;

/// <summary>End-to-end tests for <c>commit</c> and the --until/--branch/--author options.</summary>
public sealed class CommitAndFilterCliTests : IDisposable
{
    private readonly TempRepo _repo = new();

    public CommitAndFilterCliTests()
    {
        _repo.Commit("Add app", "Ada Lovelace", Day(1, 10), ("src/App.cs", "class App {}\n"), ("README.md", "# demo\n"));
        _repo.Commit("Grow app\n\nAdds a field.", "Ada Lovelace", Day(2, 10), ("src/App.cs", "class App {\n  int x;\n}\n"));
        _repo.Commit("Docs", "Linus Torvalds", Day(3, 10), ("README.md", "# demo\n\nMore.\n"));
    }

    private static CommandAppResult Run(params string[] args)
    {
        var tester = new CommandAppTester(null!, new CommandAppTesterSettings(), new TestConsole().Width(120));
        tester.SetDefaultCommand<InteractiveCommand>();
        tester.Configure(GitPulseApp.Configure);
        return tester.Run(args);
    }

    [Fact]
    public void Commit_shows_header_message_files_and_diff()
    {
        var result = Run("commit", "HEAD~1", "--path", _repo.Dir);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Ada Lovelace", result.Output);
        Assert.Contains("Grow app", result.Output);
        Assert.Contains("Adds a field.", result.Output);
        Assert.Contains("src/App.cs", result.Output);
        Assert.Contains("+  int x;", result.Output);
        Assert.Contains("-class App {}", result.Output);
    }

    [Fact]
    public void Commit_no_diff_shows_the_file_list_only()
    {
        var result = Run("commit", "HEAD", "--path", _repo.Dir, "--no-diff");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("README.md", result.Output);
        Assert.DoesNotContain("+More.", result.Output);
        Assert.DoesNotContain("── README.md", result.Output); // no diff section at all
        Assert.DoesNotContain("not shown", result.Output);
    }

    [Fact]
    public void Commit_raw_prints_valid_json()
    {
        var result = Run("commit", "HEAD", "--path", _repo.Dir, "--raw");

        Assert.Equal(0, result.ExitCode);
        using var json = JsonDocument.Parse(result.Output);
        var root = json.RootElement;
        Assert.Equal("Docs", root.GetProperty("subject").GetString());
        Assert.Equal("Linus Torvalds", root.GetProperty("author").GetProperty("name").GetString());
        var file = root.GetProperty("files")[0];
        Assert.Equal("README.md", file.GetProperty("path").GetString());
        Assert.Equal("modified", file.GetProperty("status").GetString());
        Assert.Equal(2, root.GetProperty("stats").GetProperty("added").GetInt32());
    }

    [Fact]
    public void Unknown_commit_is_a_friendly_error()
    {
        var result = Run("commit", "nope", "--path", _repo.Dir);

        Assert.Equal(GitPulseApp.ExitExpectedError, result.ExitCode);
        Assert.Contains("Commit not found", result.Output);
    }

    [Fact]
    public void Commit_without_revision_needs_an_interactive_terminal()
    {
        var result = Run("commit", "--path", _repo.Dir);

        Assert.Equal(GitPulseApp.ExitExpectedError, result.ExitCode);
        Assert.Contains("No commit given", result.Output);
        Assert.Contains("gitpulse commit HEAD", result.Output);
    }

    [Fact]
    public void Author_option_filters_every_view()
    {
        var result = Run("authors", "--path", _repo.Dir, "--author", "linus");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Top 1 of 1 author", result.Output);
        Assert.DoesNotContain("Ada Lovelace", result.Output);
    }

    [Fact]
    public void Author_option_can_be_repeated()
    {
        var result = Run("authors", "--path", _repo.Dir, "-a", "linus", "-a", "ada");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Top 2 of 2 authors", result.Output);
    }

    [Fact]
    public void Until_option_limits_the_range()
    {
        var result = Run("scan", "--path", _repo.Dir, "--until", "2026-02-15");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Scanned 2 commits by 1 author", result.Output);
    }

    [Fact]
    public void Scan_reports_the_cache()
    {
        var first = Run("scan", "--path", _repo.Dir);
        var second = Run("scan", "--path", _repo.Dir);
        var off = Run("scan", "--path", _repo.Dir, "--no-cache");

        Assert.Contains("Cache: 0 from cache, 3 commits diffed.", first.Output);
        Assert.Contains("Cache: 3 from cache, 0 commits diffed.", second.Output);
        Assert.Contains("Cache: off (--no-cache).", off.Output);
    }

    [Fact]
    public void Until_before_since_is_a_usage_error()
    {
        var result = Run("scan", "--path", _repo.Dir, "--since", "2026-03-01", "--until", "2026-02-01");

        Assert.Equal(GitPulseApp.ExitUsageError, result.ExitCode);
        Assert.Contains("is before --since", result.Output);
    }

    [Fact]
    public void Unknown_branch_is_a_friendly_error()
    {
        var result = Run("overview", "--path", _repo.Dir, "--branch", "nope");

        Assert.Equal(GitPulseApp.ExitExpectedError, result.ExitCode);
        Assert.Contains("Branch not found", result.Output);
    }

    [Fact]
    public void Overview_header_describes_the_filter()
    {
        var result = Run("overview", "--path", _repo.Dir, "--since", "2026-01-01", "--author", "ada");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("since 2026-01-01 · author ada", result.Output);
    }

    [Fact]
    public void Author_matching_nobody_is_a_friendly_error()
    {
        var result = Run("hotspots", "--path", _repo.Dir, "--author", "grace");

        Assert.Equal(GitPulseApp.ExitExpectedError, result.ExitCode);
        Assert.Contains("No matching commits", result.Output);
    }

    public void Dispose() => _repo.Dispose();
}
