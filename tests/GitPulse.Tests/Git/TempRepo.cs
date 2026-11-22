using LibGit2Sharp;

namespace GitPulse.Tests.Git;

/// <summary>A throwaway Git repository in a temp folder, deleted on dispose.</summary>
internal sealed class TempRepo : IDisposable
{
    public TempRepo(bool init = true)
    {
        Dir = Directory.CreateTempSubdirectory("gitpulse-test-").FullName;
        if (init)
        {
            Repository.Init(Dir);
        }
    }

    public string Dir { get; }

    /// <summary>Writes <paramref name="files"/> (path → content) and commits them.</summary>
    public Commit Commit(string message, string author, DateTimeOffset when, params (string Path, string Content)[] files)
    {
        using var repo = new Repository(Dir);
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(Dir, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            LibGit2Sharp.Commands.Stage(repo, path);
        }

        var sig = Signature(author, when);
        return repo.Commit(message, sig, sig, new CommitOptions { AllowEmptyCommit = files.Length == 0 });
    }

    /// <summary>Creates a branch at HEAD, runs <paramref name="onBranch"/> on it, then merges it back (no fast-forward).</summary>
    public void MergeBranch(string branch, DateTimeOffset when, Action onBranch)
    {
        string mainName;
        using (var repo = new Repository(Dir))
        {
            mainName = repo.Head.FriendlyName;
            LibGit2Sharp.Commands.Checkout(repo, repo.CreateBranch(branch));
        }

        onBranch();

        using (var repo = new Repository(Dir))
        {
            LibGit2Sharp.Commands.Checkout(repo, repo.Branches[mainName]);
            var result = repo.Merge(repo.Branches[branch], Signature("Merger", when),
                new MergeOptions { FastForwardStrategy = FastForwardStrategy.NoFastForward });
            if (result.Status == MergeStatus.Conflicts)
            {
                throw new InvalidOperationException("Test merge had conflicts.");
            }
        }
    }

    public void Tag(string name)
    {
        using var repo = new Repository(Dir);
        repo.ApplyTag(name);
    }

    private static Signature Signature(string author, DateTimeOffset when) =>
        new(author, $"{author.ToLowerInvariant()}@example.com", when);

    public void Dispose()
    {
        // LibGit2Sharp marks pack/object files read-only; clear that so cleanup works on Windows.
        foreach (var f in Directory.EnumerateFiles(Dir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(f, FileAttributes.Normal);
        }

        Directory.Delete(Dir, recursive: true);
    }
}
