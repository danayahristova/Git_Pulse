using GitPulse.Commands;
using Spectre.Console.Cli;

var app = new CommandApp<RecentCommitsCommand>();
app.Configure(config =>
{
    config.SetApplicationName("gitpulse");
});

return app.Run(args);
