using System.ComponentModel;
using GitPulse.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitPulse.Commands;

/// <summary>Options shared by every command that analyzes a repository.</summary>
internal class RepoSettings : CommandSettings
{
    public const int MinTop = 1;
    public const int MaxTop = 100;

    [CommandOption("-p|--path <DIR>")]
    [Description("Repository path. Defaults to the current directory.")]
    public string Path { get; init; } = ".";

    [CommandOption("-s|--since <DATE>")]
    [Description("Only include commits on or after this date (yyyy-MM-dd).")]
    public DateOnly? Since { get; init; }

    [CommandOption("-u|--until <DATE>")]
    [Description("Only include commits on or before this date (yyyy-MM-dd).")]
    public DateOnly? Until { get; init; }

    [CommandOption("-b|--branch <NAME>")]
    [Description("Only include commits reachable from this local branch. Repeat for several. Default: HEAD.")]
    public string[] Branches { get; init; } = [];

    [CommandOption("-a|--author <TEXT>")]
    [Description("Only include commits whose author name or email contains this text (any case). Repeat for several.")]
    public string[] Authors { get; init; } = [];

    [CommandOption("-t|--top <N>")]
    [Description("Number of rows to show (1-100).")]
    [DefaultValue(10)]
    public int Top { get; init; } = 10;

    public ScanFilter ToFilter() => new(
        Since,
        Until,
        Branches.Where(b => !string.IsNullOrWhiteSpace(b)).Select(b => b.Trim()).Distinct().ToList(),
        Authors.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct().ToList());

    public override ValidationResult Validate()
    {
        if (Top is < MinTop or > MaxTop)
        {
            return ValidationResult.Error($"--top must be between {MinTop} and {MaxTop}, but was {Top}.");
        }

        if (Since is { } since && since > DateOnly.FromDateTime(DateTime.Today))
        {
            return ValidationResult.Error($"--since cannot be in the future ({since:yyyy-MM-dd}).");
        }

        if (Since is { } from && Until is { } to && to < from)
        {
            return ValidationResult.Error($"--until ({to:yyyy-MM-dd}) is before --since ({from:yyyy-MM-dd}).");
        }

        if (Authors.Any(string.IsNullOrWhiteSpace) || Branches.Any(string.IsNullOrWhiteSpace))
        {
            return ValidationResult.Error("--author and --branch need a value.");
        }

        return ValidationResult.Success();
    }
}
