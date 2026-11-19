using GitPulse.Git;
using LibGit2Sharp;

namespace GitPulse.Tests.Git;

/// <summary>
/// Phase 0 spike: proves LibGit2Sharp's native binaries load and that commits and
/// diff stats are read correctly. Runs on every CI OS.
/// </summary>
public sealed class RepositoryReaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gitpulse-test-").FullName;

    [Fact]
    public void Open_outside_a_repository_throws()
    {
        Assert.Throws<NotAGitRepositoryException>(() => RepositoryReader.Open(_dir));
    }

    [Fact]
    public void Empty_repository_has_no_commits()
    {
        Repository.Init(_dir);

        using var reader = RepositoryReader.Open(_dir);

        Assert.True(reader.IsEmpty);
        Assert.Empty(reader.ReadCommits());
    }

    [Fact]
    public void Reads_commits_newest_first_with_line_stats()
    {
        Repository.Init(_dir);
        using (var repo = new Repository(_dir))
        {
            Commit(repo, "a.txt", "one\ntwo\nthree\n", "Add a", "Ada", 1);
            Commit(repo, "a.txt", "one\nTWO\nthree\nfour\n", "Edit a", "Linus", 2);
        }

        using var reader = RepositoryReader.Open(_dir);
        var commits = reader.ReadCommits().ToList();

        Assert.Equal(2, commits.Count);

        var latest = commits[0];
        Assert.Equal("Edit a", latest.MessageShort);
        Assert.Equal("Linus", latest.AuthorName);
        Assert.Equal(2, latest.LinesAdded);   // TWO, four
        Assert.Equal(1, latest.LinesDeleted); // two
        var file = Assert.Single(latest.Files);
        Assert.Equal("a.txt", file.Path);

        var first = commits[1];
        Assert.Equal(3, first.LinesAdded);
        Assert.Equal(0, first.LinesDeleted);
    }

    [Fact]
    public void Opens_from_a_subdirectory()
    {
        Repository.Init(_dir);
        var sub = Directory.CreateDirectory(Path.Combine(_dir, "nested", "deeper")).FullName;

        using var reader = RepositoryReader.Open(sub);

        Assert.True(reader.IsEmpty);
    }

    private void Commit(Repository repo, string file, string content, string message, string author, int day)
    {
        File.WriteAllText(Path.Combine(_dir, file), content);
        LibGit2Sharp.Commands.Stage(repo, file);
        var sig = new Signature(author, $"{author.ToLowerInvariant()}@example.com",
            new DateTimeOffset(2026, 1, day, 12, 0, 0, TimeSpan.Zero));
        repo.Commit(message, sig, sig);
    }

    public void Dispose()
    {
        // LibGit2Sharp marks pack/object files read-only; clear that so cleanup works on Windows.
        foreach (var f in Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(f, FileAttributes.Normal);
        }

        Directory.Delete(_dir, recursive: true);
    }
}
