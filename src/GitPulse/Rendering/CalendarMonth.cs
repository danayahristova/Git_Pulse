using System.Globalization;
using GitPulse.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace GitPulse.Rendering;

/// <summary>
/// A <see cref="Spectre.Console.Calendar"/> of the month of the last commit, with commit days highlighted,
/// plus a one-line summary (highlights are invisible without colors, so the numbers matter).
/// </summary>
internal sealed class CalendarMonth
{
    /// <summary>Invariant names, but weeks start on Monday like the heatmap.</summary>
    private static readonly CultureInfo MondayFirst = CreateCulture();

    private CalendarMonth(int year, int month, IReadOnlyList<(int Day, int Commits)> days)
    {
        Year = year;
        Month = month;
        Days = days;
    }

    public int Year { get; }

    public int Month { get; }

    /// <summary>Days of the month with at least one commit.</summary>
    public IReadOnlyList<(int Day, int Commits)> Days { get; }

    public string Title => $"{MondayFirst.DateTimeFormat.GetMonthName(Month)} {Year}";

    /// <summary>
    /// Columns needed so a day with commits ("30*") never wraps: 7 × (3 + 2 padding),
    /// the table's separators and edges, plus the panel border and padding.
    /// </summary>
    public const int PanelWidth = 47;

    /// <summary>
    /// Panel border + top edge + day names + separator + one row per week + bottom edge,
    /// then a blank line and the summary.
    /// </summary>
    public int PanelHeight
    {
        get
        {
            var first = new DateOnly(Year, Month, 1);
            var offset = ActivityStats.WeekdayIndex(first.DayOfWeek);
            var weeks = (int)Math.Ceiling((offset + DateTime.DaysInMonth(Year, Month)) / 7.0);
            return 2 + 4 + weeks + 2;
        }
    }

    public static CalendarMonth For(RepoStats stats)
    {
        var last = stats.Summary.LastCommit.DateTime;
        var days = stats.Activity.CommitsPerDay
            .Where(kv => kv.Key.Year == last.Year && kv.Key.Month == last.Month && kv.Value > 0)
            .OrderBy(kv => kv.Key)
            .Select(kv => (kv.Key.Day, kv.Value))
            .ToList();
        return new CalendarMonth(last.Year, last.Month, days);
    }

    public IRenderable Render()
    {
        // No vertical lines and two-letter day names, so "30*" (a day with commits) fits in a
        // third of a 120-column screen. The "*" marks commit days even without colors.
        var calendar = new Spectre.Console.Calendar(Year, Month)
        {
            Culture = MondayFirst,
            ShowHeader = false,
            Border = TableBorder.Simple,
        }
            .BorderStyle(Theme.Border)
            .HighlightStyle(new Style(Theme.Background, Theme.Accent, Decoration.Bold));

        foreach (var (day, _) in Days)
        {
            calendar.AddCalendarEvent(Year, Month, day);
        }

        var commits = Days.Sum(d => d.Commits);
        var summary = Days.Count == 0
            ? Theme.Dim("No commits this month.")
            : $"{Theme.Bold(Format.Plural(commits, "commit"))} on {Theme.Bold(Format.Plural(Days.Count, "day"))}";

        return new Rows(calendar, new Text(string.Empty), new Markup(summary));
    }

    private static CultureInfo CreateCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.DateTimeFormat.FirstDayOfWeek = DayOfWeek.Monday;
        culture.DateTimeFormat.AbbreviatedDayNames = ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];
        return culture;
    }
}
