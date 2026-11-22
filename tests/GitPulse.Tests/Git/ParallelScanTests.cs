using GitPulse.Git;
using GitPulse.Models;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Git;

public sealed class ParallelScanTests : IDisposable
{
    private readonly TempRepo _repo = new();

    public ParallelScanTests()
    {
        // 30 commits over a few files, plus a merge, so every worker gets several commits.
        for (var i = 1; i <= 30; i++)
        {
            _repo.Commit($"Commit {i}", i % 3 == 0 ? "Linus" : "Ada", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddHours(i),
                ($"f{i % 4}.txt", string.Concat(Enumerable.Repeat($"line {i}\n", i))));
        }

        _repo.MergeBranch("side", new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero), () =>
            _repo.Commit("Side", "Grace", new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero), ("side.txt", "s\n")));
    }

    private RepoSnapshot Scan(int workers, IScanProgress? progress = null)
    {
        using var reader = RepositoryReader.Open(_repo.Dir);
        return RepositoryScanner.Scan(reader, progress: progress, cancellationToken: TestContext.Current.CancellationToken, maxDegreeOfParallelism: workers);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(7)]
    public void Parallel_diffing_gives_exactly_the_sequential_result(int workers)
    {
        var sequential = Scan(workers: 1);
        var parallel = Scan(workers);

        Assert.Equal(32, sequential.Commits.Count);
        Assert.Equal(
            sequential.Commits.Select(c => (c.Sha, c.IsMerge, string.Join(";", c.Files.Select(f => $"{f.Path}:{f.LinesAdded}:{f.LinesDeleted}")))),
            parallel.Commits.Select(c => (c.Sha, c.IsMerge, string.Join(";", c.Files.Select(f => $"{f.Path}:{f.LinesAdded}:{f.LinesDeleted}")))));
    }

    [Fact]
    public void Progress_counts_every_commit_once_when_parallel()
    {
        var progress = new CountingProgress();

        Scan(workers: 4, progress);

        Assert.Equal(32, progress.DiffAdvances);
    }

    [Theory]
    [InlineData(10, 0, 1)]   // small scan: no workers to start
    [InlineData(199, 0, 1)]
    [InlineData(5, 4, 4)]    // explicit request is honoured...
    [InlineData(3, 8, 3)]    // ...but never more workers than commits
    [InlineData(0, 4, 1)]
    public void Chooses_the_number_of_workers(int commits, int requested, int expected)
    {
        Assert.Equal(expected, RepositoryScanner.WorkersFor(commits, requested));
    }

    [Fact]
    public void Large_scans_use_several_workers_but_at_most_eight()
    {
        var workers = RepositoryScanner.WorkersFor(5000, 0);

        Assert.InRange(workers, 1, 8);
        Assert.Equal(Math.Clamp(Environment.ProcessorCount, 1, 8), workers);
    }

    public void Dispose() => _repo.Dispose();

    private sealed class CountingProgress : IScanProgress
    {
        private int _diffAdvances;

        public int DiffAdvances => _diffAdvances;

        public void Started(ScanStage stage, int? total)
        {
        }

        public void Advanced(ScanStage stage, int amount = 1)
        {
            if (stage == ScanStage.ComputingDiffStats)
            {
                Interlocked.Add(ref _diffAdvances, amount); // called from several workers
            }
        }

        public void Completed(ScanStage stage)
        {
        }
    }
}
