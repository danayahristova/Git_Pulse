using System.ComponentModel;
using GitPulse.Analysis;
using GitPulse.Rendering;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitPulse.Commands;

internal sealed class HtmlReportSettings : RepoSettings
{
    [CommandOption("--html <FILE>")]
    [Description("Write the report to this HTML file.")]
    public string? Html { get; init; }

    [CommandOption("-f|--force")]
    [Description("Overwrite the file without asking.")]
    public bool Force { get; init; }

    public override ValidationResult Validate()
    {
        var baseResult = base.Validate();
        if (!baseResult.Successful)
        {
            return baseResult;
        }

        return string.IsNullOrWhiteSpace(Html)
            ? ValidationResult.Error("Say where to write the report: gitpulse report --html report.html")
            : ValidationResult.Success();
    }
}

/// <summary>
/// <c>gitpulse report --html FILE</c>: overview, activity, authors and hotspots in one standalone
/// HTML page. Asks before overwriting an existing file (ConfirmationPrompt), unless --force.
/// </summary>
internal sealed class HtmlReportCommand(IAnsiConsole console) : Command<HtmlReportSettings>
{
    public override int Execute(CommandContext context, HtmlReportSettings settings, CancellationToken cancellationToken)
    {
        var target = Path.GetFullPath(settings.Html!);
        var folder = Path.GetDirectoryName(target)!;
        if (!Directory.Exists(folder))
        {
            throw new GitPulseException(
                "Folder not found",
                $"The folder '{folder}' does not exist.",
                "Create it first, or write the report somewhere else.");
        }

        // Ask before scanning, so nobody waits for a scan only to be asked.
        if (File.Exists(target) && !settings.Force && !ConfirmOverwrite(target))
        {
            console.MarkupLine(Theme.Dim("Nothing was written."));
            return 0;
        }

        var stats = ScanDisplay.Run(console, settings, cancellationToken);
        var html = HtmlReport.Build(stats, settings.Top, DateTimeOffset.Now);

        console.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(new Style(Theme.Primary))
            .Start("Writing report…", _ => WriteAtomically(target, html));

        var size = Format.Number((new FileInfo(target).Length + 1023) / 1024) + " KB";
        console.WriteLine();
        console.MarkupLine(
            $"{Theme.Colored("Report written:", Theme.Added)} [link={new Uri(target).AbsoluteUri}]{Markup.Escape(target)}[/] {Theme.Dim($"({size})")}");
        return 0;
    }

    private bool ConfirmOverwrite(string target)
    {
        if (!console.Profile.Capabilities.Interactive)
        {
            throw new GitPulseException(
                "File already exists",
                $"'{target}' already exists.",
                "Pass --force to overwrite it, or choose another file name.");
        }

        return console.Prompt(
            new ConfirmationPrompt($"{Markup.Escape(Path.GetFileName(target))} already exists. Overwrite it?")
            {
                DefaultValue = false,
            });
    }

    private static void WriteAtomically(string target, string html)
    {
        var temp = target + "." + Environment.ProcessId + ".tmp";
        File.WriteAllText(temp, html, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temp, target, overwrite: true);
    }
}
