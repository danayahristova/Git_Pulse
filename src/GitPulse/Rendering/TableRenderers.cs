using GitPulse.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace GitPulse.Rendering;

/// <summary>The contributor table.</summary>
internal static class AuthorsRenderer
{
    public static IRenderable Render(RepoStats stats, int top, int width)
    {
        if (Breakpoints.IsTooNarrow(width))
        {
            return Widgets.TooNarrow(width);
        }

        var compact = Breakpoints.IsCompact(width);
        var shown = stats.Authors.Take(top).ToList();

        var table = new Table()
            .BorderStyle(Theme.Border)
            .Title(Theme.Title($"Top {shown.Count} of {Format.Plural(stats.Authors.Count, "author")}"))
            .AddColumn(new TableColumn(Header("#")).RightAligned())
            .AddColumn(new TableColumn(Header("Author")))
            .AddColumn(new TableColumn(Header("Commits")).RightAligned())
            .AddColumn(new TableColumn(Header("Added")).RightAligned())
            .AddColumn(new TableColumn(Header("Deleted")).RightAligned());

        if (!compact)
        {
            table.AddColumn(new TableColumn(Header("First")));
            table.AddColumn(new TableColumn(Header("Last")));
        }

        foreach (var (a, i) in shown.Select((a, i) => (a, i)))
        {
            var cells = new List<IRenderable>
            {
                new Markup(Theme.Dim((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))),
                new Markup(Markup.Escape(a.Name)),
                new Markup(Theme.Bold(Format.Number(a.Commits))),
                new Markup(Theme.AddedText(a.LinesAdded)),
                new Markup(Theme.DeletedText(a.LinesDeleted)),
            };

            if (!compact)
            {
                cells.Add(new Markup(Markup.Escape(Format.Date(a.FirstCommit))));
                cells.Add(new Markup(Markup.Escape(Format.Date(a.LastCommit))));
            }

            table.AddRow(cells);
        }

        return table;
    }

    internal static Markup Header(string text) => new(Markup.Escape(text), Theme.TableHeader);
}

/// <summary>The most-changed files table. Long paths are shortened by <see cref="TextPath"/>.</summary>
internal static class HotspotsRenderer
{
    public static IRenderable Render(RepoStats stats, int top, int width)
    {
        if (Breakpoints.IsTooNarrow(width))
        {
            return Widgets.TooNarrow(width);
        }

        var compact = Breakpoints.IsCompact(width);
        var shown = stats.Hotspots.Take(top).ToList();

        var table = new Table()
            .BorderStyle(Theme.Border)
            .Title(Theme.Title($"Top {shown.Count} of {Format.Plural(stats.Hotspots.Count, "changed file")}"))
            .AddColumn(new TableColumn(AuthorsRenderer.Header("#")).RightAligned())
            .AddColumn(new TableColumn(AuthorsRenderer.Header("File")))
            .AddColumn(new TableColumn(AuthorsRenderer.Header("Commits")).RightAligned());

        if (!compact)
        {
            table.AddColumn(new TableColumn(AuthorsRenderer.Header("Added")).RightAligned());
            table.AddColumn(new TableColumn(AuthorsRenderer.Header("Deleted")).RightAligned());
        }

        table.AddColumn(new TableColumn(AuthorsRenderer.Header("Churn")).RightAligned());

        foreach (var (f, i) in shown.Select((f, i) => (f, i)))
        {
            var cells = new List<IRenderable>
            {
                new Markup(Theme.Dim((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))),
                Path(f.Path),
                new Markup(Markup.Escape(Format.Number(f.Commits))),
            };

            if (!compact)
            {
                cells.Add(new Markup(Theme.AddedText(f.LinesAdded)));
                cells.Add(new Markup(Theme.DeletedText(f.LinesDeleted)));
            }

            cells.Add(new Markup(Theme.Bold(Format.Number(f.Churn))));
            table.AddRow(cells);
        }

        return table;
    }

    /// <summary>TextPath takes the raw path (it is not parsed as markup) and shortens it to fit.</summary>
    private static TextPath Path(string path) =>
        new TextPath(path)
            .RootColor(Theme.Muted)
            .SeparatorColor(Theme.Muted)
            .StemColor(Theme.Muted)
            .LeafColor(Theme.Primary);
}

/// <summary>Friendly panel for expected errors.</summary>
internal static class ErrorRenderer
{
    public static IRenderable Render(GitPulseException error)
    {
        var body = Markup.Escape(error.Message);
        if (error.Hint is { } hint)
        {
            body += "\n\n" + Theme.Dim(hint);
        }

        return new Panel(new Markup(body))
            .Header($" {Theme.Colored(error.Title, Theme.Error)} ")
            .BorderStyle(new Style(Theme.Error))
            .Padding(1, 0);
    }
}
