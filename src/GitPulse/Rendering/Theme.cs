using Spectre.Console;

namespace GitPulse.Rendering;

/// <summary>
/// The one place colors are defined. Renderers use these instead of hardcoded markup colors.
/// The helpers escape their text, so pass raw (unescaped) repo strings.
/// </summary>
internal static class Theme
{
    public static readonly Color Primary = Color.SteelBlue1;
    public static readonly Color Accent = Color.MediumPurple1;
    public static readonly Color Added = Color.Green3;
    public static readonly Color Deleted = Color.Red3;
    public static readonly Color Muted = Color.Grey;
    public static readonly Color Warning = Color.Gold1;
    public static readonly Color Error = Color.Red1;

    /// <summary>Text color on highlighted (colored-background) cells.</summary>
    public static readonly Color Background = Color.Black;

    /// <summary>Series colors for charts, in order. Wraps around if there are more items.</summary>
    public static readonly IReadOnlyList<Color> Palette =
    [
        Color.SteelBlue1,
        Color.MediumPurple1,
        Color.Aquamarine1,
        Color.Gold1,
        Color.LightSalmon1,
        Color.Grey62,
    ];

    public static Color Series(int index) => Palette[index % Palette.Count];

    /// <summary>Heatmap colors for levels 0 (no commits) to 4 (busiest).</summary>
    public static readonly IReadOnlyList<Color> HeatLevels =
    [
        Color.Grey23,
        Color.DarkGreen,
        Color.Green4,
        Color.Green3,
        Color.Green1,
    ];

    /// <summary>Heatmap glyphs for levels 0–4, used when the terminal has no colors.</summary>
    public static readonly IReadOnlyList<string> HeatGlyphs = ["·", "░", "▒", "▓", "█"];

    /// <summary>Color for a share of the total churn: hot (red) to cold (grey).</summary>
    public static Color Heat(double share) => share switch
    {
        >= 0.25 => Deleted,
        >= 0.10 => Warning,
        >= 0.03 => Primary,
        _ => Muted,
    };

    public static Style Border => new(Muted);

    public static Style PanelHeader => new(Primary, decoration: Decoration.Bold);

    public static Style TableHeader => new(Primary, decoration: Decoration.Bold);

    public static string Bold(string text) => $"[bold]{Markup.Escape(text)}[/]";

    public static string Colored(string text, Color color) => $"[{color.ToMarkup()}]{Markup.Escape(text)}[/]";

    public static string Title(string text) => $"[bold {Primary.ToMarkup()}]{Markup.Escape(text)}[/]";

    public static string Dim(string text) => Colored(text, Muted);

    public static string AddedText(int lines) => Colored("+" + Format.Number(lines), Added);

    public static string DeletedText(int lines) => Colored("-" + Format.Number(lines), Deleted);
}
