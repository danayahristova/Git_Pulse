using GitPulse.Interactive;
using GitPulse.Tests.Git;
using Spectre.Console;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;
using static GitPulse.Tests.Fakes;

namespace GitPulse.Tests.Cli;

public sealed class GlobalOptionsTests : IDisposable
{
    private readonly TempRepo _repo = new();

    public GlobalOptionsTests()
    {
        _repo.Commit("Add", "Ada Lovelace", Day(1, 1), ("a.cs", "class A {}\n"));
    }

    private static CommandAppResult Run(params string[] args)
    {
        var tester = new CommandAppTester(null!, new CommandAppTesterSettings(), new TestConsole().Width(120));
        tester.SetDefaultCommand<InteractiveCommand>();
        tester.Configure(GitPulseApp.Configure);
        return tester.Run(args);
    }

    [Fact]
    public void No_color_turns_colors_off_for_the_whole_run()
    {
        Assert.Equal(ColorSystem.NoColors, GitPulseApp.CreateConsole(["overview", "--no-color"]).Profile.Capabilities.ColorSystem);
        Assert.Equal(ColorSystem.NoColors, GitPulseApp.CreateConsole(["--NO-COLOR"]).Profile.Capabilities.ColorSystem);
    }

    [Theory]
    [InlineData("overview")]
    [InlineData("authors")]
    [InlineData("hotspots")]
    [InlineData("heatmap")]
    [InlineData("scan")]
    [InlineData("commit")]
    public void Every_command_accepts_no_color(string command)
    {
        var args = command == "commit"
            ? new[] { command, "HEAD", "--path", _repo.Dir, "--no-color" }
            : new[] { command, "--path", _repo.Dir, "--no-color" };

        Assert.Equal(0, Run(args).ExitCode);
    }

    [Fact]
    public void Version_prints_the_package_version()
    {
        var result = Run("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+", result.Output.Trim());
        Assert.Equal(GitPulseApp.Version, result.Output.Trim());
    }

    [Fact]
    public void Help_lists_every_command()
    {
        var result = Run("--help");

        foreach (var command in new[] { "scan", "overview", "authors", "hotspots", "commit", "report", "watch", "heatmap" })
        {
            Assert.Contains(command, result.Output);
        }

        Assert.Contains("--no-color", result.Output);
    }

    public void Dispose() => _repo.Dispose();
}
