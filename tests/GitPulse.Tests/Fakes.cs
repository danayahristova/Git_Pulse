using GitPulse.Analysis;
using GitPulse.Models;

namespace GitPulse.Tests;

/// <summary>Builders for fixed, fake data used by analysis and rendering tests.</summary>
internal static class Fakes
{
    private static int _sha;

    public static DateTimeOffset Day(int month, int day) =>
        new(2026, month, day, 12, 0, 0, TimeSpan.Zero);

    public static CommitRecord Commit(
        string author,
        DateTimeOffset when,
        params (string Path, int Added, int Deleted)[] files) =>
        Commit(author, $"{author.Split(' ')[0].ToLowerInvariant()}@example.com", when, files);

    public static CommitRecord Commit(
        string author,
        string email,
        DateTimeOffset when,
        params (string Path, int Added, int Deleted)[] files) =>
        new(
            Sha: (++_sha).ToString("x40"),
            AuthorName: author,
            AuthorEmail: email,
            When: when,
            MessageShort: $"Change by {author}",
            IsMerge: false,
            Files: files.Select(f => new FileChange(f.Path, f.Added, f.Deleted)).ToList());

    /// <summary>
    /// A small, fixed repository used by the renderer snapshot tests. Change it and the
    /// snapshots must be reviewed and updated.
    /// </summary>
    public static RepoStats SampleStats()
    {
        IReadOnlyList<FileChurn> hotspots =
        [
            new("src/Core/Engine/Scheduler.cs", 188, 9012, 7310),
            new("src/Core/Engine/RenderPipeline.cs", 140, 6200, 4100),
            new("tests/Core.Tests/SchedulerTests.cs", 58, 3100, 1500),
            new("src/Cli/Commands/Very/Deeply/Nested/Folder/Structure/OverviewCommand.cs", 63, 2100, 900),
            new("README.md", 97, 1200, 640),
            new("[docs]/guide.md", 12, 300, 20),
        ];

        var activity = SampleActivity();
        return SampleStatsCore(hotspots) with
        {
            Activity = activity,
            Heatmap = HeatmapBuilder.Build(activity.CommitsPerDay, new DateOnly(2026, 11, 18)),
            FileTree = FileTreeBuilder.Build("sample-repo", hotspots),
        };
    }

    /// <summary>
    /// A year of fixed, made-up activity ending 2026-11-18: quieter weekends, some empty days.
    /// Weekday and hour totals add up to the sample's 1,287 commits.
    /// </summary>
    public static ActivityStats SampleActivity()
    {
        var end = new DateOnly(2026, 11, 18);
        var perDay = new Dictionary<DateOnly, int>();
        for (var i = 0; i < 364; i++)
        {
            var day = end.AddDays(-i);
            var commits = (i * 7 + i / 5) % 9;
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                commits /= 3;
            }

            if (i % 13 == 0 && i > 0)
            {
                commits = 0;
            }

            if (commits > 0)
            {
                perDay[day] = commits;
            }
        }

        return new ActivityStats(
            perDay,
            [220, 251, 240, 233, 198, 80, 65],
            [5, 2, 1, 0, 0, 1, 4, 12, 45, 98, 120, 110, 70, 95, 118, 125, 112, 99, 80, 60, 48, 33, 30, 19]);
    }

    private static RepoStats SampleStatsCore(IReadOnlyList<FileChurn> hotspots) => new(
        new RepoSummary(
            Name: "sample-repo",
            Commits: 1287,
            Authors: 6,
            FirstCommit: Day(1, 3),
            LastCommit: Day(11, 18),
            Branches: 4,
            Tags: 12,
            Files: 342,
            Since: null),
        Authors:
        [
            new("Ada Lovelace", "ada@example.com", 612, 48210, 20133, Day(1, 3), Day(11, 18)),
            new("Linus Torvalds", "linus@example.com", 301, 20444, 9120, Day(2, 14), Day(11, 2)),
            new("Grace Hopper", "grace@example.com", 214, 9876, 4410, Day(3, 1), Day(10, 30)),
            new("Margaret Hamilton With A Very Long Name", "margaret@example.com", 98, 3310, 1205, Day(4, 9), Day(9, 12)),
            new("bot[dependabot]", "bot@example.com", 50, 410, 388, Day(5, 1), Day(11, 17)),
            new("Ken Thompson", "ken@example.com", 12, 120, 30, Day(6, 6), Day(6, 20)),
        ],
        Hotspots: hotspots,
        Languages:
        [
            new("C#", 812_000, 61.2),
            new("Markdown", 210_000, 15.8),
            new("JSON", 120_000, 9.0),
            new("YAML", 90_000, 6.8),
            new("Shell", 52_000, 3.9),
            new("Other", 43_000, 3.3),
        ],
        Activity: ActivityStats.Empty,
        Heatmap: Heatmap.Empty(new DateOnly(2026, 11, 18)),
        FileTree: new ChurnNode("sample-repo", "", false, 0, 0, []));
}
