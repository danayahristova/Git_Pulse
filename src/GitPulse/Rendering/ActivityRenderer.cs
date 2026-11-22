using System.Globalization;
using GitPulse.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace GitPulse.Rendering;

/// <summary>
/// The <c>heatmap</c> screen: the activity grid, then commits by weekday and by time of day
/// (side by side in <see cref="Columns"/> at 100+ columns, stacked below that).
/// </summary>
internal static class ActivityRenderer
{
    /// <summary>Panel border + padding on each side.</summary>
    private const int PanelChrome = 4;

    public static IRenderable Render(RepoStats stats, int width)
    {
        var widthClass = Breakpoints.Of(width);
        if (widthClass == WidthClass.TooNarrow)
        {
            return Widgets.TooNarrow(width);
        }

        var weeks = Math.Min(HeatmapGrid.WeeksThatFit(width - PanelChrome), stats.Heatmap.Weeks.Count);
        var heatmap = stats.Heatmap.TakeLastWeeks(weeks);

        var heatPanel = Widgets.Panel(
            new Rows(new HeatmapGrid(heatmap), new Text(string.Empty), new Markup(HeatmapSummary(heatmap))),
            $"Activity · last {Format.Plural(heatmap.Weeks.Count, "week")}");

        var byWeekday = ChartPanel(WeekdayChart(stats.Activity), "Commits by weekday");
        var byHour = ChartPanel(HourChart(stats.Activity), "Commits by time of day");

        IRenderable charts = widthClass >= WidthClass.Medium
            ? new Columns(byWeekday, byHour) { Expand = false }
            : new Rows(byWeekday, byHour);

        return new Rows(heatPanel, charts);
    }

    internal static string HeatmapSummary(Heatmap heatmap)
    {
        if (heatmap.BusiestDay is not { } busiest)
        {
            return Theme.Dim("No commits in this period.");
        }

        return $"{Theme.Bold(Format.Plural(heatmap.TotalCommits, "commit"))} on " +
               $"{Theme.Bold(Format.Plural(heatmap.ActiveDays, "day"))} · " +
               Theme.Dim($"busiest day {Format.Date(busiest.Date)} ({Format.Plural(busiest.Commits, "commit")})");
    }

    private static Panel ChartPanel(IRenderable chart, string header) =>
        new Panel(chart)
            .Header($" {Theme.Title(header)} ")
            .BorderStyle(Theme.Border);

    private static BarChart WeekdayChart(ActivityStats activity)
    {
        var chart = NewChart();
        for (var i = 0; i < 7; i++)
        {
            var day = ActivityStats.WeekdayOrder[i];
            var weekend = day is DayOfWeek.Saturday or DayOfWeek.Sunday;
            chart.AddItem(
                CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(day),
                activity.CommitsPerWeekday[i],
                weekend ? Theme.Accent : Theme.Primary);
        }

        return chart;
    }

    /// <summary>Hours in 3-hour buckets ("09-11"), so the chart stays short.</summary>
    private static BarChart HourChart(ActivityStats activity)
    {
        var chart = NewChart();
        for (var start = 0; start < 24; start += 3)
        {
            var commits = activity.CommitsPerHour.Skip(start).Take(3).Sum();
            var working = start is >= 9 and < 18;
            chart.AddItem($"{start:00}-{start + 2:00}", commits, working ? Theme.Primary : Theme.Accent);
        }

        return chart;
    }

    private static BarChart NewChart() =>
        new BarChart()
            .Width(44)
            .UseValueFormatter(v => Format.Number((long)v));
}
