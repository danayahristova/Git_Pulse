using GitPulse.Models;

namespace GitPulse.Analysis;

/// <summary>Per-author totals. Pure function of the commit list.</summary>
internal static class AuthorAnalyzer
{
    /// <summary>
    /// Groups commits by author email (case-insensitive; by name when the email is empty).
    /// The displayed name is the one on the author's most recent commit.
    /// Sorted by commits, then lines changed, then name.
    /// </summary>
    public static IReadOnlyList<AuthorStats> Compute(IEnumerable<CommitRecord> commits) =>
        commits
            .GroupBy(c => string.IsNullOrWhiteSpace(c.AuthorEmail)
                ? "name:" + c.AuthorName
                : c.AuthorEmail.Trim().ToLowerInvariant())
            .Select(g =>
            {
                var latest = g.MaxBy(c => c.When)!;
                return new AuthorStats(
                    latest.AuthorName,
                    latest.AuthorEmail,
                    g.Count(),
                    g.Sum(c => c.LinesAdded),
                    g.Sum(c => c.LinesDeleted),
                    g.Min(c => c.When),
                    latest.When);
            })
            .OrderByDescending(a => a.Commits)
            .ThenByDescending(a => a.LinesAdded + a.LinesDeleted)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}

/// <summary>Per-file churn ("hotspots"). Pure function of the commit list.</summary>
internal static class ChurnAnalyzer
{
    /// <summary>Sorted by churn, then number of commits, then path.</summary>
    public static IReadOnlyList<FileChurn> Compute(IEnumerable<CommitRecord> commits) =>
        commits
            .SelectMany(c => c.Files)
            .GroupBy(f => f.Path, StringComparer.Ordinal)
            .Select(g => new FileChurn(g.Key, g.Count(), g.Sum(f => f.LinesAdded), g.Sum(f => f.LinesDeleted)))
            .OrderByDescending(f => f.Churn)
            .ThenByDescending(f => f.Commits)
            .ThenBy(f => f.Path, StringComparer.Ordinal)
            .ToList();
}

/// <summary>Language share of the HEAD tree, by file size.</summary>
internal static class LanguageDetector
{
    public const string Other = "Other";

    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "C#", [".csx"] = "C#", [".fs"] = "F#", [".fsx"] = "F#", [".vb"] = "Visual Basic",
        [".razor"] = "Razor", [".cshtml"] = "Razor", [".xaml"] = "XAML",
        [".csproj"] = "MSBuild", [".fsproj"] = "MSBuild", [".props"] = "MSBuild", [".targets"] = "MSBuild",
        [".sln"] = "MSBuild", [".slnx"] = "MSBuild",
        [".js"] = "JavaScript", [".mjs"] = "JavaScript", [".cjs"] = "JavaScript", [".jsx"] = "JavaScript",
        [".ts"] = "TypeScript", [".tsx"] = "TypeScript", [".vue"] = "Vue", [".svelte"] = "Svelte",
        [".html"] = "HTML", [".htm"] = "HTML", [".css"] = "CSS", [".scss"] = "SCSS", [".less"] = "Less",
        [".py"] = "Python", [".rb"] = "Ruby", [".php"] = "PHP", [".pl"] = "Perl", [".lua"] = "Lua",
        [".java"] = "Java", [".kt"] = "Kotlin", [".kts"] = "Kotlin", [".scala"] = "Scala", [".groovy"] = "Groovy",
        [".go"] = "Go", [".rs"] = "Rust", [".swift"] = "Swift", [".m"] = "Objective-C", [".dart"] = "Dart",
        [".c"] = "C", [".h"] = "C", [".cpp"] = "C++", [".cc"] = "C++", [".cxx"] = "C++", [".hpp"] = "C++",
        [".hh"] = "C++", [".zig"] = "Zig", [".hs"] = "Haskell", [".ex"] = "Elixir", [".exs"] = "Elixir",
        [".erl"] = "Erlang", [".clj"] = "Clojure", [".r"] = "R", [".jl"] = "Julia",
        [".sh"] = "Shell", [".bash"] = "Shell", [".zsh"] = "Shell", [".ps1"] = "PowerShell", [".psm1"] = "PowerShell",
        [".bat"] = "Batch", [".cmd"] = "Batch", [".sql"] = "SQL",
        [".md"] = "Markdown", [".markdown"] = "Markdown", [".rst"] = "reStructuredText", [".adoc"] = "AsciiDoc",
        [".json"] = "JSON", [".jsonc"] = "JSON", [".yml"] = "YAML", [".yaml"] = "YAML", [".toml"] = "TOML",
        [".xml"] = "XML", [".ini"] = "INI", [".proto"] = "Protocol Buffers", [".graphql"] = "GraphQL",
        [".tf"] = "Terraform", [".bicep"] = "Bicep",
    };

    private static readonly Dictionary<string, string> ByFileName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dockerfile"] = "Dockerfile", ["Makefile"] = "Makefile", ["CMakeLists.txt"] = "CMake",
        ["Rakefile"] = "Ruby", ["Gemfile"] = "Ruby", ["Jenkinsfile"] = "Groovy",
    };

    /// <summary>The language for a path, or null when it is not a recognized source/text format.</summary>
    public static string? LanguageOf(string path)
    {
        var fileName = Path.GetFileName(path);
        if (ByFileName.TryGetValue(fileName, out var byName))
        {
            return byName;
        }

        var ext = Path.GetExtension(fileName);
        return ext.Length > 0 && ByExtension.TryGetValue(ext, out var byExt) ? byExt : null;
    }

    /// <summary>
    /// Share per language by bytes, largest first. Unrecognized files are ignored.
    /// At most <paramref name="maxLanguages"/> entries: beyond that the tail is merged into "Other".
    /// </summary>
    public static IReadOnlyList<LanguageShare> Detect(IEnumerable<TrackedFile> files, int maxLanguages = 6)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLanguages, 2);

        var totals = files
            .Select(f => (Language: LanguageOf(f.Path), f.SizeBytes))
            .Where(x => x.Language is not null)
            .GroupBy(x => x.Language!)
            .Select(g => (Language: g.Key, Bytes: g.Sum(x => x.SizeBytes)))
            .OrderByDescending(x => x.Bytes)
            .ThenBy(x => x.Language, StringComparer.Ordinal)
            .ToList();

        var all = totals.Sum(x => x.Bytes);
        if (all == 0)
        {
            return [];
        }

        if (totals.Count > maxLanguages)
        {
            var head = totals.Take(maxLanguages - 1).ToList();
            head.Add((Other, totals.Skip(maxLanguages - 1).Sum(x => x.Bytes)));
            totals = head;
        }

        return totals.Select(x => new LanguageShare(x.Language, x.Bytes, 100.0 * x.Bytes / all)).ToList();
    }
}

/// <summary>Turns a raw <see cref="RepoSnapshot"/> into the <see cref="RepoStats"/> renderers use.</summary>
internal static class RepoAnalyzer
{
    public static RepoStats Analyze(RepoSnapshot snapshot)
    {
        var authors = AuthorAnalyzer.Compute(snapshot.Commits);
        var summary = new RepoSummary(
            snapshot.Name,
            snapshot.Commits.Count,
            authors.Count,
            snapshot.Commits.Count > 0 ? snapshot.Commits.Min(c => c.When) : default,
            snapshot.Commits.Count > 0 ? snapshot.Commits.Max(c => c.When) : default,
            snapshot.BranchCount,
            snapshot.TagCount,
            snapshot.Files.Count,
            snapshot.Since);

        return new RepoStats(
            summary,
            authors,
            ChurnAnalyzer.Compute(snapshot.Commits),
            LanguageDetector.Detect(snapshot.Files));
    }
}
