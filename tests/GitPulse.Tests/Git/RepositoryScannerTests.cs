using GitPulse.Git;
using GitPulse.Models;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Git;

public sealed class RepositoryScannerTests : IDisposable
{
    private readonly TempRepo _repo = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Empty_repository_throws_friendly_error()
    {
        using var reader = RepositoryReader.Open(_repo.Dir);
        Assert.Throws<EmptyRepositoryException>(() => RepositoryScanner.Scan(reader, cancellationToken: Ct));
    }

    [Fact]
    public void Scans_commits_files_branches_and_tags()
    {
        _repo.Commit("Add", "Ada", Day(1, 1), ("src/a.cs", "class A {}\n"), ("README.md", "# hi\n"));
        _repo.Commit("More", "Ada", Day(1, 2), ("src/b.cs", "class B {}\n"));
        _repo.Tag("v0.1");

        using var reader = RepositoryReader.Open(_repo.Dir);
        var snapshot = RepositoryScanner.Scan(reader, cancellationToken: Ct);

        Assert.Equal(2, snapshot.Commits.Count);
        Assert.Equal(["README.md", "src/a.cs", "src/b.cs"], snapshot.Files.Select(f => f.Path).Order());
        Assert.Equal(11, snapshot.Files.Single(f => f.Path == "src/a.cs").SizeBytes);
        Assert.Equal(1, snapshot.BranchCount);
        Assert.Equal(1, snapshot.TagCount);
    }

    [Fact]
    public void Merge_commits_have_no_file_stats_so_changes_are_not_counted_twice()
    {
        _repo.Commit("Base", "Ada", Day(1, 1), ("a.txt", "a\n"));
        _repo.MergeBranch("feature", Day(1, 3), () =>
            _repo.Commit("Feature", "Linus", Day(1, 2), ("b.txt", "b\nb\n")));

        using var reader = RepositoryReader.Open(_repo.Dir);
        var snapshot = RepositoryScanner.Scan(reader, cancellationToken: Ct);

        Assert.Equal(3, snapshot.Commits.Count);
        var merge = snapshot.Commits[0];
        Assert.True(merge.IsMerge);
        Assert.Empty(merge.Files);
        Assert.Equal(3, snapshot.Commits.Sum(c => c.LinesAdded)); // a.txt (1) + b.txt (2), merge adds 0
    }

    [Fact]
    public void Since_filters_older_commits()
    {
        _repo.Commit("Old", "Ada", Day(1, 1), ("a.txt", "a\n"));
        _repo.Commit("New", "Ada", Day(6, 1), ("a.txt", "b\n"));

        using var reader = RepositoryReader.Open(_repo.Dir);
        var snapshot = RepositoryScanner.Scan(reader, new ScanFilter(Since: new DateOnly(2026, 3, 1)), cancellationToken: Ct);

        Assert.Equal("New", Assert.Single(snapshot.Commits).MessageShort);
    }

    [Fact]
    public void Since_after_every_commit_throws_friendly_error()
    {
        _repo.Commit("Old", "Ada", Day(1, 1), ("a.txt", "a\n"));

        using var reader = RepositoryReader.Open(_repo.Dir);

        Assert.Throws<NoCommitsInRangeException>(() =>
            RepositoryScanner.Scan(reader, new ScanFilter(Since: new DateOnly(2026, 6, 1)), cancellationToken: Ct));
    }

    [Fact]
    public void Reports_every_stage_in_order()
    {
        _repo.Commit("One", "Ada", Day(1, 1), ("a.txt", "a\n"));
        _repo.Commit("Two", "Ada", Day(1, 2), ("b.txt", "b\n"));

        var progress = new RecordingProgress();
        using var reader = RepositoryReader.Open(_repo.Dir);
        RepositoryScanner.Scan(reader, progress: progress, cancellationToken: Ct);

        Assert.Equal(
        [
            "start ReadingCommits ?", "advance ReadingCommits", "advance ReadingCommits", "done ReadingCommits",
            "start ComputingDiffStats 2", "advance ComputingDiffStats", "advance ComputingDiffStats", "done ComputingDiffStats",
            "start DetectingLanguages 2", "advance DetectingLanguages", "advance DetectingLanguages", "done DetectingLanguages",
        ], progress.Events);
    }

    public void Dispose() => _repo.Dispose();

    private sealed class RecordingProgress : IScanProgress
    {
        public List<string> Events { get; } = [];

        public void Started(ScanStage stage, int? total) => Events.Add($"start {stage} {total?.ToString() ?? "?"}");

        public void Advanced(ScanStage stage, int amount = 1) => Events.Add($"advance {stage}");

        public void Completed(ScanStage stage) => Events.Add($"done {stage}");
    }
}
