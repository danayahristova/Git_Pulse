using GitPulse.Git;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Git;

/// <summary>
/// Real repositories in temp folders: proves LibGit2Sharp's native binaries load and that
/// commits and diff stats are read correctly. Runs on every CI OS.
/// </summary>
public sealed class RepositoryReaderTests : IDisposable
{
    private readonly TempRepo _repo = new();

    [Fact]
    public void Open_outside_a_repository_throws()
    {
        using var notARepo = new TempRepo(init: false);
        Assert.Throws<NotAGitRepositoryException>(() => RepositoryReader.Open(notARepo.Dir));
    }

    [Fact]
    public void Open_a_missing_directory_throws()
    {
        Assert.Throws<NotAGitRepositoryException>(() => RepositoryReader.Open(Path.Combine(_repo.Dir, "nope")));
    }

    [Fact]
    public void Empty_repository_has_no_commits()
    {
        using var reader = RepositoryReader.Open(_repo.Dir);

        Assert.True(reader.IsEmpty);
        Assert.Empty(reader.ReadCommits());
    }

    [Fact]
    public void Reads_commits_newest_first_with_line_stats()
    {
        _repo.Commit("Add a", "Ada", Day(1, 1), ("a.txt", "one\ntwo\nthree\n"));
        _repo.Commit("Edit a", "Linus", Day(1, 2), ("a.txt", "one\nTWO\nthree\nfour\n"));

        using var reader = RepositoryReader.Open(_repo.Dir);
        var commits = reader.ReadCommits().ToList();

        Assert.Equal(2, commits.Count);

        var latest = commits[0];
        Assert.Equal("Edit a", latest.MessageShort);
        Assert.Equal("Linus", latest.AuthorName);
        Assert.Equal(2, latest.LinesAdded);   // TWO, four
        Assert.Equal(1, latest.LinesDeleted); // two
        Assert.Equal("a.txt", Assert.Single(latest.Files).Path);

        var first = commits[1];
        Assert.Equal(3, first.LinesAdded);
        Assert.Equal(0, first.LinesDeleted);
    }

    [Fact]
    public void Opens_from_a_subdirectory()
    {
        _repo.Commit("Add", "Ada", Day(1, 1), ("nested/deeper/x.txt", "x\n"));

        using var reader = RepositoryReader.Open(Path.Combine(_repo.Dir, "nested", "deeper"));

        Assert.False(reader.IsEmpty);
        Assert.Equal(Path.GetFileName(_repo.Dir), reader.Name);
    }

    public void Dispose() => _repo.Dispose();
}
