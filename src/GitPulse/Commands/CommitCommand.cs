using System.ComponentModel;
using GitPulse.Git;
using GitPulse.Interactive;
using GitPulse.Rendering;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitPulse.Commands;

internal sealed class CommitSettings : GlobalSettings
{
    [CommandArgument(0, "[REVISION]")]
    [Description("Commit to show: a SHA (or its start), HEAD~N, a branch or a tag. Leave out to pick one.")]
    public string? Revision { get; init; }

    [CommandOption("-p|--path <DIR>")]
    [Description("Repository path. Defaults to the current directory.")]
    public string Path { get; init; } = ".";

    [CommandOption("--raw")]
    [Description("Print the commit as JSON (no diff text).")]
    public bool Raw { get; init; }

    [CommandOption("--no-diff")]
    [Description("Show the header and file list only.")]
    public bool NoDiff { get; init; }
}

/// <summary>One commit in detail. Without a revision it opens the commit picker (interactive only).</summary>
internal sealed class CommitCommand(IAnsiConsole console) : Command<CommitSettings>
{
    public override int Execute(CommandContext context, CommitSettings settings, CancellationToken cancellationToken)
    {
        // With --raw the output must be nothing but JSON, so no spinner text either.
        using var reader = settings.Raw ? RepositoryReader.Open(settings.Path) : ScanDisplay.Open(console, settings.Path);
        if (reader.IsEmpty)
        {
            throw new EmptyRepositoryException(settings.Path);
        }

        var revision = settings.Revision ?? Pick(reader);
        var limits = settings.Raw || settings.NoDiff ? DiffLimits.None : DiffLimits.Default;
        var detail = reader.ReadCommitDetail(revision, limits);

        if (settings.Raw)
        {
            CommitJson.Write(console, detail);
        }
        else
        {
            console.Write(CommitRenderer.Render(detail, console.Profile.Width, showDiff: !settings.NoDiff));
        }

        return 0;
    }

    private string Pick(RepositoryReader reader)
    {
        if (!console.Profile.Capabilities.Interactive)
        {
            throw new NeedsInputException(
                "No commit given",
                "Which commit? This terminal is not interactive, so GitPulse can't show the commit picker.",
                "Pass one, for example: gitpulse commit HEAD, gitpulse commit 1a2b3c4 or gitpulse commit main~2.");
        }

        return CommitPicker.Pick(console, reader.RecentCommits(CommitPicker.DefaultCount));
    }
}
