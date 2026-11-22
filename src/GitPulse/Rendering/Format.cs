using System.Globalization;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace GitPulse.Rendering;

/// <summary>Culture-independent formatting, so output (and snapshots) look the same everywhere.</summary>
internal static class Format
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Number(long value) => value.ToString("N0", Invariant);

    public static string Percent(double value) => value.ToString("0.0", Invariant) + "%";

    /// <summary>The date in the commit's own time zone, as yyyy-MM-dd.</summary>
    public static string Date(DateTimeOffset when) => when.ToString("yyyy-MM-dd", Invariant);

    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", Invariant);

    /// <summary>Shortens <paramref name="text"/> to <paramref name="max"/> characters, ending in an ellipsis.</summary>
    public static string Truncate(string text, int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(max, 1);
        return text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 1), "…");
    }

    public static string Plural(long count, string singular, string? plural = null) =>
        $"{Number(count)} {(count == 1 ? singular : plural ?? singular + "s")}";

    /// <summary>Length of the history in whole days, at least 1.</summary>
    public static int SpanDays(DateTimeOffset first, DateTimeOffset last) =>
        Math.Max(1, (int)Math.Ceiling((last - first).TotalDays));
}

/// <summary>Terminal width breakpoints shared by all renderers.</summary>
internal static class Breakpoints
{
    /// <summary>Below this, side-by-side layouts switch to a stacked compact layout.</summary>
    public const int Compact = 80;

    /// <summary>Below this, nothing useful fits; a "terminal too narrow" message is shown instead.</summary>
    public const int Minimum = 40;

    public static bool IsTooNarrow(int width) => width < Minimum;

    public static bool IsCompact(int width) => width < Compact;
}

/// <summary>Shared building blocks used by several renderers.</summary>
internal static class Widgets
{
    public static Panel Panel(IRenderable content, string header) =>
        new Panel(content)
            .Header($" {Theme.Title(header)} ")
            .BorderStyle(Theme.Border)
            .Expand();

    /// <summary>Shown instead of a screen when the terminal is narrower than <see cref="Breakpoints.Minimum"/>.</summary>
    public static IRenderable TooNarrow(int width) =>
        new Panel(new Markup(
                $"{Theme.Colored("Terminal too narrow", Theme.Warning)}\n" +
                $"Needs {Breakpoints.Minimum}+ columns, has {width}. " +
                $"{Theme.Dim("Widen the window and try again.")}"))
            .BorderStyle(new Style(Theme.Warning));
}
