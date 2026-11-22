using System.Text.RegularExpressions;

namespace GitPulse.Git;

/// <summary>
/// Turns a remote URL from <c>.git/config</c> into a web link for a commit. Pure string work:
/// no network access. Credentials in the URL (<c>https://user:token@host/…</c>) are always dropped.
/// Only known hosts get links, because the commit URL format differs between them.
/// </summary>
internal static partial class RemoteLinks
{
    /// <summary>The repository's web page, e.g. <c>https://github.com/owner/repo</c>, or null.</summary>
    public static string? WebUrl(string? remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return null;
        }

        var url = remoteUrl.Trim();
        string host;
        string path;

        var scp = ScpLike().Match(url); // git@github.com:owner/repo.git
        if (scp.Success)
        {
            host = scp.Groups["host"].Value;
            path = scp.Groups["path"].Value;
        }
        else if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                 uri.Scheme is "https" or "http" or "ssh" or "git")
        {
            host = uri.Host; // drops user:password and port
            path = uri.AbsolutePath.TrimStart('/');
        }
        else
        {
            return null; // local path, file:// or something unknown
        }

        path = path.TrimEnd('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^4];
        }

        if (HostKindOf(host) is null || !SafePath().IsMatch(path) || !path.Contains('/'))
        {
            return null;
        }

        return $"https://{host.ToLowerInvariant()}/{path}";
    }

    /// <summary>Link to one commit on the repository's web page, or null when there is none.</summary>
    public static string? CommitUrl(string? remoteUrl, string sha)
    {
        if (WebUrl(remoteUrl) is not { } web || !Sha().IsMatch(sha))
        {
            return null;
        }

        var host = new Uri(web).Host;
        return HostKindOf(host) switch
        {
            HostKind.Bitbucket => $"{web}/commits/{sha}",
            HostKind.GitLab => $"{web}/-/commit/{sha}",
            _ => $"{web}/commit/{sha}",
        };
    }

    private enum HostKind
    {
        GitHub,
        GitLab,
        Bitbucket,
        Gitea,
    }

    private static HostKind? HostKindOf(string host)
    {
        host = host.ToLowerInvariant();
        return host switch
        {
            "github.com" or "www.github.com" => HostKind.GitHub,
            "gitlab.com" => HostKind.GitLab,
            "bitbucket.org" => HostKind.Bitbucket,
            "codeberg.org" => HostKind.Gitea,
            _ when host.StartsWith("gitlab.", StringComparison.Ordinal) => HostKind.GitLab,
            _ => null,
        };
    }

    [GeneratedRegex(@"^(?:[\w.-]+@)?(?<host>[\w.-]+):(?!//)(?<path>.+)$")]
    private static partial Regex ScpLike();

    /// <summary>owner/repo style paths only: nothing that could break out of a markup link.</summary>
    [GeneratedRegex(@"^[\w.~-]+(/[\w.~-]+)+$")]
    private static partial Regex SafePath();

    [GeneratedRegex("^[0-9a-f]{7,40}$")]
    private static partial Regex Sha();
}
