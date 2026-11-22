using GitPulse.Commands;
using GitPulse.Interactive;
using GitPulse.Tests.Git;
using Spectre.Console.Testing;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Cli;

/// <summary>Drives the interactive menu with simulated key presses.</summary>
public sealed class InteractiveTests : IDisposable
{
    // Menu order: Overview, Authors, Hotspots, Activity, Rows to show, Exit.
    private const int AuthorsIndex = 1;
    private const int HotspotsIndex = 2;
    private const int ActivityIndex = 3;
    private const int RowCountIndex = 4;
    private const int ExitIndex = 5;

    private readonly TempRepo _repo = new();
    private readonly TestConsole _console = new TestConsole().Width(120).Interactive();

    public InteractiveTests()
    {
        _repo.Commit("Add", "Ada Lovelace", Day(1, 1), ("a.cs", "class A {}\n"), ("b.cs", "class B {}\n"));
        _repo.Commit("Edit", "Linus Torvalds", Day(2, 1), ("a.cs", "class A { }\n"));
    }

    private (int ExitCode, string Output) Run()
    {
        var command = new InteractiveCommand(_console);
        var exitCode = command.Execute(null!, new RepoSettings { Path = _repo.Dir }, TestContext.Current.CancellationToken);
        return (exitCode, _console.Output);
    }

    /// <summary>
    /// Moves down <paramref name="index"/> times and presses Enter. Arrow keys are pushed with no
    /// character, like a real terminal sends them: <c>PushKey(ConsoleKey.DownArrow)</c> would carry
    /// '(' as its character, which the menu's search box treats as typed text.
    /// </summary>
    private void Choose(int index)
    {
        for (var i = 0; i < index; i++)
        {
            _console.Input.PushKey(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, shift: false, alt: false, control: false));
        }

        _console.Input.PushKey(ConsoleKey.Enter);
    }

    [Fact]
    public void Shows_banner_overview_and_exits()
    {
        Choose(ExitIndex);

        var (exitCode, output) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Understand a repository's history", output);
        Assert.Contains("Summary", output);
        Assert.Contains("What next?", output);
    }

    [Fact]
    public void Navigates_to_authors_and_hotspots()
    {
        Choose(AuthorsIndex);
        Choose(HotspotsIndex);
        Choose(ExitIndex);

        var (exitCode, output) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Top 2 of 2 authors", output);
        Assert.Contains("Top 2 of 2 changed files", output);
    }

    [Fact]
    public void Navigates_to_the_activity_heatmap()
    {
        Choose(ActivityIndex);
        Choose(ExitIndex);

        var (exitCode, output) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Commits by weekday", output);
    }

    [Fact]
    public void Search_selects_a_menu_item()
    {
        _console.Input.PushTextWithEnter("files"); // "Most-changed files"
        Choose(ExitIndex);

        var (exitCode, output) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Top 2 of 2 changed files", output);
    }

    [Fact]
    public void Row_count_prompt_validates_and_applies()
    {
        Choose(RowCountIndex);
        _console.Input.PushTextWithEnter("500"); // rejected
        _console.Input.PushTextWithEnter("1");   // accepted
        Choose(AuthorsIndex);
        Choose(ExitIndex);

        var (exitCode, output) = Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Choose a number from 1 to 100.", output);
        Assert.Contains("Top 1 of 2 authors", output);
    }

    public void Dispose() => _repo.Dispose();
}
