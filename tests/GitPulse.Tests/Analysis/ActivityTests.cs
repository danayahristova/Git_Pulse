using GitPulse.Analysis;
using GitPulse.Models;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Analysis;

public sealed class ActivityAnalyzerTests
{
    [Fact]
    public void Counts_commits_per_day_weekday_and_hour_in_the_commits_own_time_zone()
    {
        var plus2 = TimeSpan.FromHours(2);
        var commits = new[]
        {
            // Monday 2026-01-05 23:30 at +02:00 (that's 21:30 UTC: still Monday either way)
            Commit("A", new DateTimeOffset(2026, 1, 5, 23, 30, 0, plus2)),
            // Tuesday 2026-01-06 00:15 at +02:00, which is still Monday 22:15 in UTC
            Commit("A", new DateTimeOffset(2026, 1, 6, 0, 15, 0, plus2)),
            Commit("B", new DateTimeOffset(2026, 1, 6, 9, 0, 0, TimeSpan.Zero)),
            Commit("B", new DateTimeOffset(2026, 1, 11, 14, 0, 0, TimeSpan.Zero)), // Sunday
        };

        var activity = ActivityAnalyzer.Compute(commits);

        Assert.Equal(1, activity.CommitsPerDay[new DateOnly(2026, 1, 5)]);
        Assert.Equal(2, activity.CommitsPerDay[new DateOnly(2026, 1, 6)]);
        Assert.Equal([1, 2, 0, 0, 0, 0, 1], activity.CommitsPerWeekday);
        Assert.Equal(1, activity.CommitsPerHour[23]);
        Assert.Equal(1, activity.CommitsPerHour[0]);
        Assert.Equal(1, activity.CommitsPerHour[9]);
        Assert.Equal(1, activity.CommitsPerHour[14]);
        Assert.Equal(4, activity.CommitsPerHour.Sum());
    }

    [Fact]
    public void Empty_history_gives_zeroes()
    {
        var activity = ActivityAnalyzer.Compute([]);

        Assert.Empty(activity.CommitsPerDay);
        Assert.Equal(7, activity.CommitsPerWeekday.Count);
        Assert.Equal(24, activity.CommitsPerHour.Count);
    }
}

public sealed class HeatmapBuilderTests
{
    // 2026-11-18 is a Wednesday.
    private static readonly DateOnly End = new(2026, 11, 18);

    [Fact]
    public void Columns_are_weeks_starting_on_monday_and_end_with_the_end_date()
    {
        var heatmap = HeatmapBuilder.Build(new Dictionary<DateOnly, int>(), End, weeks: 3);

        Assert.Equal(3, heatmap.Weeks.Count);
        Assert.All(heatmap.Weeks, w => Assert.Equal(7, w.Count));
        Assert.Equal(new DateOnly(2026, 11, 2), heatmap.Start);
        Assert.Equal(DayOfWeek.Monday, heatmap.Weeks[0][0].Date.DayOfWeek);

        var lastWeek = heatmap.Weeks[^1];
        Assert.Equal(End, lastWeek[2].Date);
        Assert.True(lastWeek[2].InRange);
        Assert.False(lastWeek[3].InRange); // Thursday is after the end date
        Assert.Equal(17, heatmap.Weeks.SelectMany(w => w).Count(c => c.InRange)); // 14 + Mon..Wed
    }

    [Fact]
    public void Levels_scale_from_one_to_four_against_the_busiest_day()
    {
        var days = new Dictionary<DateOnly, int>
        {
            [End] = 10,
            [End.AddDays(-1)] = 1,
            [End.AddDays(-2)] = 5,
            [End.AddDays(-3)] = 6,
        };

        var heatmap = HeatmapBuilder.Build(days, End, weeks: 2);
        int LevelOf(DateOnly d) => heatmap.Weeks.SelectMany(w => w).Single(c => c.Date == d).Level;

        Assert.Equal(10, heatmap.MaxCommits);
        Assert.Equal(4, LevelOf(End));
        Assert.Equal(1, LevelOf(End.AddDays(-1)));
        Assert.Equal(2, LevelOf(End.AddDays(-2)));
        Assert.Equal(3, LevelOf(End.AddDays(-3)));
        Assert.Equal(0, LevelOf(End.AddDays(-4)));
    }

    [Fact]
    public void Commits_outside_the_window_are_ignored()
    {
        var days = new Dictionary<DateOnly, int> { [End.AddDays(-100)] = 50, [End] = 2 };

        var heatmap = HeatmapBuilder.Build(days, End, weeks: 2);

        Assert.Equal(2, heatmap.MaxCommits);
        Assert.Equal(2, heatmap.TotalCommits);
        Assert.Equal(1, heatmap.ActiveDays);
        Assert.Equal(End, heatmap.BusiestDay!.Date);
    }

    [Fact]
    public void Take_last_weeks_keeps_the_most_recent_columns()
    {
        var heatmap = HeatmapBuilder.Build(new Dictionary<DateOnly, int>(), End, weeks: 52);

        var last10 = heatmap.TakeLastWeeks(10);

        Assert.Equal(10, last10.Weeks.Count);
        Assert.Equal(heatmap.Weeks[^1], last10.Weeks[^1]);
        Assert.Equal(last10.Weeks[0][0].Date, last10.Start);
        Assert.Same(heatmap, heatmap.TakeLastWeeks(60));
    }
}

public sealed class FileTreeBuilderTests
{
    private static FileChurn F(string path, int churn) => new(path, 1, churn, 0);

    [Fact]
    public void Sums_churn_up_the_folders_and_sorts_children_by_churn()
    {
        var root = FileTreeBuilder.Build("repo",
        [
            F("src/a.cs", 10), F("src/b.cs", 30), F("docs/x.md", 5), F("README.md", 20),
        ]);

        Assert.Equal("repo", root.Name);
        Assert.Equal(65, root.Churn);
        Assert.Equal(4, root.Files);
        Assert.Equal(["src", "README.md", "docs"], root.Children.Select(c => c.Name));
        var src = root.Children[0];
        Assert.False(src.IsFile);
        Assert.Equal(40, src.Churn);
        Assert.Equal(["b.cs", "a.cs"], src.Children.Select(c => c.Name));
        Assert.True(src.Children[0].IsFile);
        Assert.Equal("src/b.cs", src.Children[0].Path);
    }

    [Fact]
    public void Collapses_chains_of_single_folders()
    {
        var root = FileTreeBuilder.Build("repo", [F("src/Core/Engine/a.cs", 1), F("src/Core/Engine/b.cs", 2)]);

        var engine = Assert.Single(root.Children);
        Assert.Equal("src/Core/Engine", engine.Name);
        Assert.Equal("src/Core/Engine", engine.Path);
        Assert.Equal(2, engine.Children.Count);
    }

    [Fact]
    public void Keeps_the_top_children_and_counts_the_rest()
    {
        var files = Enumerable.Range(1, 8).Select(i => F($"f{i}.cs", i)).ToArray();

        var root = FileTreeBuilder.Build("repo", files, maxChildren: 3);

        Assert.Equal(["f8.cs", "f7.cs", "f6.cs"], root.Children.Select(c => c.Name));
        Assert.Equal(5, root.HiddenChildren);
        Assert.Equal(1 + 2 + 3 + 4 + 5, root.HiddenChurn);
    }

    [Fact]
    public void Stops_at_max_depth()
    {
        var root = FileTreeBuilder.Build("repo", [F("a/x.cs", 1), F("a/b/y.cs", 1), F("a/b/c/z.cs", 1), F("q.cs", 1)], maxDepth: 2);

        var a = root.Children.Single(c => c.Name == "a");
        var b = a.Children.Single(c => c.Name == "b");
        Assert.Empty(b.Children);
        Assert.Equal(2, b.HiddenChildren); // c/ and y.cs
        Assert.Equal(2, b.Churn);
    }

    [Fact]
    public void Empty_input_gives_an_empty_root()
    {
        var root = FileTreeBuilder.Build("repo", []);

        Assert.Equal(0, root.Churn);
        Assert.Empty(root.Children);
    }
}
