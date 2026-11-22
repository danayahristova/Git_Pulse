using GitPulse.Analysis;
using GitPulse.Models;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Analysis;

public sealed class AuthorAnalyzerTests
{
    [Fact]
    public void Groups_by_email_case_insensitively_and_uses_latest_name()
    {
        var commits = new[]
        {
            Commit("Ada L.", "ADA@example.com", Day(1, 1), ("a.cs", 10, 2)),
            Commit("Ada Lovelace", "ada@example.com", Day(3, 1), ("a.cs", 5, 1)),
            Commit("Linus", "linus@example.com", Day(2, 1), ("b.cs", 1, 0)),
        };

        var authors = AuthorAnalyzer.Compute(commits);

        Assert.Equal(2, authors.Count);
        var ada = authors[0];
        Assert.Equal("Ada Lovelace", ada.Name);
        Assert.Equal(2, ada.Commits);
        Assert.Equal(15, ada.LinesAdded);
        Assert.Equal(3, ada.LinesDeleted);
        Assert.Equal(Day(1, 1), ada.FirstCommit);
        Assert.Equal(Day(3, 1), ada.LastCommit);
    }

    [Fact]
    public void Sorts_by_commits_then_lines_then_name()
    {
        var commits = new[]
        {
            Commit("Zed", Day(1, 1), ("a", 1, 0)),
            Commit("Amy", Day(1, 2), ("a", 1, 0)),
            Commit("Bob", Day(1, 3), ("a", 50, 0)),
            Commit("Bob", Day(1, 4), ("a", 1, 0)),
            Commit("Cat", Day(1, 5), ("a", 9, 0)),
        };

        var names = AuthorAnalyzer.Compute(commits).Select(a => a.Name);

        Assert.Equal(["Bob", "Cat", "Amy", "Zed"], names);
    }

    [Fact]
    public void Falls_back_to_name_when_email_is_empty()
    {
        var commits = new[]
        {
            Commit("Anon", "", Day(1, 1)),
            Commit("Anon", "", Day(1, 2)),
            Commit("Other", "", Day(1, 3)),
        };

        var authors = AuthorAnalyzer.Compute(commits);

        Assert.Equal(2, authors.Count);
        Assert.Equal(2, authors.Single(a => a.Name == "Anon").Commits);
    }
}

public sealed class ChurnAnalyzerTests
{
    [Fact]
    public void Sums_churn_per_file_and_sorts_by_churn_descending()
    {
        var commits = new[]
        {
            Commit("A", Day(1, 1), ("hot.cs", 100, 50), ("cold.cs", 1, 0)),
            Commit("B", Day(1, 2), ("hot.cs", 10, 10), ("warm.cs", 30, 5)),
        };

        var churn = ChurnAnalyzer.Compute(commits);

        Assert.Equal(["hot.cs", "warm.cs", "cold.cs"], churn.Select(c => c.Path));
        var hot = churn[0];
        Assert.Equal(2, hot.Commits);
        Assert.Equal(110, hot.LinesAdded);
        Assert.Equal(60, hot.LinesDeleted);
        Assert.Equal(170, hot.Churn);
    }

    [Fact]
    public void Ties_break_on_commit_count_then_path()
    {
        var commits = new[]
        {
            Commit("A", Day(1, 1), ("b.cs", 5, 0), ("a.cs", 5, 0)),
            Commit("A", Day(1, 2), ("c.cs", 2, 0)),
            Commit("A", Day(1, 3), ("c.cs", 3, 0)),
        };

        Assert.Equal(["c.cs", "a.cs", "b.cs"], ChurnAnalyzer.Compute(commits).Select(c => c.Path));
    }
}

public sealed class LanguageDetectorTests
{
    [Theory]
    [InlineData("src/Program.cs", "C#")]
    [InlineData("README.md", "Markdown")]
    [InlineData("web/app.TSX", "TypeScript")]
    [InlineData("build/Dockerfile", "Dockerfile")]
    [InlineData(".github/workflows/ci.yml", "YAML")]
    [InlineData("Directory.Build.props", "MSBuild")]
    public void Detects_language_from_path(string path, string expected)
    {
        Assert.Equal(expected, LanguageDetector.LanguageOf(path));
    }

    [Theory]
    [InlineData("LICENSE")]
    [InlineData("image.png")]
    [InlineData(".gitignore")]
    public void Unknown_files_have_no_language(string path)
    {
        Assert.Null(LanguageDetector.LanguageOf(path));
    }

    [Fact]
    public void Computes_share_by_bytes_ignoring_unknown_files()
    {
        var files = new TrackedFile[]
        {
            new("a.cs", 600), new("b.cs", 150), new("c.md", 250), new("logo.png", 10_000),
        };

        var shares = LanguageDetector.Detect(files);

        Assert.Equal(["C#", "Markdown"], shares.Select(s => s.Language));
        Assert.Equal(75.0, shares[0].Percent, precision: 1);
        Assert.Equal(25.0, shares[1].Percent, precision: 1);
    }

    [Fact]
    public void Groups_the_tail_into_other()
    {
        var files = new TrackedFile[]
        {
            new("a.cs", 500), new("b.md", 200), new("c.json", 100), new("d.yml", 90), new("e.sh", 60), new("f.py", 50),
        };

        var shares = LanguageDetector.Detect(files, maxLanguages: 3);

        Assert.Equal(["C#", "Markdown", "Other"], shares.Select(s => s.Language));
        Assert.Equal(300, shares[2].Bytes);
        Assert.Equal(100.0, shares.Sum(s => s.Percent), precision: 6);
    }

    [Fact]
    public void Empty_input_gives_empty_result()
    {
        Assert.Empty(LanguageDetector.Detect([]));
    }
}

public sealed class RepoAnalyzerTests
{
    [Fact]
    public void Builds_summary_from_snapshot()
    {
        var snapshot = new RepoSnapshot(
            Name: "demo",
            WorkingDirectory: "/tmp/demo",
            Commits:
            [
                Commit("B", Day(5, 1), ("x.cs", 1, 0)),
                Commit("A", Day(1, 1), ("x.cs", 1, 0)),
                Commit("A", Day(3, 1), ("y.cs", 1, 0)),
            ],
            Files: [new("x.cs", 10), new("y.cs", 10)],
            BranchCount: 3,
            TagCount: 1,
            Since: null);

        var stats = RepoAnalyzer.Analyze(snapshot);

        Assert.Equal(new RepoSummary("demo", 3, 2, Day(1, 1), Day(5, 1), 3, 1, 2, null), stats.Summary);
        Assert.Equal("A", stats.Authors[0].Name);
        Assert.Equal("x.cs", stats.Hotspots[0].Path);
        Assert.Equal("C#", Assert.Single(stats.Languages).Language);
    }
}
