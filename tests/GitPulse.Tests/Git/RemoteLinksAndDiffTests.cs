using GitPulse.Git;
using GitPulse.Models;

namespace GitPulse.Tests.Git;

public sealed class RemoteLinksTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    [Theory]
    [InlineData("https://github.com/owner/repo.git", "https://github.com/owner/repo")]
    [InlineData("https://github.com/owner/repo", "https://github.com/owner/repo")]
    [InlineData("git@github.com:owner/repo.git", "https://github.com/owner/repo")]
    [InlineData("ssh://git@github.com/owner/repo.git", "https://github.com/owner/repo")]
    [InlineData("ssh://git@github.com:22/owner/repo.git", "https://github.com/owner/repo")]
    [InlineData("https://GitHub.com/Owner/Repo.git/", "https://github.com/Owner/Repo")]
    [InlineData("https://gitlab.com/group/sub/project.git", "https://gitlab.com/group/sub/project")]
    [InlineData("git@gitlab.example.com:team/app.git", "https://gitlab.example.com/team/app")] // self-hosted gitlab.*
    [InlineData("https://gitlab.company.io/team/app.git", "https://gitlab.company.io/team/app")]
    [InlineData("git@git.company.io:team/app.git", null)] // unknown host
    [InlineData("git@bitbucket.org:team/app.git", "https://bitbucket.org/team/app")]
    [InlineData("https://codeberg.org/user/app.git", "https://codeberg.org/user/app")]
    public void Builds_the_web_url(string remote, string? expected)
    {
        Assert.Equal(expected, RemoteLinks.WebUrl(remote));
    }

    [Theory]
    [InlineData("https://user:s3cr3t-token@github.com/owner/repo.git")]
    [InlineData("https://x-access-token:ghp_abcdef@github.com/owner/repo")]
    [InlineData("https://oauth2:glpat-xyz@gitlab.com/group/project.git")]
    public void Never_keeps_credentials(string remote)
    {
        var url = RemoteLinks.CommitUrl(remote, Sha);

        Assert.NotNull(url);
        Assert.DoesNotContain("@", url);
        Assert.DoesNotContain("token", url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("s3cr3t", url);
        Assert.DoesNotContain("ghp_", url);
        Assert.DoesNotContain("glpat", url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/home/me/repos/local.git")]
    [InlineData("file:///srv/git/app.git")]
    [InlineData("C:\\repos\\app")]
    [InlineData("https://git.example.com/owner/repo.git")] // unknown host: commit URL format unknown
    [InlineData("https://github.com/owner/repo]injected[link=evil")]
    [InlineData("https://github.com/onlyowner")]
    public void No_link_for_local_unknown_or_odd_remotes(string? remote)
    {
        Assert.Null(RemoteLinks.CommitUrl(remote, Sha));
    }

    [Theory]
    [InlineData("https://github.com/o/r.git", "https://github.com/o/r/commit/" + Sha)]
    [InlineData("https://gitlab.com/o/r.git", "https://gitlab.com/o/r/-/commit/" + Sha)]
    [InlineData("https://bitbucket.org/o/r.git", "https://bitbucket.org/o/r/commits/" + Sha)]
    [InlineData("https://codeberg.org/o/r.git", "https://codeberg.org/o/r/commit/" + Sha)]
    public void Uses_each_hosts_commit_url_format(string remote, string expected)
    {
        Assert.Equal(expected, RemoteLinks.CommitUrl(remote, Sha));
    }

    [Fact]
    public void Rejects_something_that_is_not_a_sha()
    {
        Assert.Null(RemoteLinks.CommitUrl("https://github.com/o/r", "HEAD~1"));
    }
}

public sealed class DiffParserTests
{
    private const string Patch =
        "diff --git a/a.txt b/a.txt\n" +
        "index 1111111..2222222 100644\n" +
        "--- a/a.txt\n" +
        "+++ b/a.txt\n" +
        "@@ -1,3 +1,3 @@\n" +
        " one\n" +
        "-two\n" +
        "+TWO\n" +
        " three\n" +
        "\\ No newline at end of file\n";

    [Fact]
    public void Types_each_line_and_skips_the_file_header()
    {
        var (lines, hidden) = DiffParser.Parse(Patch, maxLines: 100);

        Assert.Equal(0, hidden);
        Assert.Equal(
        [
            DiffLineKind.Hunk, DiffLineKind.Context, DiffLineKind.Deleted,
            DiffLineKind.Added, DiffLineKind.Context, DiffLineKind.Note,
        ], lines.Select(l => l.Kind));
        Assert.Equal("two", lines[2].Text);
        Assert.Equal("TWO", lines[3].Text);
        Assert.Equal("@@ -1,3 +1,3 @@", lines[0].Text);
    }

    [Fact]
    public void Cuts_off_at_max_lines_and_counts_the_rest()
    {
        var (lines, hidden) = DiffParser.Parse(Patch, maxLines: 2);

        Assert.Equal(2, lines.Count);
        Assert.Equal(4, hidden);
    }

    [Fact]
    public void Handles_crlf_and_empty_input()
    {
        Assert.Equal(6, DiffParser.Parse(Patch.Replace("\n", "\r\n"), 100).Lines.Count);
        Assert.Empty(DiffParser.Parse(string.Empty, 100).Lines);
    }
}

public sealed class ScanFilterTests
{
    [Fact]
    public void Describes_itself()
    {
        Assert.Equal("all history", ScanFilter.None.Describe());
        Assert.Equal(
            "since 2026-01-01 · until 2026-06-30 · branch main · authors ada, linus",
            new ScanFilter(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), ["main"], ["ada", "linus"]).Describe());
    }

    [Theory]
    [InlineData("ada", true)]
    [InlineData("ADA@", true)]
    [InlineData("lovelace", true)]
    [InlineData("linus", false)]
    public void Matches_authors_by_name_or_email_ignoring_case(string term, bool expected)
    {
        var filter = new ScanFilter(Authors: [term]);
        Assert.Equal(expected, filter.MatchesAuthor("Ada Lovelace", "ada@example.com"));
    }

    [Theory]
    [InlineData("ada@example.com", true)]
    [InlineData("ADA@EXAMPLE.COM", true)]
    [InlineData("nada@example.com", false)]          // contains "ada@example.com", but is someone else
    [InlineData("ada@example.com.au", false)]
    public void Ticked_authors_match_exactly(string email, bool expected)
    {
        var filter = new ScanFilter(ExactAuthors: ["ada@example.com"]);
        Assert.Equal(expected, filter.MatchesAuthor("Someone", email));
    }

    [Fact]
    public void Typed_and_ticked_authors_combine()
    {
        var filter = new ScanFilter(Authors: ["lovelace"], ExactAuthors: ["ada@example.com"]);

        Assert.True(filter.MatchesAuthor("Ada Lovelace", "ada@example.com"));
        Assert.False(filter.MatchesAuthor("Ada Byron", "ada@example.com")); // not "lovelace"
        Assert.Equal("authors lovelace, ada@example.com", filter.Describe());
    }

    [Fact]
    public void Exact_author_without_email_matches_by_name()
    {
        var filter = new ScanFilter(ExactAuthors: ["Anon"]);

        Assert.True(filter.MatchesAuthor("anon", ""));
        Assert.False(filter.MatchesAuthor("Anonymous", ""));
    }

    [Fact]
    public void No_author_filter_matches_everyone()
    {
        Assert.True(ScanFilter.None.MatchesAuthor("Anyone", "x@y"));
        Assert.True(ScanFilter.None.IsEmpty);
    }
}
