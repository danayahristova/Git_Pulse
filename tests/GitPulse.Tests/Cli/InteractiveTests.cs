using GitPulse.Commands;
using GitPulse.Interactive;
using GitPulse.Tests.Git;
using Spectre.Console.Testing;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Cli;

/// <summary>Drives the interactive menu with simulated key presses.</summary>
public sealed class InteractiveTests : IDisposable
{
    private readonly TempRepo _repo = new();
    private readonly TestConsole _console = new TestConsole().Width(120).Interactive();

    public InteractiveTests()
    {
        // Ada: 1 commit touching 2 files; Linus: 1 commit touching 1 file.
        _repo.Commit("Add", "Ada Lovelace", Day(1, 1), ("a.cs", "class A {}\n"), ("b.cs", "class B {}\n"));
        _repo.Commit("Edit", "Linus Torvalds", Day(2, 1), ("a.cs", "class A { }\n"));
    }

    private (int ExitCode, string Output) Run()
    {
        var command = new InteractiveCommand(_console);
        var exitCode = command.Execute(null!, new RepoSettings { Path = _repo.Dir }, TestContext.Current.CancellationToken);
        return (exitCode, _console.Output);
    }

    /// <summary>Types into a menu's search box and presses Enter: picks the first match.</summary>
    private void Choose(string text) => _console.Input.PushTextWithEnter(text);

    /// <summary>
    /// Arrow keys are pushed with no character, like a real terminal sends them:
    /// <c>PushKey(ConsoleKey.DownArrow)</c> would carry '(' as its character, which search boxes treat as text.
    /// </summary>
    private void Down(int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            _console.Input.PushKey(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, shift: false, alt: false, control: false));
        }
    }

    private void Space() => _console.Input.PushKey(ConsoleKey.Spacebar);

    private void Enter() => _console.Input.PushKey(ConsoleKey.Enter);

    [Fact]
    public void Shows_banner_overview_and_exits()
    {
        Choose("Exit");

        var (exitCode, output) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Understand a repository's history", output);
        Assert.Contains("Summary", output);
        Assert.Contains("What next?", output);
    }

    [Fact]
    public void Arrow_keys_navigate_the_menu()
    {
        Down();          // Top authors
        Enter();
        Down(2);         // Most-changed files
        Enter();
        Choose("Exit");

        var (exitCode, output) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Top 2 of 2 authors", output);
        Assert.Contains("Top 2 of 2 changed files", output);
    }

    [Fact]
    public void Search_selects_a_menu_item()
    {
        Choose("files"); // "Most-changed files"
        Choose("heatmap");
        Choose("Exit");

        var (exitCode, output) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Top 2 of 2 changed files", output);
        Assert.Contains("Commits by weekday", output);
    }

    [Fact]
    public void Row_count_prompt_validates_and_applies()
    {
        Choose("Rows");
        _console.Input.PushTextWithEnter("500"); // rejected
        _console.Input.PushTextWithEnter("1");   // accepted
        Choose("Top authors");
        Choose("Exit");

        var (exitCode, output) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Choose a number from 1 to 100.", output);
        Assert.Contains("Top 1 of 2 authors", output);
    }

    [Fact]
    public void Commit_details_picks_a_commit_with_search()
    {
        Choose("Commit details");
        Choose("Edit");   // search the picker for the commit message
        Choose("Exit");

        var (exitCode, output) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Pick a commit", output);
        Assert.Contains("2026-02-01 · Sunday", output); // day group header
        Assert.Contains("Linus Torvalds <linus@example.com>", output);
        Assert.Contains("+class A { }", output);
    }

    [Fact]
    public void Author_filter_uses_a_multi_selection()
    {
        Choose("Filter by author");
        Down();          // Linus (Ada is first: more lines changed)
        Space();
        Enter();
        Choose("Top authors");
        Choose("Exit");

        var (exitCode, output) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Show commits by which authors?", output);
        Assert.Contains("Top 1 of 1 author", output);
        Assert.Contains("author linus@example.com", output);
    }

    [Fact]
    public void Clear_filters_shows_everyone_again()
    {
        Choose("Filter by author");
        Space();         // Ada
        Enter();
        Choose("Top authors");
        Choose("Clear filters");
        Choose("Top authors");
        Choose("Exit");

        var (exitCode, output) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Top 1 of 1 author", output);
        Assert.Contains("Top 2 of 2 authors", output);
    }

    [Fact]
    public void Date_range_prompts_validate_and_rescan()
    {
        Choose("Date range");
        _console.Input.PushTextWithEnter("2099-01-01"); // since: rejected, in the future
        _console.Input.PushTextWithEnter("banana");     // since: rejected, not a date
        _console.Input.PushTextWithEnter("2026-01-15"); // since: accepted
        _console.Input.PushTextWithEnter("2026-01-01"); // until: rejected, before since
        _console.Input.PushTextWithEnter("");           // until: default (today)
        Choose("Exit");

        var (exitCode, output) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("That date is in the future.", output);
        Assert.Contains("Please enter a date like 2026-01-31.", output);
        Assert.Contains("Choose 2026-01-15 or later.", output);
        Assert.Contains("1 commit · 1 author · since 2026-01-15", output);
    }

    [Fact]
    public void A_date_range_without_commits_keeps_the_old_filters()
    {
        Choose("Date range");
        _console.Input.PushTextWithEnter("2026-03-01");
        _console.Input.PushTextWithEnter("2026-03-02");
        Choose("Exit");

        var (exitCode, output) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("No commits in range", output);
        Assert.Contains("The filters were not changed.", output);
    }

    public void Dispose() => _repo.Dispose();
}
