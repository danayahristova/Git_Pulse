using GitPulse.Models;

namespace GitPulse.Git;

/// <summary>Splits one file's unified diff text into typed lines. Pure; no LibGit2Sharp.</summary>
internal static class DiffParser
{
    /// <param name="patch">Unified diff for a single file, including its <c>diff --git</c> header.</param>
    /// <param name="maxLines">Keep at most this many lines; the rest are counted as hidden.</param>
    public static (IReadOnlyList<DiffLine> Lines, int Hidden) Parse(string patch, int maxLines)
    {
        var lines = new List<DiffLine>();
        var hidden = 0;
        var inHunk = false;

        foreach (var raw in patch.ReplaceLineEndings("\n").Split('\n'))
        {
            if (raw.StartsWith("@@", StringComparison.Ordinal))
            {
                inHunk = true;
                Add(new DiffLine(DiffLineKind.Hunk, raw));
                continue;
            }

            if (!inHunk || raw.Length == 0)
            {
                continue; // file header ("diff --git", "index", "---", "+++") or the trailing newline
            }

            var line = raw[0] switch
            {
                '+' => new DiffLine(DiffLineKind.Added, raw[1..]),
                '-' => new DiffLine(DiffLineKind.Deleted, raw[1..]),
                ' ' => new DiffLine(DiffLineKind.Context, raw[1..]),
                '\\' => new DiffLine(DiffLineKind.Note, raw),
                _ => null,
            };

            if (line is not null)
            {
                Add(line);
            }
        }

        return (lines, hidden);

        void Add(DiffLine line)
        {
            if (lines.Count < maxLines)
            {
                lines.Add(line);
            }
            else
            {
                hidden++;
            }
        }
    }
}
