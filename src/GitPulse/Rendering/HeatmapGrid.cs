using System.Globalization;
using GitPulse.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace GitPulse.Rendering;

/// <summary>
/// The weeks × weekdays activity grid. With colors it draws a <see cref="Canvas"/> (one 2-column
/// pixel per day); without colors (redirected output, NO_COLOR) it uses shade glyphs instead,
/// because a Canvas only paints background colors and would come out blank.
/// </summary>
internal sealed class HeatmapGrid(Heatmap heatmap) : Renderable
{
    public const int LabelWidth = 4; // "Mon "
    public const int CellWidth = 2;

    private static readonly string[] DayLabels = ["Mon", "", "Wed", "", "Fri", "", ""];

    /// <summary>How many week columns fit in <paramref name="width"/> columns.</summary>
    public static int WeeksThatFit(int width) => Math.Max(1, (width - LabelWidth) / CellWidth);

    private int Width => LabelWidth + heatmap.Weeks.Count * CellWidth;

    protected override Measurement Measure(RenderOptions options, int maxWidth)
    {
        var width = Math.Min(Width, maxWidth);
        return new Measurement(width, width);
    }

    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        // Never draw wider than we were given: drop the oldest weeks instead.
        var shown = heatmap.TakeLastWeeks(WeeksThatFit(maxWidth));
        var muted = new Style(Theme.Muted);
        // Colors only reach the screen when ANSI output is on (not when redirected or in TestConsole).
        var useColor = options.Ansi && options.ColorSystem != ColorSystem.NoColors;

        var segments = new List<Segment> { new(MonthHeader(shown), muted), Segment.LineBreak };

        var rows = useColor ? CanvasRows(shown, options) : GlyphRows(shown);
        for (var day = 0; day < 7; day++)
        {
            segments.Add(new Segment(DayLabels[day].PadRight(LabelWidth), muted));
            segments.AddRange(rows[day]);
            segments.Add(Segment.LineBreak);
        }

        segments.Add(Segment.LineBreak);
        segments.Add(new Segment(new string(' ', LabelWidth) + "Less ", muted));
        for (var level = 0; level < Heatmap.Levels; level++)
        {
            segments.Add(Cell(level, useColor));
        }

        segments.Add(new Segment("More", muted));
        segments.Add(Segment.LineBreak);
        return segments;
    }

    /// <summary>Three-letter month names above the first week of each month, without overlaps.</summary>
    internal static string MonthHeader(Heatmap shown)
    {
        var chars = new string(' ', LabelWidth + shown.Weeks.Count * CellWidth).ToCharArray();
        var nextFree = 0;
        int? previousMonth = null;

        for (var w = 0; w < shown.Weeks.Count; w++)
        {
            var month = shown.Weeks[w][0].Date.Month;
            if (month == previousMonth)
            {
                continue;
            }

            previousMonth = month;
            var name = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(month);
            var at = LabelWidth + w * CellWidth;
            if (at >= nextFree && at + name.Length <= chars.Length)
            {
                name.CopyTo(0, chars, at, name.Length);
                nextFree = at + name.Length + 1;
            }
        }

        return new string(chars).TrimEnd();
    }

    /// <summary>
    /// Canvas pixels are half-block characters (1 column wide, half a line tall), so each day is
    /// a 2×2 block of pixels: <see cref="CellWidth"/> columns by one line.
    /// </summary>
    private static List<List<Segment>> CanvasRows(Heatmap shown, RenderOptions options)
    {
        var canvas = new Canvas(shown.Weeks.Count * CellWidth, 7 * 2) { Scale = false };
        for (var w = 0; w < shown.Weeks.Count; w++)
        {
            for (var d = 0; d < 7; d++)
            {
                var cell = shown.Weeks[w][d];
                if (!cell.InRange)
                {
                    continue;
                }

                var color = Theme.HeatLevels[cell.Level];
                for (var dx = 0; dx < CellWidth; dx++)
                {
                    canvas.SetPixel(w * CellWidth + dx, d * 2, color);
                    canvas.SetPixel(w * CellWidth + dx, d * 2 + 1, color);
                }
            }
        }

        var lines = Segment.SplitLines(((IRenderable)canvas).Render(options, shown.Weeks.Count * CellWidth));
        return lines.Select(l => l.ToList()).ToList();
    }

    private static List<List<Segment>> GlyphRows(Heatmap shown)
    {
        var rows = new List<List<Segment>>();
        for (var d = 0; d < 7; d++)
        {
            var row = new List<Segment>();
            foreach (var week in shown.Weeks)
            {
                var cell = week[d];
                row.Add(cell.InRange ? Cell(cell.Level, useColor: false) : new Segment(new string(' ', CellWidth)));
            }

            rows.Add(row);
        }

        return rows;
    }

    private static Segment Cell(int level, bool useColor) => useColor
        ? new Segment(new string(' ', CellWidth), new Style(background: Theme.HeatLevels[level]))
        : new Segment(Theme.HeatGlyphs[level].PadRight(CellWidth), new Style(Theme.HeatLevels[level]));
}
