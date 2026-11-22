using GitPulse.Models;

namespace GitPulse.Analysis;

/// <summary>Commits per day, weekday and hour. Pure function of the commit list.</summary>
internal static class ActivityAnalyzer
{
    public static ActivityStats Compute(IEnumerable<CommitRecord> commits)
    {
        var perDay = new Dictionary<DateOnly, int>();
        var perWeekday = new int[7];
        var perHour = new int[24];

        foreach (var commit in commits)
        {
            // DateTime of a DateTimeOffset is the clock time in the commit's own offset.
            var local = commit.When.DateTime;
            var day = DateOnly.FromDateTime(local);
            perDay[day] = perDay.GetValueOrDefault(day) + 1;
            perWeekday[ActivityStats.WeekdayIndex(local.DayOfWeek)]++;
            perHour[local.Hour]++;
        }

        return new ActivityStats(perDay, perWeekday, perHour);
    }
}

/// <summary>Builds the GitHub-style weeks × weekdays grid.</summary>
internal static class HeatmapBuilder
{
    public const int DefaultWeeks = 52;

    /// <param name="end">The last day shown (usually the last commit's day).</param>
    /// <param name="weeks">Number of week columns; the last one contains <paramref name="end"/>.</param>
    public static Heatmap Build(IReadOnlyDictionary<DateOnly, int> commitsPerDay, DateOnly end, int weeks = DefaultWeeks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(weeks, 1);

        var lastMonday = end.AddDays(-ActivityStats.WeekdayIndex(end.DayOfWeek));
        var start = lastMonday.AddDays(-7 * (weeks - 1));

        var max = commitsPerDay
            .Where(kv => kv.Key >= start && kv.Key <= end)
            .Select(kv => kv.Value)
            .DefaultIfEmpty(0)
            .Max();

        var columns = new List<IReadOnlyList<HeatmapCell>>(weeks);
        for (var w = 0; w < weeks; w++)
        {
            var column = new List<HeatmapCell>(7);
            for (var d = 0; d < 7; d++)
            {
                var date = start.AddDays(7 * w + d);
                var inRange = date <= end;
                var count = inRange ? commitsPerDay.GetValueOrDefault(date) : 0;
                column.Add(new HeatmapCell(date, count, LevelOf(count, max), inRange));
            }

            columns.Add(column);
        }

        return new Heatmap(start, end, columns, max);
    }

    /// <summary>0 for no commits; otherwise 1–4, where 4 is the busiest day.</summary>
    internal static int LevelOf(int commits, int max) =>
        commits <= 0 || max <= 0
            ? 0
            : Math.Clamp((int)Math.Ceiling((Heatmap.Levels - 1) * (double)commits / max), 1, Heatmap.Levels - 1);
}

/// <summary>Folder tree of churn, for the hotspots view.</summary>
internal static class FileTreeBuilder
{
    /// <param name="maxDepth">Levels shown below the root; deeper content is summarized in its folder.</param>
    /// <param name="maxChildren">Children shown per folder (largest churn first); the rest are summarized.</param>
    public static ChurnNode Build(string rootName, IEnumerable<FileChurn> files, int maxDepth = 4, int maxChildren = 5)
    {
        var root = new MutableNode(rootName, string.Empty, isFile: false);
        foreach (var file in files)
        {
            var parts = file.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var node = root;
            for (var i = 0; i < parts.Length; i++)
            {
                var isFile = i == parts.Length - 1;
                var path = string.Join('/', parts.Take(i + 1));
                if (!node.Children.TryGetValue(parts[i], out var child))
                {
                    child = new MutableNode(parts[i], path, isFile);
                    node.Children[parts[i]] = child;
                }

                node = child;
            }

            node.OwnChurn += file.Churn;
        }

        root.ComputeTotals();
        return Shape(root, depth: 0, maxDepth, maxChildren, isRoot: true);
    }

    private static ChurnNode Shape(MutableNode node, int depth, int maxDepth, int maxChildren, bool isRoot)
    {
        // Collapse "src" → "Core" → "Engine" into one "src/Core/Engine" node (never the root).
        var name = node.Name;
        while (!isRoot && !node.IsFile && node.Children.Count == 1 && !node.Children.Values.First().IsFile)
        {
            node = node.Children.Values.First();
            name += "/" + node.Name;
        }

        var ordered = node.Children.Values
            .OrderByDescending(c => c.Churn)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();

        var shownCount = depth >= maxDepth ? 0 : Math.Min(maxChildren, ordered.Count);
        var shown = ordered.Take(shownCount)
            .Select(c => Shape(c, depth + 1, maxDepth, maxChildren, isRoot: false))
            .ToList();
        var hidden = ordered.Skip(shownCount).ToList();

        return new ChurnNode(
            name,
            node.Path,
            node.IsFile,
            node.Churn,
            node.Files,
            shown,
            hidden.Count,
            hidden.Sum(c => c.Churn));
    }

    private sealed class MutableNode(string name, string path, bool isFile)
    {
        public string Name { get; } = name;

        public string Path { get; } = path;

        public bool IsFile { get; } = isFile;

        public Dictionary<string, MutableNode> Children { get; } = new(StringComparer.Ordinal);

        public int OwnChurn { get; set; }

        public int Churn { get; private set; }

        public int Files { get; private set; }

        public void ComputeTotals()
        {
            foreach (var child in Children.Values)
            {
                child.ComputeTotals();
            }

            Churn = OwnChurn + Children.Values.Sum(c => c.Churn);
            Files = (IsFile ? 1 : 0) + Children.Values.Sum(c => c.Files);
        }
    }
}
