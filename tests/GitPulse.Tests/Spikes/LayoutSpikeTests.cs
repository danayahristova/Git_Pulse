using Spectre.Console;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;

namespace GitPulse.Tests.Spikes;

/// <summary>
/// Phase 0 spike: how do Layout, BarChart and BreakdownChart behave at different widths?
/// Kept as a regression guard: rendering must never throw and never exceed the console width.
/// </summary>
public sealed class LayoutSpikeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(120)]
    [InlineData(100)]
    [InlineData(80)]
    [InlineData(60)]
    [InlineData(40)]
    public void Dashboard_layout_fits_console_width(int width)
    {
        var console = new TestConsole().Width(width);
        console.Profile.Height = 24;

        console.Write(BuildDashboard());

        var lines = console.Lines;
        output.WriteLine($"--- width {width}, {lines.Count} lines ---");
        foreach (var line in lines)
        {
            output.WriteLine(line);
        }

        // Set GITPULSE_SPIKE_OUT=<dir> to dump renders for eyeballing.
        if (Environment.GetEnvironmentVariable("GITPULSE_SPIKE_OUT") is { Length: > 0 } dir)
        {
            File.WriteAllLines(Path.Combine(dir, $"dashboard-{width}.txt"), lines);
        }

        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.True(l.Length <= width, $"line longer than {width}: '{l}'"));
    }

    private static IRenderable BuildDashboard()
    {
        var languages = new BreakdownChart()
            .FullSize()
            .AddItem("C#", 62.5, Color.Green)
            .AddItem("Markdown", 20.1, Color.Blue)
            .AddItem("JSON", 10.4, Color.Yellow)
            .AddItem("YAML", 7.0, Color.Purple);

        var authors = new BarChart()
            .Label("[bold]Top authors[/]")
            .AddItem("Ada Lovelace", 412, Color.Green)
            .AddItem("Linus", 233, Color.Blue)
            .AddItem("Grace Hopper", 120, Color.Yellow)
            .AddItem("Someone With A Very Long Name", 64, Color.Purple)
            // Finding: BarChart labels are parsed as markup, so names must be escaped
            // ("bot[dependabot]" unescaped throws "malformed markup tag").
            .AddItem(Markup.Escape("bot[dependabot]"), 12, Color.Grey);

        var layout = new Layout("root")
            .SplitRows(
                new Layout("header").Size(3),
                new Layout("body").SplitColumns(
                    new Layout("languages"),
                    new Layout("authors")));

        layout["header"].Update(new Panel("[bold]GitPulse[/] spike").Expand());
        layout["languages"].Update(new Panel(languages).Header("Languages").Expand());
        layout["authors"].Update(new Panel(authors).Header("Authors").Expand());

        return layout;
    }
}
