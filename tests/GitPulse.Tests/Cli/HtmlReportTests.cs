using GitPulse.Commands;
using GitPulse.Interactive;
using GitPulse.Models;
using GitPulse.Rendering;
using GitPulse.Tests.Git;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Cli;

public sealed class HtmlReportBuildTests
{
    private static readonly DateTimeOffset Generated = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Is_a_standalone_page_with_every_section()
    {
        var html = HtmlReport.Build(SampleStats(), top: 10, Generated);

        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains("<meta charset=\"utf-8\">", html);
        Assert.Contains("<title>GitPulse · sample-repo</title>", html);
        Assert.Contains("generated 2026-11-22 12:00 +00:00", html);
        foreach (var section in new[] { "Overview", "Activity", "Authors", "Hotspots" })
        {
            Assert.Contains($"<h2>{section}</h2>", html);
        }

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", html);  // nothing external to load
        Assert.EndsWith("</html>\n", html);
    }

    [Fact]
    public void Keeps_colors_and_draws_the_heatmap_as_a_canvas()
    {
        var html = HtmlReport.Build(SampleStats(), top: 10, Generated);

        Assert.Contains("color: #", html);       // Spectre's colored spans
        Assert.Contains("background-color: #", html); // canvas cells
        Assert.DoesNotContain("▓", html);        // not the no-color fallback
    }

    [Fact]
    public void Repository_text_is_html_encoded()
    {
        var stats = SampleStats();
        stats = stats with { Summary = stats.Summary with { Name = "<script>alert(1)</script>" } };

        var html = HtmlReport.Build(stats, top: 10, Generated);

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("bot[dependabot]", html); // markup-looking names survive
    }

    [Fact]
    public void Writes_the_sample_report_when_asked()
    {
        // GITPULSE_WRITE_SAMPLE_REPORT=docs/sample-report.html regenerates the committed sample
        // from the fixed fake data (no real repository or people in it).
        if (Environment.GetEnvironmentVariable("GITPULSE_WRITE_SAMPLE_REPORT") is not { Length: > 0 } path)
        {
            return;
        }

        File.WriteAllText(path, HtmlReport.Build(SampleStats(), top: 10, Generated));
    }
}

public sealed class HtmlReportCommandTests : IDisposable
{
    private readonly TempRepo _repo = new();
    private readonly string _outDir = Directory.CreateTempSubdirectory("gitpulse-report-").FullName;

    public HtmlReportCommandTests()
    {
        _repo.Commit("Add", "Ada Lovelace", Day(1, 1), ("a.cs", "class A {}\n"));
    }

    private string Out(string name = "report.html") => Path.Combine(_outDir, name);

    private static CommandAppResult Run(params string[] args)
    {
        var tester = new CommandAppTester(null!, new CommandAppTesterSettings(), new TestConsole().Width(120));
        tester.SetDefaultCommand<InteractiveCommand>();
        tester.Configure(GitPulseApp.Configure);
        return tester.Run(args);
    }

    [Fact]
    public void Writes_the_report()
    {
        var result = Run("report", "--path", _repo.Dir, "--html", Out());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Report written:", result.Output);
        Assert.Contains("Ada Lovelace", File.ReadAllText(Out()));
        Assert.Empty(Directory.GetFiles(_outDir, "*.tmp"));
    }

    [Fact]
    public void Html_is_required()
    {
        var result = Run("report", "--path", _repo.Dir);

        Assert.Equal(GitPulseApp.ExitUsageError, result.ExitCode);
        Assert.Contains("gitpulse report --html report.html", result.Output);
    }

    [Fact]
    public void Refuses_to_overwrite_without_a_terminal_or_force()
    {
        File.WriteAllText(Out(), "keep me");

        var result = Run("report", "--path", _repo.Dir, "--html", Out());

        Assert.Equal(GitPulseApp.ExitExpectedError, result.ExitCode);
        Assert.Contains("File already exists", result.Output);
        Assert.Equal("keep me", File.ReadAllText(Out()));
    }

    [Fact]
    public void Force_overwrites()
    {
        File.WriteAllText(Out(), "old");

        var result = Run("report", "--path", _repo.Dir, "--html", Out(), "--force");

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("<!doctype html>", File.ReadAllText(Out()));
    }

    [Fact]
    public void Missing_folder_is_a_friendly_error()
    {
        var result = Run("report", "--path", _repo.Dir, "--html", Path.Combine(_outDir, "nope", "r.html"));

        Assert.Equal(GitPulseApp.ExitExpectedError, result.ExitCode);
        Assert.Contains("Folder not found", result.Output);
    }

    [Theory]
    [InlineData("y", true)]
    [InlineData("n", false)]
    [InlineData("", false)] // Enter takes the default: no
    public void Asks_before_overwriting_in_a_terminal(string answer, bool overwritten)
    {
        File.WriteAllText(Out(), "old");
        var console = new TestConsole().Width(120).Interactive();
        console.Input.PushTextWithEnter(answer);

        var exitCode = new HtmlReportCommand(console).Execute(
            null!, new HtmlReportSettings { Path = _repo.Dir, Html = Out() }, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("report.html already exists. Overwrite it?", console.Output);
        Assert.Equal(overwritten, File.ReadAllText(Out()) != "old");
        if (!overwritten)
        {
            Assert.Contains("Nothing was written.", console.Output);
        }
    }

    public void Dispose()
    {
        _repo.Dispose();
        Directory.Delete(_outDir, recursive: true);
    }
}
