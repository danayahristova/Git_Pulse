using GitPulse.Analysis;
using GitPulse.Models;
using GitPulse.Rendering;
using Spectre.Console;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Rendering;

/// <summary>Snapshot tests for every renderer, at the MVP's target widths, using a fixed fake dataset.</summary>
public sealed class RendererSnapshotTests
{
    private static readonly RepoStats Stats = SampleStats();

    [Theory]
    [InlineData(120)]
    [InlineData(100)]
    [InlineData(80)]
    [InlineData(60)]
    [InlineData(30)]
    public void Overview(int width) => Check(OverviewRenderer.Render(Stats, width), width, $"overview-{width}");

    [Theory]
    [InlineData(120)]
    [InlineData(70)]
    public void Authors(int width) => Check(AuthorsRenderer.Render(Stats, top: 10, width), width, $"authors-{width}");

    [Theory]
    [InlineData(120)]
    [InlineData(100)]
    [InlineData(70)]
    public void Hotspots(int width) => Check(HotspotsRenderer.Render(Stats, top: 10, width), width, $"hotspots-{width}");

    [Theory]
    [InlineData(120)]
    [InlineData(100)]
    [InlineData(80)]
    [InlineData(60)]
    [InlineData(30)]
    public void Activity(int width) => Check(ActivityRenderer.Render(Stats, width), width, $"activity-{width}");

    [Fact]
    public void Heatmap_uses_a_canvas_when_the_terminal_has_colors()
    {
        var console = new TestConsole().Width(120).Colors(ColorSystem.TrueColor).EmitAnsiSequences();
        console.Profile.Capabilities.Unicode = true;

        console.Write(new HeatmapGrid(Stats.Heatmap));

        Assert.Contains("\u001b[", console.Output);  // colored cells
        Assert.DoesNotContain("▓", console.Output); // no glyph fallback
    }

    [Fact]
    public void Heatmap_month_labels_do_not_overlap()
    {
        var header = HeatmapGrid.MonthHeader(Stats.Heatmap);

        Assert.StartsWith(new string(' ', HeatmapGrid.LabelWidth), header);
        var months = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.InRange(months.Length, 11, 13); // a label is skipped when it would touch its neighbour
        Assert.Equal("Nov", months[^1]);
    }

    [Fact]
    public void Activity_with_no_recent_commits()
    {
        var stats = Stats with { Heatmap = HeatmapBuilder.Build(new Dictionary<DateOnly, int>(), new DateOnly(2026, 11, 18)) };
        var output = Snapshot.Render(ActivityRenderer.Render(stats, 100), 100);

        Assert.Contains("No commits in this period.", output);
    }

    [Fact]
    public void Authors_respects_top() =>
        Check(AuthorsRenderer.Render(Stats, top: 2, width: 100), 100, "authors-top2");

    [Fact]
    public void Error_panel() =>
        Check(ErrorRenderer.Render(new NotAGitRepositoryException("/tmp/nowhere")), 100, "error-not-a-repo");

    [Fact]
    public void Overview_handles_a_repo_without_recognized_languages()
    {
        var stats = Stats with { Languages = [] };
        Check(OverviewRenderer.Render(stats, 100), 100, "overview-no-languages");
    }

    private static void Check(IRenderable renderable, int width, string name)
    {
        var output = Snapshot.Render(renderable, width);

        // Never wider than the terminal: a too-wide line wraps and garbles the screen.
        Assert.All(output.ReplaceLineEndings("\n").Split('\n'), line =>
            Assert.True(line.TrimEnd().Length <= width, $"Line wider than {width}: '{line}'"));

        Snapshot.Match(output, name);
    }
}
