namespace GitPulse.Models;

/// <summary>
/// When commits happen. Days, weekdays and hours use each commit's own time zone
/// (the author's local time), which is what "when do people work" means.
/// </summary>
internal sealed record ActivityStats(
    IReadOnlyDictionary<DateOnly, int> CommitsPerDay,
    IReadOnlyList<int> CommitsPerWeekday,
    IReadOnlyList<int> CommitsPerHour)
{
    /// <summary>Index 0 = Monday … 6 = Sunday.</summary>
    public static readonly IReadOnlyList<DayOfWeek> WeekdayOrder =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
    ];

    public static int WeekdayIndex(DayOfWeek day) => ((int)day + 6) % 7;

    public static ActivityStats Empty { get; } = new(new Dictionary<DateOnly, int>(), new int[7], new int[24]);
}

/// <summary>One day in the heatmap. <see cref="Level"/> is 0 (no commits) to 4 (busiest).</summary>
internal sealed record HeatmapCell(DateOnly Date, int Commits, int Level, bool InRange);

/// <summary>
/// A GitHub-style grid: one column per week (Monday first), seven rows per column.
/// Days after <see cref="End"/> in the last week are padding (<see cref="HeatmapCell.InRange"/> false).
/// </summary>
internal sealed record Heatmap(
    DateOnly Start,
    DateOnly End,
    IReadOnlyList<IReadOnlyList<HeatmapCell>> Weeks,
    int MaxCommits)
{
    public const int Levels = 5;

    public int TotalCommits => Weeks.SelectMany(w => w).Where(c => c.InRange).Sum(c => c.Commits);

    public int ActiveDays => Weeks.SelectMany(w => w).Count(c => c.InRange && c.Commits > 0);

    /// <summary>The busiest day, or null when there are no commits in range.</summary>
    public HeatmapCell? BusiestDay => Weeks.SelectMany(w => w)
        .Where(c => c.InRange && c.Commits > 0)
        .OrderByDescending(c => c.Commits).ThenByDescending(c => c.Date)
        .FirstOrDefault();

    /// <summary>The last <paramref name="weeks"/> weeks (all of them when there are fewer).</summary>
    public Heatmap TakeLastWeeks(int weeks)
    {
        if (weeks >= Weeks.Count)
        {
            return this;
        }

        var kept = Weeks.Skip(Weeks.Count - weeks).ToList();
        return this with { Start = kept[0][0].Date, Weeks = kept };
    }

    public static Heatmap Empty(DateOnly end) => new(end, end, [], 0);
}

/// <summary>A folder or file in the churn tree. Folder totals include everything below them.</summary>
internal sealed record ChurnNode(
    string Name,
    string Path,
    bool IsFile,
    int Churn,
    int Files,
    IReadOnlyList<ChurnNode> Children,
    int HiddenChildren = 0,
    int HiddenChurn = 0);
