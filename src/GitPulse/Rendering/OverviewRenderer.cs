using GitPulse.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace GitPulse.Rendering;

/// <summary>
/// The overview dashboard: header, summary grid, language breakdown and top authors.
/// Side by side in a <see cref="Layout"/> at 80+ columns, stacked below that.
/// </summary>
internal static class OverviewRenderer
{
    public const int TopAuthors = 5;

    public static IRenderable Render(RepoStats stats, int width)
    {
        if (Breakpoints.IsTooNarrow(width))
        {
            return Widgets.TooNarrow(width);
        }

        var compact = Breakpoints.IsCompact(width);
        var columnWidth = compact ? width : width / 2;
        var nameWidth = columnWidth >= 50 ? 20 : 16;

        var header = Header(stats.Summary);
        var summary = Widgets.Panel(SummaryGrid(stats.Summary), "Summary");
        var languages = Widgets.Panel(LanguageChart(stats.Languages), "Languages");
        var authors = Widgets.Panel(AuthorChart(stats.Authors, nameWidth), $"Top {TopAuthors} authors");

        if (compact)
        {
            return new Rows(header, summary, languages, authors);
        }

        // A Layout fills the console height unless told otherwise (see docs/spikes.md),
        // so size every row from its content and pin the total height.
        var summaryHeight = SummaryRowCount + 2;
        var languagesHeight = LanguagePanelHeight(stats.Languages, columnWidth - 4);
        var authorsHeight = AuthorPanelHeight(stats.Authors);
        var bodyHeight = Math.Max(summaryHeight + languagesHeight, authorsHeight + MinHotspotsHeight);

        // The right column is usually shorter: fill the rest with the top hotspots.
        var hotspotRows = bodyHeight - authorsHeight - 2;
        var hotspots = Widgets.Panel(HotspotList(stats.Hotspots, hotspotRows), "Top hotspots");

        var layout = new Layout("root").SplitRows(
            new Layout("header").Size(HeaderHeight),
            new Layout("body").Size(bodyHeight).SplitColumns(
                new Layout("left").SplitRows(
                    new Layout("summary").Size(summaryHeight),
                    new Layout("languages")),
                new Layout("right").SplitRows(
                    new Layout("authors").Size(authorsHeight),
                    new Layout("hotspots"))));

        layout["header"].Update(header);
        layout["summary"].Update(summary);
        layout["languages"].Update(languages);
        layout["authors"].Update(authors);
        layout["hotspots"].Update(hotspots);

        return new FixedHeight(layout, HeaderHeight + bodyHeight);
    }

    /// <summary>Borders + chart label + one row per author shown.</summary>
    private static int AuthorPanelHeight(IReadOnlyList<AuthorStats> authors) =>
        2 + 1 + Math.Max(1, Math.Min(TopAuthors, authors.Count));

    private const int MinHotspotsHeight = 5;

    private static IRenderable HotspotList(IReadOnlyList<FileChurn> hotspots, int rows)
    {
        if (hotspots.Count == 0)
        {
            return new Markup(Theme.Dim("No file changes."));
        }

        var grid = new Grid()
            .Expand()
            .AddColumn(new GridColumn())
            .AddColumn(new GridColumn().NoWrap().RightAligned());

        foreach (var f in hotspots.Take(rows))
        {
            grid.AddRow(
                new TextPath(f.Path).RootColor(Theme.Muted).SeparatorColor(Theme.Muted)
                    .StemColor(Theme.Muted).LeafColor(Theme.Primary),
                new Markup(Theme.Dim(Format.Number(f.Churn))));
        }

        return grid;
    }

    private const int HeaderHeight = 3;

    private static Panel Header(RepoSummary s)
    {
        var range = s.Since is { } since ? $"since {Format.Date(since)}" : "all history";
        var text =
            $"{Theme.Title(s.Name)}  " +
            Theme.Dim($"{Format.Plural(s.Commits, "commit")} · {Format.Plural(s.Authors, "author")} · {range}");

        return new Panel(new Markup(text)).BorderStyle(new Style(Theme.Primary)).Expand();
    }

    private const int SummaryRowCount = 8;

    private static Grid SummaryGrid(RepoSummary s)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(2))
            .AddColumn(new GridColumn().NoWrap());

        void Row(string label, string value) => grid.AddRow(new Markup(Theme.Dim(label)), new Markup(value));

        Row("Commits", Theme.Bold(Format.Number(s.Commits)));
        Row("Authors", Theme.Bold(Format.Number(s.Authors)));
        Row("First commit", Markup.Escape(Format.Date(s.FirstCommit)));
        Row("Last commit", Markup.Escape(Format.Date(s.LastCommit)));
        Row("Active for", Markup.Escape(Format.Plural(Format.SpanDays(s.FirstCommit, s.LastCommit), "day")));
        Row("Branches", Markup.Escape(Format.Number(s.Branches)));
        Row("Tags", Markup.Escape(Format.Number(s.Tags)));
        Row("Files", Markup.Escape(Format.Number(s.Files)));
        return grid;
    }

    private static IRenderable LanguageChart(IReadOnlyList<LanguageShare> languages)
    {
        if (languages.Count == 0)
        {
            return new Markup(Theme.Dim("No recognized source files."));
        }

        var chart = new BreakdownChart()
            .FullSize()
            .UseValueFormatter(v => Format.Percent(v));

        for (var i = 0; i < languages.Count; i++)
        {
            chart.AddItem(Markup.Escape(languages[i].Language), Math.Round(languages[i].Percent, 1), Theme.Series(i));
        }

        return chart;
    }

    /// <summary>Panel borders + bar + blank line + legend lines (the legend wraps like words).</summary>
    private static int LanguagePanelHeight(IReadOnlyList<LanguageShare> languages, int innerWidth)
    {
        if (languages.Count == 0)
        {
            return 3;
        }

        var lines = 1;
        var used = 0;
        foreach (var l in languages)
        {
            // "■ Name 12.3%" plus spacing between items
            var item = 2 + l.Language.Length + 1 + Format.Percent(l.Percent).Length + 4;
            if (used > 0 && used + item > innerWidth)
            {
                lines++;
                used = 0;
            }

            used += item;
        }

        return 2 + 1 + 1 + lines;
    }

    private static IRenderable AuthorChart(IReadOnlyList<AuthorStats> authors, int nameWidth)
    {
        if (authors.Count == 0)
        {
            return new Markup(Theme.Dim("No authors."));
        }

        var chart = new BarChart()
            .Label(Theme.Dim("commits"))
            .RightAlignLabel()
            .UseValueFormatter(v => Format.Number((long)v));

        foreach (var (author, i) in authors.Take(TopAuthors).Select((a, i) => (a, i)))
        {
            // BarChart labels are parsed as markup: always escape repo-provided names.
            chart.AddItem(Markup.Escape(Format.Truncate(author.Name, nameWidth)), author.Commits, Theme.Series(i));
        }

        return chart;
    }
}
