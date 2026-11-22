using GitPulse.Analysis;
using GitPulse.Caching;
using GitPulse.Git;
using GitPulse.Models;
using GitPulse.Tests.Git;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Caching;

public sealed class ScanCacheTests : IDisposable
{
    private readonly TempRepo _repo = new();
    private readonly string _cacheFile = Path.Combine(TestSetup.CacheDirectory, Guid.NewGuid() + ".json");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private (RepoSnapshot Snapshot, ScanCache Cache) Scan(ScanFilter? filter = null)
    {
        using var reader = RepositoryReader.Open(_repo.Dir);
        var cache = ScanCache.Load(reader.GitDirectory, _cacheFile);
        var snapshot = RepositoryScanner.Scan(reader, filter, cancellationToken: Ct, cache: cache);
        cache.Save();
        return (snapshot, cache);
    }

    [Fact]
    public void The_cache_location_honours_the_environment_variable_and_never_uses_the_repo()
    {
        Assert.Equal(TestSetup.CacheDirectory, CacheLocation.Directory);
        Assert.StartsWith(TestSetup.CacheDirectory, CacheLocation.FileFor(Path.Combine(_repo.Dir, ".git")));
        Assert.DoesNotContain(_repo.Dir, CacheLocation.FileFor(Path.Combine(_repo.Dir, ".git")));
    }

    [Fact]
    public void Second_scan_reads_every_commit_from_the_cache_with_identical_results()
    {
        _repo.Commit("One", "Ada", Day(1, 1), ("a.txt", "a\nb\n"));
        _repo.Commit("Two", "Linus", Day(1, 2), ("a.txt", "a\nB\n"), ("c.txt", "c\n"));

        var (first, firstCache) = Scan();
        var (second, secondCache) = Scan();

        Assert.Equal((0, 2), (firstCache.Hits, firstCache.Misses));
        Assert.Equal((2, 0), (secondCache.Hits, secondCache.Misses));
        Assert.Equal(RepoAnalyzer.Analyze(first).Hotspots, RepoAnalyzer.Analyze(second).Hotspots);
        Assert.Equal(first.Commits.Select(c => (c.Sha, c.LinesAdded, c.LinesDeleted)),
                     second.Commits.Select(c => (c.Sha, c.LinesAdded, c.LinesDeleted)));
    }

    [Fact]
    public void Only_new_commits_are_diffed_after_more_work()
    {
        _repo.Commit("One", "Ada", Day(1, 1), ("a.txt", "a\n"));
        Scan();
        _repo.Commit("Two", "Ada", Day(1, 2), ("a.txt", "b\n"));
        _repo.Commit("Three", "Ada", Day(1, 3), ("a.txt", "c\n"));

        var (snapshot, cache) = Scan();

        Assert.Equal(3, snapshot.Commits.Count);
        Assert.Equal((1, 2), (cache.Hits, cache.Misses));
    }

    [Fact]
    public void A_corrupt_cache_is_ignored_and_rebuilt()
    {
        _repo.Commit("One", "Ada", Day(1, 1), ("a.txt", "a\n"));
        File.WriteAllText(_cacheFile, "{ this is not json");

        var (snapshot, cache) = Scan();

        Assert.True(cache.LoadFailed);
        Assert.Single(snapshot.Commits);
        Assert.Equal(1, ScanCache.Load(Path.Combine(_repo.Dir, ".git"), _cacheFile).Count); // rewritten
    }

    [Fact]
    public void A_cache_for_another_repository_is_not_used()
    {
        _repo.Commit("One", "Ada", Day(1, 1), ("a.txt", "a\n"));
        Scan();

        using var other = new TempRepo();
        var cache = ScanCache.Load(Path.Combine(other.Dir, ".git"), _cacheFile);

        Assert.True(cache.LoadFailed);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Commits_that_left_the_history_are_pruned_by_a_full_scan()
    {
        _repo.Commit("Base", "Ada", Day(1, 1), ("a.txt", "a\n"));
        _repo.OnBranch("side", () => _repo.Commit("Side", "Ada", Day(1, 2), ("b.txt", "b\n")));
        Scan(new ScanFilter(Branches: ["side"])); // caches Base and Side

        using var reader = RepositoryReader.Open(_repo.Dir);
        var cache = ScanCache.Load(reader.GitDirectory, _cacheFile);
        Assert.Equal(2, cache.Count);

        var snapshot = RepositoryScanner.Scan(reader, cancellationToken: Ct, cache: cache); // HEAD only: Base
        cache.Prune(snapshot.Commits.Select(c => c.Sha));

        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Saving_is_atomic_and_leaves_no_temp_files()
    {
        _repo.Commit("One", "Ada", Day(1, 1), ("a.txt", "a\n"));
        Scan();

        Assert.True(File.Exists(_cacheFile));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(_cacheFile)!, Path.GetFileName(_cacheFile) + ".*.tmp"));
    }

    [Fact]
    public void Scanning_never_writes_inside_the_repository()
    {
        _repo.Commit("One", "Ada", Day(1, 1), ("a.txt", "a\n"));
        var before = Directory.GetFileSystemEntries(_repo.Dir, "*", SearchOption.TopDirectoryOnly).Order().ToList();

        Scan();

        Assert.Equal(before, Directory.GetFileSystemEntries(_repo.Dir, "*", SearchOption.TopDirectoryOnly).Order());
    }

    public void Dispose()
    {
        _repo.Dispose();
        File.Delete(_cacheFile);
    }
}
