using GitPulse.Analysis;
using GitPulse.Git;
using GitPulse.Models;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Git;

public sealed class ScanFilterOnRealReposTests : IDisposable
{
    private readonly TempRepo _repo = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RepoSnapshot Scan(ScanFilter filter)
    {
        using var reader = RepositoryReader.Open(_repo.Dir);
        return RepositoryScanner.Scan(reader, filter, cancellationToken: Ct);
    }

    [Fact]
    public void Until_drops_newer_commits_and_includes_the_whole_last_day()
    {
        _repo.Commit("Jan", "Ada", Day(1, 10), ("a.txt", "1\n"));
        _repo.Commit("Mar late", "Ada", new DateTimeOffset(2026, 3, 1, 23, 30, 0, TimeSpan.Zero).ToLocalTime(), ("a.txt", "2\n"));
        _repo.Commit("Jun", "Ada", Day(6, 1), ("a.txt", "3\n"));

        var snapshot = Scan(new ScanFilter(Until: DateOnly.FromDateTime(
            new DateTimeOffset(2026, 3, 1, 23, 30, 0, TimeSpan.Zero).LocalDateTime)));

        Assert.Equal(["Mar late", "Jan"], snapshot.Commits.Select(c => c.MessageShort));
    }

    [Fact]
    public void Branch_reads_history_from_that_branch_instead_of_head()
    {
        _repo.Commit("Base", "Ada", Day(1, 1), ("a.txt", "a\n"));
        _repo.OnBranch("feature", () => _repo.Commit("Feature work", "Linus", Day(1, 2), ("b.txt", "b\n")));

        var head = Scan(ScanFilter.None);
        var feature = Scan(new ScanFilter(Branches: ["feature"]));

        Assert.Equal(["Base"], head.Commits.Select(c => c.MessageShort));
        Assert.Equal(["Feature work", "Base"], feature.Commits.Select(c => c.MessageShort));
    }

    [Fact]
    public void Unknown_branch_is_a_friendly_error_listing_the_branches()
    {
        _repo.Commit("Base", "Ada", Day(1, 1), ("a.txt", "a\n"));

        var error = Assert.Throws<BranchNotFoundException>(() => Scan(new ScanFilter(Branches: ["nope"])));

        Assert.Contains("nope", error.Message);
        Assert.NotNull(error.Hint);
    }

    [Fact]
    public void Author_filter_is_applied_by_the_analysis()
    {
        _repo.Commit("By Ada", "Ada", Day(1, 1), ("a.txt", "a\n"));
        _repo.Commit("By Linus", "Linus", Day(1, 2), ("b.txt", "b\nb\n"));

        var snapshot = Scan(new ScanFilter(Authors: ["linus"]));
        var stats = RepoAnalyzer.Analyze(snapshot);

        Assert.Equal(2, snapshot.Commits.Count); // the scan keeps everyone
        Assert.Equal(1, stats.Summary.Commits);
        Assert.Equal("Linus", Assert.Single(stats.Authors).Name);
        Assert.Equal("b.txt", Assert.Single(stats.Hotspots).Path);
        Assert.Contains("author linus", stats.Summary.Filter.Describe());
    }

    [Fact]
    public void Ticked_author_does_not_match_a_similar_email()
    {
        _repo.Commit("By Ada", "Ada", Day(1, 1), ("a.txt", "a\n"));
        _repo.Commit("By Nada", "Nada", Day(1, 2), ("b.txt", "b\n")); // nada@example.com contains ada@example.com

        var stats = RepoAnalyzer.Analyze(Scan(new ScanFilter(ExactAuthors: ["ada@example.com"])));

        Assert.Equal("Ada", Assert.Single(stats.Authors).Name);
    }

    [Fact]
    public void Recent_commits_stops_after_the_requested_number()
    {
        for (var day = 1; day <= 5; day++)
        {
            _repo.Commit($"Commit {day}", "Ada", Day(1, day), ("a.txt", $"{day}\n"));
        }

        using var reader = RepositoryReader.Open(_repo.Dir);
        var walked = 0;
        var firstTwo = reader.EnumerateCommits().Select(c => { walked++; return c; }).Take(2).ToList();

        Assert.Equal(["Commit 5", "Commit 4"], firstTwo.Select(c => c.MessageShort));
        Assert.Equal(2, walked); // the walk stopped; it did not read all 5 commits first
        Assert.Equal(3, reader.RecentCommits(3).Count);
    }

    [Fact]
    public void Unknown_branch_fails_immediately_even_for_a_lazy_walk()
    {
        _repo.Commit("Base", "Ada", Day(1, 1), ("a.txt", "a\n"));
        using var reader = RepositoryReader.Open(_repo.Dir);

        Assert.Throws<BranchNotFoundException>(() => reader.EnumerateCommits(new ScanFilter(Branches: ["nope"])));
    }

    [Fact]
    public void Author_filter_matching_nobody_is_a_friendly_error()
    {
        _repo.Commit("By Ada", "Ada", Day(1, 1), ("a.txt", "a\n"));
        var snapshot = Scan(new ScanFilter(Authors: ["grace"]));

        Assert.Throws<NoCommitsByAuthorException>(() => RepoAnalyzer.Analyze(snapshot));
    }

    public void Dispose() => _repo.Dispose();
}

public sealed class CommitDetailTests : IDisposable
{
    private readonly TempRepo _repo = new();

    private CommitDetail Read(string revision, DiffLimits? limits = null)
    {
        using var reader = RepositoryReader.Open(_repo.Dir);
        return reader.ReadCommitDetail(revision, limits ?? DiffLimits.Default);
    }

    [Fact]
    public void Reads_author_message_parents_and_diff()
    {
        var first = _repo.Commit("Add a", "Ada", Day(1, 1), ("a.txt", "one\ntwo\n"));
        var second = _repo.Commit("Edit a\n\nLonger explanation\nover two lines.", "Linus", Day(1, 2), ("a.txt", "one\nTWO\n"));

        var detail = Read("HEAD");

        Assert.Equal(second.Sha, detail.Sha);
        Assert.Equal(second.Sha[..7], detail.ShortSha);
        Assert.Equal("Linus", detail.AuthorName);
        Assert.Equal("Edit a", detail.Subject);
        Assert.Equal("Longer explanation\nover two lines.", detail.Body);
        Assert.Equal([first.Sha], detail.Parents);
        var file = Assert.Single(detail.Files);
        Assert.Equal(FileChangeKind.Modified, file.Kind);
        Assert.Equal((1, 1), (file.LinesAdded, file.LinesDeleted));
        Assert.Contains(file.Lines, l => l is { Kind: DiffLineKind.Deleted, Text: "two" });
        Assert.Contains(file.Lines, l => l is { Kind: DiffLineKind.Added, Text: "TWO" });
        Assert.Null(detail.WebUrl); // no remote
    }

    [Theory]
    [InlineData("HEAD~1")]
    [InlineData("first")]
    public void Accepts_revisions_short_shas_and_tags(string revision)
    {
        var first = _repo.Commit("First", "Ada", Day(1, 1), ("a.txt", "a\n"));
        _repo.Tag("first");
        _repo.Commit("Second", "Ada", Day(1, 2), ("a.txt", "b\n"));

        Assert.Equal(first.Sha, Read(revision).Sha);
        Assert.Equal(first.Sha, Read(first.Sha[..8]).Sha);
    }

    [Fact]
    public void Root_commit_shows_all_files_as_added()
    {
        _repo.Commit("Root", "Ada", Day(1, 1), ("a.txt", "a\n"), ("b.txt", "b\n"));

        var detail = Read("HEAD");

        Assert.Empty(detail.Parents);
        Assert.All(detail.Files, f => Assert.Equal(FileChangeKind.Added, f.Kind));
    }

    [Fact]
    public void Reports_deleted_and_renamed_files()
    {
        _repo.Commit("Add", "Ada", Day(1, 1), ("old.txt", "same content\nline two\nline three\n"), ("gone.txt", "x\n"));
        _repo.Rename("old.txt", "new.txt");
        _repo.Delete("gone.txt");
        _repo.CommitStaged("Rename and delete", "Ada", Day(1, 2));

        var files = Read("HEAD").Files.ToDictionary(f => f.Path);

        Assert.Equal(FileChangeKind.Deleted, files["gone.txt"].Kind);
        Assert.Equal(FileChangeKind.Renamed, files["new.txt"].Kind);
        Assert.Equal("old.txt", files["new.txt"].OldPath);
        Assert.False(files.ContainsKey("old.txt")); // one rename, not a delete + add
        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void Links_to_the_commit_when_origin_is_a_known_host()
    {
        _repo.Commit("Add", "Ada", Day(1, 1), ("a.txt", "a\n"));
        _repo.AddRemote("origin", "https://user:secret-token@github.com/owner/repo.git");

        var detail = Read("HEAD");

        Assert.Equal($"https://github.com/owner/repo/commit/{detail.Sha}", detail.WebUrl);
    }

    [Fact]
    public void Diff_limits_cut_long_diffs()
    {
        var big = string.Concat(Enumerable.Range(1, 50).Select(i => $"line {i}\n"));
        _repo.Commit("Big", "Ada", Day(1, 1), ("big.txt", big));

        var limited = Read("HEAD", new DiffLimits(MaxLinesPerFile: 10, MaxTotalLines: 100));
        var none = Read("HEAD", DiffLimits.None);

        var file = Assert.Single(limited.Files);
        Assert.Equal(10, file.Lines.Count);
        Assert.Equal(41, file.HiddenLines); // 1 hunk header + 50 lines - 10 shown
        Assert.Equal(50, file.LinesAdded);
        Assert.Empty(Assert.Single(none.Files).Lines);
    }

    [Theory]
    [InlineData("does-not-exist")]
    [InlineData("HEAD~10")]
    [InlineData("deadbeef")]
    public void Unknown_revision_is_a_friendly_error(string revision)
    {
        _repo.Commit("Only", "Ada", Day(1, 1), ("a.txt", "a\n"));

        Assert.Throws<CommitNotFoundException>(() => Read(revision));
    }

    [Fact]
    public void Empty_repository_is_a_friendly_error()
    {
        Assert.Throws<EmptyRepositoryException>(() => Read("HEAD"));
    }

    public void Dispose() => _repo.Dispose();
}
