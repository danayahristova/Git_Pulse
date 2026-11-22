using System.ComponentModel;
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

    [CommandOption("-t|--top <N>")]
    [Description("Number of rows to show (1-100).")]
    [DefaultValue(10)]
    public int Top { get; init; } = 10;

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

        return ValidationResult.Success();
    }
}
