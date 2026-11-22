using System.ComponentModel;
using Spectre.Console.Cli;

namespace GitPulse.Commands;

/// <summary>Options every command accepts.</summary>
internal abstract class GlobalSettings : CommandSettings
{
    public const string NoColorOption = "--no-color";

    /// <summary>
    /// Declared here so it parses and shows in --help; it takes effect in <c>Program</c>, which
    /// creates the console without colors before any command runs (see <see cref="GitPulseApp.CreateConsole"/>).
    /// </summary>
    [CommandOption(NoColorOption)]
    [Description("Plain output without colors (also when the NO_COLOR environment variable is set).")]
    public bool NoColor { get; init; }
}
