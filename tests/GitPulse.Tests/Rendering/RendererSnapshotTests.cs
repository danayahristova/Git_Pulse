using GitPulse.Models;
using GitPulse.Rendering;
using Spectre.Console.Rendering;
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
