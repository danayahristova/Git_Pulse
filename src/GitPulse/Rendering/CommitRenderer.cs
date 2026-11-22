using System.Text.Json;
using System.Text.Json.Serialization;
using GitPulse.Models;
using Spectre.Console;
using Spectre.Console.Json;
using Spectre.Console.Rendering;

namespace GitPulse.Rendering;

/// <summary>
/// <c>gitpulse commit</c>: a header panel (author, date, parents, link), the message,
/// a table of changed files and the colored diff.
/// </summary>
internal static class CommitRenderer
{
    /// <param name="showDiff">False for <c>--no-diff</c>: header and file list only.</param>
    public static IRenderable Render(CommitDetail commit, int width, bool showDiff = true)
    {
        if (Breakpoints.IsTooNarrow(width))
        {
            return Widgets.TooNarrow(width);
        }

        var parts = new List<IRenderable> { Header(commit, width), new Text(string.Empty), FilesTable(commit, width) };

        var diffs = showDiff
            ? commit.Files.Where(f => f.Lines.Count > 0 || f.HiddenLines > 0 || f.IsBinary).ToList()
            : [];
        if (diffs.Count > 0 && commit.IsMerge)
        {
            parts.Add(new Markup(Theme.Dim("Merge commit: the diff is against the first parent.")));
        }

        foreach (var file in diffs)
        {
            parts.Add(new Text(string.Empty));
            parts.Add(new Rule(Theme.Title(file.Path)).LeftJustified().RuleStyle(Theme.Border));
            parts.Add(Diff(file, width));
        }

        return new Rows(parts);
    }

    private static Panel Header(CommitDetail c, int width)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(2))
            .AddColumn(new GridColumn());

        void Row(string label, string markup) => grid.AddRow(new Markup(Theme.Dim(label)), new Markup(markup));

        Row("Author", Person(c.AuthorName, c.AuthorEmail));
        Row("Date", Markup.Escape(DateTime(c.AuthorWhen)));
        if (c.CommitterName != c.AuthorName || c.CommitterEmail != c.AuthorEmail)
        {
            Row("Committer", $"{Person(c.CommitterName, c.CommitterEmail)} {Theme.Dim(DateTime(c.CommitterWhen))}");
        }

        Row("Parents", c.Parents.Count == 0
            ? Theme.Dim("none (first commit)")
            : string.Join("  ", c.Parents.Select(p => Theme.Colored(p[..Math.Min(7, p.Length)], Theme.Accent))));
        Row("Changes",
            $"{Format.Plural(c.Files.Count, "file")}  {Theme.AddedText(c.LinesAdded)}  {Theme.DeletedText(c.LinesDeleted)}");

        if (c.WebUrl is { } url)
        {
            // RemoteLinks only produces plain https URLs, so the link can't break the markup.
            // When the full URL would wrap, show it with the short SHA (the hosts accept both);
            // the link itself always points at the full URL.
            var text = url.Length + 16 <= width ? url : url.Replace(c.Sha, c.ShortSha, StringComparison.Ordinal);
            Row("Link", $"[link={url}]{Markup.Escape(text)}[/]");
        }

        var content = new List<IRenderable> { grid, new Text(string.Empty), new Markup(Theme.Bold(c.Subject)) };
        if (c.Body.Length > 0)
        {
            content.Add(new Text(string.Empty));
            content.Add(new Text(c.Body));
        }

        return new Panel(new Rows(content))
            .Header($" {Theme.Title("commit")} {Theme.Colored(c.ShortSha, Theme.Accent)} ")
            .BorderStyle(Theme.Border)
            .Expand();
    }

    private static Table FilesTable(CommitDetail c, int width)
    {
        var table = new Table()
            .BorderStyle(Theme.Border)
            .AddColumn(new TableColumn(AuthorsRenderer.Header(" ")).Centered())
            .AddColumn(new TableColumn(AuthorsRenderer.Header("File")))
            .AddColumn(new TableColumn(AuthorsRenderer.Header("Added")).RightAligned())
            .AddColumn(new TableColumn(AuthorsRenderer.Header("Deleted")).RightAligned());

        if (c.Files.Count == 0)
        {
            table.AddRow(new Markup(string.Empty), new Markup(Theme.Dim("No file changes.")), new Markup(string.Empty), new Markup(string.Empty));
            return table;
        }

        foreach (var f in c.Files)
        {
            IRenderable path = f.OldPath is { } old
                ? new Markup($"{Theme.Dim(Format.TruncatePath(old, Math.Max(10, width / 3)))} → {Markup.Escape(f.Path)}")
                : new TextPath(f.Path).RootColor(Theme.Muted).SeparatorColor(Theme.Muted).StemColor(Theme.Muted).LeafColor(Theme.Primary);

            table.AddRow(
                new Markup(Status(f.Kind)),
                path,
                new Markup(f.IsBinary ? Theme.Dim("binary") : Theme.AddedText(f.LinesAdded)),
                new Markup(f.IsBinary ? string.Empty : Theme.DeletedText(f.LinesDeleted)));
        }

        return table;
    }

    private static IRenderable Diff(FileDiff file, int width)
    {
        if (file.IsBinary)
        {
            return new Markup(Theme.Dim("Binary file, no diff shown."));
        }

        // Diff lines are raw repository text: Text/Paragraph never parse markup, so nothing to escape.
        var paragraph = new Paragraph();
        var first = true;
        foreach (var line in file.Lines)
        {
            if (!first)
            {
                paragraph.Append("\n");
            }

            first = false;
            var (prefix, style) = line.Kind switch
            {
                DiffLineKind.Added => ("+", new Style(Theme.Added)),
                DiffLineKind.Deleted => ("-", new Style(Theme.Deleted)),
                DiffLineKind.Hunk => (string.Empty, new Style(Theme.Accent)),
                DiffLineKind.Note => (string.Empty, new Style(Theme.Muted)),
                _ => (" ", Style.Plain),
            };

            var text = prefix + line.Text.Replace("\t", "    ", StringComparison.Ordinal);
            paragraph.Append(Format.Truncate(text, Math.Max(1, width - 1)), style);
        }

        if (file.HiddenLines > 0)
        {
            if (!first)
            {
                paragraph.Append("\n");
            }

            paragraph.Append($"… {Format.Plural(file.HiddenLines, "more line")} not shown", new Style(Theme.Muted));
        }

        return paragraph;
    }

    private static string Status(FileChangeKind kind) => kind switch
    {
        FileChangeKind.Added => Theme.Colored("A", Theme.Added),
        FileChangeKind.Deleted => Theme.Colored("D", Theme.Deleted),
        FileChangeKind.Renamed => Theme.Colored("R", Theme.Accent),
        FileChangeKind.Copied => Theme.Colored("C", Theme.Accent),
        FileChangeKind.Modified => Theme.Colored("M", Theme.Warning),
        _ => Theme.Dim("?"),
    };

    private static string Person(string name, string email) =>
        $"{Markup.Escape(name)} {Theme.Dim($"<{email}>")}";

    private static string DateTime(DateTimeOffset when) =>
        when.ToString("yyyy-MM-dd HH:mm zzz", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// <c>gitpulse commit --raw</c>: the commit as JSON (stats per file, no diff text).
/// Colored with <see cref="JsonText"/> in a terminal; plain, unwrapped JSON when redirected,
/// so other tools can read it.
/// </summary>
internal static class CommitJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(CommitDetail c) => JsonSerializer.Serialize(
        new
        {
            c.Sha,
            c.ShortSha,
            Author = new { Name = c.AuthorName, Email = c.AuthorEmail, Date = c.AuthorWhen },
            Committer = new { Name = c.CommitterName, Email = c.CommitterEmail, Date = c.CommitterWhen },
            c.Subject,
            c.Body,
            c.Parents,
            c.IsMerge,
            Url = c.WebUrl,
            Stats = new { Files = c.Files.Count, Added = c.LinesAdded, Deleted = c.LinesDeleted },
            Files = c.Files.Select(f => new
            {
                f.Path,
                f.OldPath,
                Status = f.Kind,
                Added = f.LinesAdded,
                Deleted = f.LinesDeleted,
                Binary = f.IsBinary,
            }),
        },
        Options);

    public static void Write(IAnsiConsole console, CommitDetail commit)
    {
        var json = Serialize(commit);
        if (console.Profile.Out.IsTerminal && console.Profile.Capabilities.Ansi)
        {
            console.Write(new JsonText(json)
                .MemberColor(Theme.Primary)
                .StringColor(Theme.Added)
                .NumberColor(Theme.Warning)
                .BooleanColor(Theme.Accent)
                .NullColor(Theme.Muted)
                .BracesColor(Theme.Muted)
                .BracketColor(Theme.Muted)
                .ColonColor(Theme.Muted)
                .CommaColor(Theme.Muted));
            console.WriteLine();
        }
        else
        {
            // Bypass Spectre's wrapping: a long string wrapped at the console width is invalid JSON.
            console.Profile.Out.Writer.WriteLine(json);
            console.Profile.Out.Writer.Flush();
        }
    }
}
